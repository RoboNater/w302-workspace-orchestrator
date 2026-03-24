using System.Runtime.InteropServices;
using WorkspaceOrchestrator.Core.Interop;

namespace WorkspaceOrchestrator.Core.Services;

/// <summary>
/// Registers global hotkeys and runs a Win32 message loop to detect them.
/// Must be started on a dedicated thread (or the CLI's main thread when blocking is acceptable).
/// Call <see cref="Stop"/> from any thread to unblock <see cref="Run"/>.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private readonly Dictionary<int, string> _idToProject = new();
    private volatile bool _running;
    private uint _ownerThreadId;

    /// <summary>Raised on the message-loop thread when a registered hotkey is pressed.</summary>
    public event Action<string>? HotkeyPressed;

    /// <summary>A hotkey registration binding a Win32 id to a project name and hotkey string.</summary>
    public sealed record HotkeyRegistration(int Id, string ProjectName, string Hotkey);

    // ── Hotkey parsing ────────────────────────────────────────────────────────

    /// <summary>
    /// Parse a hotkey string such as "Ctrl+Alt+1" or "Ctrl+Shift+F5" into Win32
    /// modifier flags and a virtual-key code.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the key token is unrecognised.</exception>
    public static (uint Modifiers, uint VirtualKey) ParseHotkey(string hotkey)
    {
        uint mods = Win32.MOD_NOREPEAT;
        uint vk   = 0;

        foreach (string part in hotkey.Split('+').Select(p => p.Trim()))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL": mods |= Win32.MOD_CONTROL; break;
                case "ALT":     mods |= Win32.MOD_ALT;     break;
                case "SHIFT":   mods |= Win32.MOD_SHIFT;   break;
                case "WIN":     mods |= Win32.MOD_WIN;      break;
                default:        vk = ResolveVirtualKey(part); break;
            }
        }

        if (vk == 0)
            throw new ArgumentException(
                $"Unrecognised key in hotkey '{hotkey}'. " +
                "Expected a letter (A–Z), digit (0–9), or function key (F1–F24).");

        return (mods, vk);
    }

    private static uint ResolveVirtualKey(string key)
    {
        // Function keys F1–F24
        if (key.Length >= 2 && char.ToUpperInvariant(key[0]) == 'F' &&
            int.TryParse(key[1..], out int fn) && fn >= 1 && fn <= 24)
            return (uint)(0x6F + fn); // VK_F1 = 0x70

        // Single character (digit or letter)
        if (key.Length == 1)
        {
            char c = char.ToUpperInvariant(key[0]);
            if (c >= '0' && c <= '9') return c; // VK_0–VK_9  (0x30–0x39)
            if (c >= 'A' && c <= 'Z') return c; // VK_A–VK_Z  (0x41–0x5A)
        }

        // Named keys
        return key.ToUpperInvariant() switch
        {
            "SPACE"              => 0x20,
            "ENTER" or "RETURN"  => 0x0D,
            "TAB"                => 0x09,
            "ESC"   or "ESCAPE"  => 0x1B,
            "HOME"               => 0x24,
            "END"                => 0x23,
            "PGUP"  or "PAGEUP"  => 0x21,
            "PGDN"  or "PAGEDOWN"=> 0x22,
            "INSERT"             => 0x2D,
            "DELETE"             => 0x2E,
            "LEFT"               => 0x25,
            "UP"                 => 0x26,
            "RIGHT"              => 0x27,
            "DOWN"               => 0x28,
            _                    => 0,
        };
    }

    // ── Message loop ──────────────────────────────────────────────────────────

    /// <summary>
    /// Register all hotkeys and enter the Win32 message loop on the calling thread.
    /// Blocks until <see cref="Stop"/> posts WM_QUIT or the loop returns an error.
    /// </summary>
    /// <param name="registrations">Hotkeys to register.</param>
    /// <param name="log">Optional writer for diagnostic messages.</param>
    public void Run(IEnumerable<HotkeyRegistration> registrations, TextWriter? log = null)
    {
        _ownerThreadId = Win32.GetCurrentThreadId();
        _running = true;

        var registered = new List<int>();

        foreach (var reg in registrations)
        {
            try
            {
                var (mods, vk) = ParseHotkey(reg.Hotkey);
                if (Win32.RegisterHotKey(IntPtr.Zero, reg.Id, mods, vk))
                {
                    _idToProject[reg.Id] = reg.ProjectName;
                    registered.Add(reg.Id);
                    log?.WriteLine($"  Registered: {reg.Hotkey} → {reg.ProjectName}");
                }
                else
                {
                    int err = Marshal.GetLastWin32Error();
                    log?.WriteLine(
                        $"  [WARN] Could not register '{reg.Hotkey}' for '{reg.ProjectName}': " +
                        $"Win32 error {err} (hotkey may already be claimed by another app)");
                }
            }
            catch (Exception ex)
            {
                log?.WriteLine($"  [WARN] Skipping '{reg.Hotkey}' for '{reg.ProjectName}': {ex.Message}");
            }
        }

        if (registered.Count == 0)
        {
            log?.WriteLine("No hotkeys successfully registered — exiting.");
            _running = false;
            return;
        }

        // Win32 message loop.
        // GetMessage returns 0 when it dequeues WM_QUIT; -1 on error.
        int result;
        while (_running && (result = Win32.GetMessage(out Win32.MSG msg, IntPtr.Zero, 0, 0)) != 0)
        {
            if (result == -1) break; // GetMessage error

            if (msg.message == Win32.WM_HOTKEY &&
                _idToProject.TryGetValue((int)msg.wParam, out string? proj))
            {
                try { HotkeyPressed?.Invoke(proj); }
                catch (Exception ex) { log?.WriteLine($"  [ERROR] HotkeyPressed handler threw: {ex.Message}"); }
            }

            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessage(ref msg);
        }

        foreach (int id in registered)
            Win32.UnregisterHotKey(IntPtr.Zero, id);

        _running = false;
    }

    /// <summary>
    /// Post WM_QUIT to the message-loop thread, causing <see cref="Run"/> to return.
    /// Safe to call from any thread, including Ctrl+C signal handlers.
    /// </summary>
    public void Stop()
    {
        _running = false;
        if (_ownerThreadId != 0)
            Win32.PostThreadMessage(_ownerThreadId, Win32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
    }

    public void Dispose() => Stop();
}
