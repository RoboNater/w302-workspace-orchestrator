using System.Text.RegularExpressions;
using WorkspaceOrchestrator.Core.Config;
using WorkspaceOrchestrator.Core.Models;

namespace WorkspaceOrchestrator.Core.Services;

/// <summary>
/// Captures the current on-screen positions of all windows belonging to a deployed project.
/// The resulting <see cref="WindowSnapshot"/> is saved by <see cref="StateManager"/> and
/// consumed by <see cref="DeployService"/> to restore the last-known layout on next deploy.
/// </summary>
public class SnapshotService
{
    private readonly WindowManager _wm;
    private readonly StateManager  _state;

    public SnapshotService(WindowManager wm, StateManager state)
    {
        _wm    = wm;
        _state = state;
    }

    /// <summary>
    /// Capture current window positions for a deployed project.
    /// Returns null if the project is not in the deploy journal.
    /// </summary>
    /// <param name="projectName">Project to snapshot.</param>
    /// <param name="output">Writer for diagnostic messages.</param>
    public WindowSnapshot? Capture(string projectName, TextWriter? output = null)
    {
        output ??= TextWriter.Null;

        var record = _state.GetDeployedProject(projectName);
        if (record is null)
        {
            output.WriteLine($"  '{projectName}' is not in the deploy journal — cannot snapshot.");
            return null;
        }

        var allWindows = _wm.GetAllWindows();
        var apps       = new List<AppSnapshot>();

        foreach (var app in record.Apps)
        {
            var win = FindWindow(allWindows, app);
            if (win is not null)
            {
                output.WriteLine(
                    $"  [{app.Type}] captured: [{win.X},{win.Y}] {win.Width}x{win.Height}  \"{win.Title}\"");

                apps.Add(new AppSnapshot
                {
                    Type         = app.Type,
                    ProcessName  = app.ProcessName,
                    TitlePattern = app.TitlePattern,
                    Hwnd         = app.Hwnd,
                    X            = win.X,
                    Y            = win.Y,
                    Width        = win.Width,
                    Height       = win.Height,
                });
            }
            else
            {
                output.WriteLine($"  [{app.Type}] window not found (already closed?)");
            }
        }

        return new WindowSnapshot
        {
            Project    = projectName,
            CapturedAt = DateTimeOffset.UtcNow,
            Apps       = apps,
        };
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static WindowInfo? FindWindow(IReadOnlyList<WindowInfo> allWindows, DeployedApp app)
    {
        // HWND first — fast and exact when the window hasn't been restarted
        if (app.Hwnd != 0)
        {
            var byHwnd = allWindows.FirstOrDefault(w => w.Hwnd == (nint)app.Hwnd);
            if (byHwnd is not null) return byHwnd;
        }

        // Title-pattern fallback — reliable even after WT or VS Code restarts
        if (string.IsNullOrEmpty(app.TitlePattern)) return null;

        try
        {
            var regex = new Regex(app.TitlePattern, RegexOptions.IgnoreCase);
            return allWindows.FirstOrDefault(w =>
                string.Equals(w.ProcessName, app.ProcessName, StringComparison.OrdinalIgnoreCase)
                && regex.IsMatch(w.Title));
        }
        catch
        {
            return null;
        }
    }
}
