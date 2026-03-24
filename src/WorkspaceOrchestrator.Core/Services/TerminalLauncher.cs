using System.Diagnostics;
using WorkspaceOrchestrator.Core.Models;

namespace WorkspaceOrchestrator.Core.Services;

/// <summary>
/// Launches Windows Terminal with multiple named tabs, using the <c>--window ws-&lt;project&gt;</c>
/// naming convention so each project's terminal window can be reliably identified at stow time.
/// </summary>
public class TerminalLauncher
{
    private const string WtExe = "wt.exe";

    /// <summary>
    /// Launch Windows Terminal with the given tabs for the specified project.
    /// The window title is set to <c>ws-{projectName}</c> via <c>--window</c>.
    /// </summary>
    /// <param name="projectName">Project identifier — becomes the WT window name/title.</param>
    /// <param name="tabs">Tab configurations.</param>
    /// <param name="defaultShell">Default shell profile name (e.g. "pwsh").</param>
    public void LaunchWithTabs(string projectName, IEnumerable<TabConfig> tabs, string defaultShell = "pwsh")
    {
        var tabList = tabs.ToList();
        if (tabList.Count == 0) return;

        string windowName = $"ws-{projectName}";

        // Build a single argument string.
        // IMPORTANT: pass as a single string (not an array) so that ';' tab separators
        // are not individually quoted by Process.Start, which would break wt parsing.
        var parts = new List<string>();
        foreach (var tab in tabList)
        {
            string shell = string.IsNullOrEmpty(tab.Shell) ? defaultShell : tab.Shell;
            string dir   = tab.Directory.Replace('/', '\\');
            string title = tab.Title;

            // First tab also sets the window name; subsequent tabs append to the same window.
            string part = $"new-tab --window \"{windowName}\" -p \"{shell}\" -d \"{dir}\" --title \"{title}\"";

            if (!string.IsNullOrEmpty(tab.RunOnDeploy))
                part += $" -- {shell} -NoExit -Command \"{tab.RunOnDeploy}\"";

            parts.Add(part);
        }

        string argString = string.Join(" ; ", parts);

        var psi = new ProcessStartInfo
        {
            FileName        = WtExe,
            Arguments       = argString,
            UseShellExecute = true,
        };

        Process.Start(psi);
    }

    /// <summary>
    /// Get the expected title pattern for a project's terminal window.
    /// Used by stow to find and close the correct window.
    /// </summary>
    public static string GetWindowTitlePattern(string projectName)
        => $"^ws-{System.Text.RegularExpressions.Regex.Escape(projectName)}$";

    /// <summary>The WT process name as reported by Windows.</summary>
    public const string ProcessName = "WindowsTerminal";
}
