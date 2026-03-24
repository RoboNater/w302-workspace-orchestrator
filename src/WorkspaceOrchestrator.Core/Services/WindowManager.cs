using System.Diagnostics;
using System.Text;
using WorkspaceOrchestrator.Core.Interop;
using WorkspaceOrchestrator.Core.Models;

namespace WorkspaceOrchestrator.Core.Services;

/// <summary>
/// High-level window management: enumerate, find, position, close.
/// </summary>
public class WindowManager
{
    /// <summary>Enumerate all visible top-level windows with title and position info.</summary>
    public IReadOnlyList<WindowInfo> GetAllWindows()
    {
        var result = new List<WindowInfo>();

        Win32.EnumWindows((hwnd, _) =>
        {
            if (!Win32.IsWindowVisible(hwnd)) return true;

            int len = Win32.GetWindowTextLength(hwnd);
            if (len == 0) return true;

            var sb = new StringBuilder(len + 1);
            Win32.GetWindowText(hwnd, sb, sb.Capacity);
            string title = sb.ToString();

            Win32.GetWindowThreadProcessId(hwnd, out uint pid);

            string procName = string.Empty;
            try { procName = Process.GetProcessById((int)pid).ProcessName; }
            catch { /* process may have exited */ }

            Win32.GetWindowRect(hwnd, out var rect);

            result.Add(new WindowInfo(hwnd, title, pid, procName,
                rect.Left, rect.Top, rect.Width, rect.Height));

            return true;
        }, nint.Zero);

        return result;
    }

    /// <summary>
    /// Poll for a window matching the given criteria, up to <paramref name="timeoutSeconds"/>.
    /// </summary>
    public WindowInfo? FindWindow(
        string processName,
        string titlePattern = ".*",
        int timeoutSeconds = 15,
        int pollIntervalMs = 200,
        IEnumerable<nint>? excludeHwnds = null)
    {
        var exclude = new HashSet<nint>(excludeHwnds ?? []);
        var regex   = new System.Text.RegularExpressions.Regex(titlePattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            var match = GetAllWindows().FirstOrDefault(w =>
                string.Equals(w.ProcessName, processName, StringComparison.OrdinalIgnoreCase)
                && regex.IsMatch(w.Title)
                && !exclude.Contains(w.Hwnd));

            if (match is not null) return match;
            Thread.Sleep(pollIntervalMs);
        }

        return null;
    }

    /// <summary>Move and resize a window to exact pixel coordinates.</summary>
    public bool PositionWindow(nint hwnd, int x, int y, int width, int height)
    {
        Win32.ShowWindow(hwnd, Win32.SW_RESTORE);
        uint flags = Win32.SWP_NOZORDER | Win32.SWP_SHOWWINDOW;
        return Win32.SetWindowPos(hwnd, nint.Zero, x, y, width, height, flags);
    }

    /// <summary>
    /// Close a window gracefully via WM_CLOSE.
    /// If the window survives <paramref name="gracePeriodMs"/> (e.g. "close all tabs?" dialog),
    /// sends a second WM_CLOSE to dismiss the confirmation dialog.
    /// </summary>
    public void CloseWindow(nint hwnd, int gracePeriodMs = 800)
    {
        Win32.SendMessage(hwnd, Win32.WM_CLOSE, nint.Zero, nint.Zero);
        Thread.Sleep(gracePeriodMs);

        if (Win32.IsWindowVisible(hwnd))
            Win32.SendMessage(hwnd, Win32.WM_CLOSE, nint.Zero, nint.Zero);
    }

    /// <summary>
    /// Close all windows matching the given criteria.
    /// Returns the number of windows closed.
    /// </summary>
    public int CloseWindowsByPattern(string processName, string titlePattern, int gracePeriodMs = 800)
    {
        var regex = new System.Text.RegularExpressions.Regex(titlePattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var targets = GetAllWindows().Where(w =>
            string.Equals(w.ProcessName, processName, StringComparison.OrdinalIgnoreCase)
            && regex.IsMatch(w.Title)).ToList();

        foreach (var win in targets)
            CloseWindow(win.Hwnd, gracePeriodMs);

        return targets.Count;
    }
}
