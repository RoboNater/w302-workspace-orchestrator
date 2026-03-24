using WorkspaceOrchestrator.Core.Config;
using WorkspaceOrchestrator.Core.Models;

namespace WorkspaceOrchestrator.Core.Services;

/// <summary>
/// Orchestrates a full project stow: find and close all project windows by title pattern.
/// Uses the deploy journal to identify exactly which windows belong to the project.
/// </summary>
public class StowService
{
    private readonly WindowManager _wm;
    private readonly StateManager  _state;

    public StowService(WindowManager wm, StateManager state)
    {
        _wm    = wm;
        _state = state;
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

    private int StowFromRecord(DeployedProject record, TextWriter output, bool dryRun)
    {
        int closed = 0;

        foreach (var app in record.Apps)
        {
            output.WriteLine($"  Closing {app.Type} (pattern: {app.TitlePattern})...");

            if (dryRun)
            {
                var targets = FindWindows(app.ProcessName, app.TitlePattern);
                output.WriteLine($"    [dry-run] Would close {targets.Count} window(s).");
                continue;
            }

            int count = _wm.CloseWindowsByPattern(app.ProcessName, app.TitlePattern);
            if (count > 0)
            {
                output.WriteLine($"    Closed {count} window(s).");
                closed += count;
            }
            else
            {
                output.WriteLine($"    No windows found (already closed).");
            }
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
