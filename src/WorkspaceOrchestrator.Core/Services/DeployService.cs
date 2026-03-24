using WorkspaceOrchestrator.Core.Config;
using WorkspaceOrchestrator.Core.Models;

namespace WorkspaceOrchestrator.Core.Services;

/// <summary>
/// Orchestrates a full project deploy: launch all apps, position windows, assign to virtual desktop.
/// </summary>
public class DeployService
{
    private readonly WindowManager         _wm;
    private readonly VirtualDesktopService _vd;
    private readonly TerminalLauncher      _tl;
    private readonly BrowserLauncher       _bl;
    private readonly StateManager          _state;

    // VS Code executable path
    private static readonly string VsCodeExe = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "Microsoft VS Code", "Code.exe");

    public DeployService(
        WindowManager wm,
        VirtualDesktopService vd,
        TerminalLauncher tl,
        BrowserLauncher bl,
        StateManager state)
    {
        _wm    = wm;
        _vd    = vd;
        _tl    = tl;
        _bl    = bl;
        _state = state;
    }

    /// <summary>
    /// Deploy a project. Returns a deploy record for the state journal.
    /// Progress messages are written to <paramref name="output"/> (default: Console.Out).
    /// </summary>
    public DeployedProject Deploy(ProjectContext config, string configPath,
        TextWriter? output = null, bool dryRun = false)
    {
        output ??= Console.Out;
        string name = config.Meta.Name;

        // Resolve target virtual desktop (0-based index)
        int targetDesktop = config.VirtualDesktops is not null
            ? config.VirtualDesktops.Primary - 1
            : -1;

        // Load snapshot so we can restore the last-known window positions
        var snapshot = _state.LoadSnapshot(name);
        if (snapshot is not null)
            output.WriteLine($"  Found saved snapshot ({snapshot.Apps.Count} positions) — will prefer snapshot layout.");

        var deployedApps = new List<DeployedApp>();

        // Step 1: Ensure virtual desktops exist
        if (targetDesktop >= 0)
        {
            output.WriteLine($"[1] Setting up virtual desktop {targetDesktop + 1}...");
            if (!dryRun)
                _vd.EnsureDesktopCount(targetDesktop + 1);
        }

        int step = 1;
        int total = config.Applications.Count + (targetDesktop >= 0 ? 2 : 0);

        // Step 2+: Launch apps
        foreach (var (appKey, app) in config.Applications)
        {
            step++;
            string label = $"[{step}/{total}]";

            switch (app.Type.ToLowerInvariant())
            {
                case "vscode":
                    output.WriteLine($"{label} Launching VS Code ({appKey})...");
                    if (!dryRun)
                    {
                        var pos = SnapshotPosition("vscode", snapshot) ?? app.Window?.Position;
                        if (snapshot is not null && pos != app.Window?.Position)
                            output.WriteLine($"       Using snapshot position: [{pos!.X},{pos.Y}] {pos.Width}x{pos.Height}");
                        var da = LaunchVsCode(app, name, targetDesktop, pos);
                        deployedApps.Add(da);
                    }
                    break;

                case "windows-terminal":
                case "terminal":
                    output.WriteLine($"{label} Launching Windows Terminal ({appKey})...");
                    if (!dryRun)
                    {
                        var pos = SnapshotPosition("terminal", snapshot) ?? app.Window?.Position;
                        if (snapshot is not null && pos != app.Window?.Position)
                            output.WriteLine($"       Using snapshot position: [{pos!.X},{pos.Y}] {pos.Width}x{pos.Height}");
                        var da = LaunchTerminal(app, name, targetDesktop, pos);
                        deployedApps.Add(da);
                    }
                    break;

                case "file-explorer":
                case "explorer":
                    output.WriteLine($"{label} Launching File Explorer ({appKey})...");
                    if (!dryRun)
                    {
                        var pos = SnapshotPosition("explorer", snapshot) ?? app.Window?.Position;
                        if (snapshot is not null && pos != app.Window?.Position)
                            output.WriteLine($"       Using snapshot position: [{pos!.X},{pos.Y}] {pos.Width}x{pos.Height}");
                        var das = LaunchExplorer(app, name, targetDesktop, pos);
                        deployedApps.AddRange(das);
                    }
                    break;

                case "chrome":
                case "edge":
                    output.WriteLine($"{label} Launching {app.Type} browser ({appKey})...");
                    if (!dryRun)
                    {
                        var pos = SnapshotPosition(app.Type, snapshot) ?? app.Window?.Position;
                        if (snapshot is not null && pos != app.Window?.Position)
                            output.WriteLine($"       Using snapshot position: [{pos!.X},{pos.Y}] {pos.Width}x{pos.Height}");
                        var da = LaunchBrowser(app, targetDesktop, pos);
                        deployedApps.Add(da);
                    }
                    break;

                default:
                    output.WriteLine($"{label} [SKIP] Unknown app type '{app.Type}' for '{appKey}'");
                    break;
            }
        }

        // Final step: switch to target desktop
        if (targetDesktop >= 0 && !dryRun)
        {
            output.WriteLine($"[{total}] Switching to virtual desktop {targetDesktop + 1}...");
            _vd.SwitchToDesktop(targetDesktop);
        }

        var record = new DeployedProject
        {
            Project    = name,
            DeployedAt = DateTimeOffset.UtcNow,
            ConfigPath = configPath,
            Apps       = deployedApps,
        };

        if (!dryRun)
            _state.RecordDeploy(record);

        return record;
    }

    /// <summary>
    /// Look up a saved snapshot position for an app type.
    /// Returns null if no snapshot exists or the app type isn't in it.
    /// </summary>
    private static WindowPosition? SnapshotPosition(string appType, WindowSnapshot? snapshot)
    {
        if (snapshot is null) return null;
        var entry = snapshot.Apps.FirstOrDefault(a =>
            string.Equals(a.Type, appType, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return null;
        return new WindowPosition { X = entry.X, Y = entry.Y, Width = entry.Width, Height = entry.Height };
    }

    private DeployedApp LaunchVsCode(AppConfig app, string projectName, int targetDesktop,
        WindowPosition? posOverride = null)
    {
        string? workspace = app.Workspace?.Replace('/', '\\');
        var pos = posOverride ?? app.Window?.Position;

        nint hwnd = 0;
        try
        {
            // Snapshot existing VS Code windows to avoid matching a pre-existing instance
            var existingHwnds = _wm.GetAllWindows()
                .Where(w => w.ProcessName == "Code")
                .Select(w => w.Hwnd)
                .ToHashSet();

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName        = VsCodeExe,
                Arguments       = workspace ?? string.Empty,
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(psi);

            var win = _wm.FindWindow("Code", "Visual Studio Code", 20,
                excludeHwnds: existingHwnds);

            if (win is not null)
            {
                if (pos is not null)
                    _wm.PositionWindow(win.Hwnd, pos.X, pos.Y, pos.Width, pos.Height);

                if (targetDesktop >= 0)
                    _vd.MoveWindowToDesktop(win.Hwnd, targetDesktop);

                hwnd = win.Hwnd;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [WARN] VS Code launch failed: {ex.Message}");
        }

        return new DeployedApp
        {
            Type         = "vscode",
            TitlePattern = "Visual Studio Code",
            ProcessName  = "Code",
            Hwnd         = (long)hwnd,
        };
    }

    private DeployedApp LaunchTerminal(AppConfig app, string projectName, int targetDesktop,
        WindowPosition? posOverride = null)
    {
        var pos = posOverride ?? app.Window?.Position;
        nint hwnd = 0;

        try
        {
            var existingHwnds = _wm.GetAllWindows()
                .Where(w => w.ProcessName == TerminalLauncher.ProcessName)
                .Select(w => w.Hwnd)
                .ToHashSet();

            _tl.LaunchWithTabs(projectName, app.Tabs);

            // Wait for the new WT window — identified by --window title
            string pattern = TerminalLauncher.GetWindowTitlePattern(projectName);
            var win = _wm.FindWindow(TerminalLauncher.ProcessName, pattern, 15,
                excludeHwnds: existingHwnds);

            if (win is not null)
            {
                if (pos is not null)
                    _wm.PositionWindow(win.Hwnd, pos.X, pos.Y, pos.Width, pos.Height);

                if (targetDesktop >= 0)
                    _vd.MoveWindowToDesktop(win.Hwnd, targetDesktop);

                hwnd = win.Hwnd;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [WARN] Terminal launch failed: {ex.Message}");
        }

        return new DeployedApp
        {
            Type         = "terminal",
            TitlePattern = TerminalLauncher.GetWindowTitlePattern(projectName),
            ProcessName  = TerminalLauncher.ProcessName,
            Hwnd         = (long)hwnd,
        };
    }

    private DeployedApp LaunchBrowser(AppConfig app, int targetDesktop,
        WindowPosition? posOverride = null)
    {
        // Apply snapshot position override to the app config before launching
        AppConfig effectiveApp = app;
        if (posOverride is not null && app.Window is not null)
        {
            effectiveApp = new AppConfig
            {
                Type    = app.Type,
                Profile = app.Profile,
                Urls    = app.Urls,
                Window  = new WindowConfig { Monitor = app.Window.Monitor, Position = posOverride },
            };
        }

        try
        {
            return _bl.Launch(effectiveApp, targetDesktop, _vd);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [WARN] Browser launch failed: {ex.Message}");
            return new DeployedApp
            {
                Type         = app.Type,
                ProcessName  = BrowserLauncher.GetProcessName(app.Type),
                TitlePattern = ".*",
                Hwnd         = 0,
            };
        }
    }

    private IEnumerable<DeployedApp> LaunchExplorer(AppConfig app, string projectName, int targetDesktop,
        WindowPosition? posOverride = null)
    {
        var pos = posOverride ?? app.Window?.Position;

        foreach (var rawPath in app.Paths)
        {
            string nativePath = rawPath.Replace('/', '\\');
            string folderName = Path.GetFileName(nativePath.TrimEnd('\\'));
            nint hwnd = 0;

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = "explorer.exe",
                    Arguments       = nativePath,
                    UseShellExecute = true,
                };
                System.Diagnostics.Process.Start(psi);

                // Match by folder name in window title (e.g. "sample-project-1 - File Explorer")
                var win = _wm.FindWindow("explorer",
                    System.Text.RegularExpressions.Regex.Escape(folderName), 15);

                if (win is not null)
                {
                    if (pos is not null)
                        _wm.PositionWindow(win.Hwnd, pos.X, pos.Y, pos.Width, pos.Height);

                    if (targetDesktop >= 0)
                        _vd.MoveWindowToDesktop(win.Hwnd, targetDesktop);

                    hwnd = win.Hwnd;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  [WARN] Explorer launch failed for '{nativePath}': {ex.Message}");
            }

            yield return new DeployedApp
            {
                Type         = "explorer",
                TitlePattern = System.Text.RegularExpressions.Regex.Escape(folderName),
                ProcessName  = "explorer",
                Hwnd         = (long)hwnd,
            };
        }
    }
}
