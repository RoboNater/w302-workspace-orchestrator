using WorkspaceOrchestrator.Core.Config;
using WorkspaceOrchestrator.Core.Models;

namespace WorkspaceOrchestrator.Core.Services;

/// <summary>
/// Orchestrates a full project stow: find and close all project windows by title pattern.
/// Uses the deploy journal to identify exactly which windows belong to the project.
/// </summary>
public class StowService
{
    private readonly WindowManager   _wm;
    private readonly StateManager    _state;
    private readonly SnapshotService _snapshot;

    public StowService(WindowManager wm, StateManager state, SnapshotService snapshot)
    {
        _wm       = wm;
        _state    = state;
        _snapshot = snapshot;
    }

    /// <summary>
    /// Stow a deployed project. Closes all windows recorded in the deploy journal.
    /// </summary>
    /// <param name="projectName">Project to stow.</param>
    /// <param name="output">Writer for progress output.</param>
    /// <param name="dryRun">If true, show what would be done without doing it.</param>
    /// <returns>Number of windows closed.</returns>
    public int Stow(string projectName, TextWriter? output = null, bool dryRun = false)
    {
        output ??= Console.Out;

        var record = _state.GetDeployedProject(projectName);
        if (record is null)
        {
            output.WriteLine($"  Project '{projectName}' is not currently deployed (no journal entry).");
            output.WriteLine("  Attempting to find and close windows by convention anyway...");
            return StowByConvention(projectName, output, dryRun);
        }

        return StowFromRecord(record, output, dryRun);
    }

    // App types that use HWND-first close (title is unpredictable at stow time)
    private static readonly HashSet<string> HwndFirstTypes =
        new(StringComparer.OrdinalIgnoreCase) { "chrome", "edge", "browser" };

    private int StowFromRecord(DeployedProject record, TextWriter output, bool dryRun)
    {
        // Capture window positions before closing so the next deploy can restore the layout.
        if (!dryRun)
        {
            var snap = _snapshot.Capture(record.Project, output);
            if (snap is not null)
            {
                _state.SaveSnapshot(snap);
                output.WriteLine($"  Snapshot saved ({snap.Apps.Count} window(s)).");
            }
        }

        int closed = 0;

        foreach (var app in record.Apps)
        {
            bool useHwndFirst = HwndFirstTypes.Contains(app.Type) && app.Hwnd != 0;
            string closeDesc  = useHwndFirst
                ? $"hwnd:{app.Hwnd}"
                : $"pattern: {app.TitlePattern}";

            output.WriteLine($"  Closing {app.Type} ({closeDesc})...");

            if (dryRun)
            {
                if (useHwndFirst)
                    output.WriteLine($"    [dry-run] Would close window hwnd:{app.Hwnd}.");
                else
                {
                    var targets = FindWindows(app.ProcessName, app.TitlePattern);
                    output.WriteLine($"    [dry-run] Would close {targets.Count} window(s).");
                }
                continue;
            }

            int count;
            if (useHwndFirst)
            {
                // Try HWND first; fall back to pattern if the handle is stale
                bool found = _wm.CloseWindowByHwnd((nint)app.Hwnd);
                if (found)
                {
                    output.WriteLine("    Closed by hwnd.");
                    count = 1;
                }
                else
                {
                    output.WriteLine("    hwnd stale — falling back to pattern match...");
                    count = _wm.CloseWindowsByPattern(app.ProcessName, app.TitlePattern);
                    output.WriteLine(count > 0
                        ? $"    Closed {count} window(s) by pattern."
                        : "    No windows found (already closed).");
                }
            }
            else
            {
                count = _wm.CloseWindowsByPattern(app.ProcessName, app.TitlePattern);
                output.WriteLine(count > 0
                    ? $"    Closed {count} window(s)."
                    : "    No windows found (already closed).");
            }

            closed += count;
        }

        if (!dryRun)
            _state.RecordStow(record.Project);

        return closed;
    }

    private int StowByConvention(string projectName, TextWriter output, bool dryRun)
    {
        // Fall back to convention-based matching when there's no journal entry.
        // This handles the case where deploy was run with the old PowerShell scripts.
        int closed = 0;

        var conventions = new[]
        {
            (ProcessName: TerminalLauncher.ProcessName,
             Pattern: TerminalLauncher.GetWindowTitlePattern(projectName)),
            (ProcessName: "Code",
             Pattern: "Visual Studio Code"),
            (ProcessName: "explorer",
             Pattern: System.Text.RegularExpressions.Regex.Escape(projectName)),
        };

        foreach (var (proc, pattern) in conventions)
        {
            output.WriteLine($"  Closing {proc} (pattern: {pattern})...");

            if (dryRun)
            {
                var targets = FindWindows(proc, pattern);
                output.WriteLine($"    [dry-run] Would close {targets.Count} window(s).");
                continue;
            }

            int count = _wm.CloseWindowsByPattern(proc, pattern);
            output.WriteLine(count > 0
                ? $"    Closed {count} window(s)."
                : "    No windows found.");
            closed += count;
        }

        if (!dryRun)
            _state.RecordStow(projectName);

        return closed;
    }

    private IReadOnlyList<WorkspaceOrchestrator.Core.Models.WindowInfo> FindWindows(
        string processName, string titlePattern)
    {
        var regex = new System.Text.RegularExpressions.Regex(titlePattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return _wm.GetAllWindows()
            .Where(w => string.Equals(w.ProcessName, processName,
                            StringComparison.OrdinalIgnoreCase)
                        && regex.IsMatch(w.Title))
            .ToList();
    }
}
