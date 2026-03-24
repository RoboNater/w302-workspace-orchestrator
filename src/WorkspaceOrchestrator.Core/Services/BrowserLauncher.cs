using System.Diagnostics;
using WorkspaceOrchestrator.Core.Models;

namespace WorkspaceOrchestrator.Core.Services;

/// <summary>
/// Launches Chrome or Edge with a named profile and initial URLs.
/// After launch, finds the new browser window for journal recording and positioning.
/// </summary>
public class BrowserLauncher
{
    private readonly WindowManager _wm;

    public const string ChromeProcessName = "chrome";
    public const string EdgeProcessName   = "msedge";

    public BrowserLauncher(WindowManager wm)
    {
        _wm = wm;
    }

    /// <summary>Find the Chrome executable at common install locations.</summary>
    public static string? FindChromeExe()
    {
        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google", "Chrome", "Application", "chrome.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>Find the Edge executable at common install locations.</summary>
    public static string? FindEdgeExe()
    {
        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft", "Edge", "Application", "msedge.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Returns the process name for the given app type ("chrome" or "edge").
    /// </summary>
    public static string GetProcessName(string appType) =>
        appType.Equals("edge", StringComparison.OrdinalIgnoreCase)
            ? EdgeProcessName
            : ChromeProcessName;

    /// <summary>
    /// Launch a browser with the specified profile and initial URLs.
    /// Finds the new browser window after launch, positions it, and moves it to the target desktop.
    /// </summary>
    /// <param name="app">App config (type: "chrome" or "edge"; profile; urls; window)</param>
    /// <param name="targetDesktop">Virtual desktop index (0-based), or -1 for current</param>
    /// <param name="vd">Virtual desktop service used to move the window</param>
    /// <returns>DeployedApp record to persist in the state journal</returns>
    public DeployedApp Launch(AppConfig app, int targetDesktop, VirtualDesktopService vd)
    {
        bool isEdge      = app.Type.Equals("edge", StringComparison.OrdinalIgnoreCase);
        string procName  = isEdge ? EdgeProcessName : ChromeProcessName;
        string? exe      = isEdge ? FindEdgeExe() : FindChromeExe();

        if (exe is null)
            throw new FileNotFoundException(
                $"{(isEdge ? "Microsoft Edge" : "Google Chrome")} executable not found. " +
                "Ensure the browser is installed, or remove this entry from the config.");

        // Build argument list
        var args = new List<string>();

        // Named profile (maps to profile directory, e.g. "Profile 1" or custom name).
        // Chrome/Edge create the profile automatically on first launch if it doesn't exist.
        if (!string.IsNullOrWhiteSpace(app.Profile))
            args.Add($"--profile-directory={app.Profile}");

        // Initial URLs (opened as tabs)
        foreach (var url in app.Urls)
            args.Add(url);

        // Snapshot existing browser windows so we can identify the new one
        var existingHwnds = _wm.GetAllWindows()
            .Where(w => string.Equals(w.ProcessName, procName, StringComparison.OrdinalIgnoreCase))
            .Select(w => w.Hwnd)
            .ToHashSet();

        var psi = new ProcessStartInfo
        {
            FileName        = exe,
            Arguments       = string.Join(" ", args),
            UseShellExecute = false,
        };
        Process.Start(psi);

        // Poll for the new browser window (up to 20 seconds)
        nint hwnd = 0;
        var newWin = _wm.FindWindow(procName, ".*", 20, excludeHwnds: existingHwnds);

        if (newWin is not null)
        {
            hwnd = newWin.Hwnd;

            var pos = app.Window?.Position;
            if (pos is not null)
                _wm.PositionWindow(hwnd, pos.X, pos.Y, pos.Width, pos.Height);

            if (targetDesktop >= 0)
                vd.MoveWindowToDesktop(hwnd, targetDesktop);
        }

        return new DeployedApp
        {
            Type         = isEdge ? "edge" : "chrome",
            ProcessName  = procName,
            // HWND is the primary stow key for browsers (title is unpredictable).
            // TitlePattern ".*" is the fallback in case the HWND goes stale.
            TitlePattern = ".*",
            Hwnd         = (long)hwnd,
        };
    }
}
