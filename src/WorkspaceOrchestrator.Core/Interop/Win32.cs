using System.Runtime.InteropServices;
using System.Text;

namespace WorkspaceOrchestrator.Core.Interop;

/// <summary>
/// P/Invoke declarations for Win32 window management APIs.
/// </summary>
internal static class Win32
{
    public delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(nint hwnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(nint hwnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        nint hwnd, nint hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(nint hwnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(nint hwnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(nint hwnd);

    [DllImport("user32.dll")]
    public static extern nint SendMessage(nint hwnd, uint Msg, nint wParam, nint lParam);

    // SetWindowPos flags
    public const uint SWP_NOZORDER    = 0x0004;
    public const uint SWP_SHOWWINDOW  = 0x0040;
    public const uint SWP_NOACTIVATE  = 0x0010;

    // ShowWindow commands
    public const int SW_RESTORE        = 9;
    public const int SW_SHOWNORMAL     = 1;

    // Messages
    public const uint WM_CLOSE         = 0x0010;
    public const uint WM_QUIT          = 0x0012;
    public const uint WM_HOTKEY        = 0x0312;

    // RegisterHotKey modifier flags
    public const uint MOD_ALT          = 0x0001;
    public const uint MOD_CONTROL      = 0x0002;
    public const uint MOD_SHIFT        = 0x0004;
    public const uint MOD_WIN          = 0x0008;
    public const uint MOD_NOREPEAT     = 0x4000;

    // Global hotkey registration
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(nint hWnd, int id);

    // Win32 message loop
    [DllImport("user32.dll")]
    public static extern int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    public static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern nint DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern bool PostThreadMessage(uint idThread, uint Msg, nint wParam, nint lParam);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public int  ptX;
        public int  ptY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width  => Right - Left;
        public int Height => Bottom - Top;
    }
}
