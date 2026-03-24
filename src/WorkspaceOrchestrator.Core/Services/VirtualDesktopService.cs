using System.Diagnostics;

namespace WorkspaceOrchestrator.Core.Services;

/// <summary>
/// Virtual desktop operations via a PowerShell bridge script.
///
/// The MScholtes VirtualDesktop module (v1.5.11, already installed) is called through
/// pwsh.exe as a subprocess. This avoids replicating undocumented COM interface GUIDs
/// that change between Windows builds. Phase 3 can replace with direct COM interop.
/// </summary>
public class VirtualDesktopService
{
    private readonly string _bridgeScript;

    public VirtualDesktopService(string? bridgeScriptPath = null)
    {
        _bridgeScript = bridgeScriptPath
            ?? FindBridgeScript()
            ?? throw new FileNotFoundException("VirtualDesktopBridge.ps1 not found. Expected in lib/ directory.");
    }

    /// <summary>Ensure at least <paramref name="count"/> virtual desktops exist.</summary>
    public void EnsureDesktopCount(int count)
    {
        RunBridge($"-Op EnsureDesktopCount -Count {count}");
    }

    /// <summary>Move a window to the given 0-based desktop index.</summary>
    public void MoveWindowToDesktop(nint hwnd, int desktopIndex)
    {
        RunBridge($"-Op MoveWindowToDesktop -Hwnd {(long)hwnd} -DesktopIndex {desktopIndex}");
    }

    /// <summary>Switch the active virtual desktop to the given 0-based index.</summary>
    public void SwitchToDesktop(int desktopIndex)
    {
        RunBridge($"-Op SwitchToDesktop -DesktopIndex {desktopIndex}");
    }

    /// <summary>Get the 0-based index of the desktop a window is on, or -1 if not found.</summary>
    public int GetWindowDesktopIndex(nint hwnd)
    {
        string output = RunBridge($"-Op GetWindowDesktopIndex -Hwnd {(long)hwnd}", captureOutput: true);
        return int.TryParse(output.Trim(), out int idx) ? idx : -1;
    }

    /// <summary>Get the current number of virtual desktops.</summary>
    public int GetDesktopCount()
    {
        string output = RunBridge("-Op GetDesktopCount", captureOutput: true);
        return int.TryParse(output.Trim(), out int count) ? count : 1;
    }

    private string RunBridge(string args, bool captureOutput = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = @"C:\Program Files\PowerShell\7\pwsh.exe",
            Arguments              = $"-NonInteractive -NoProfile -File \"{_bridgeScript}\" {args}",
            UseShellExecute        = false,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError  = false,
            CreateNoWindow         = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start pwsh.exe");

        string output = captureOutput ? proc.StandardOutput.ReadToEnd() : string.Empty;
        proc.WaitForExit();

        if (proc.ExitCode != 0 && !captureOutput)
        {
            // Non-fatal: log but don't throw (virtual desktop ops shouldn't crash deploy)
            Console.Error.WriteLine($"[VirtualDesktopService] Bridge exited with code {proc.ExitCode} for args: {args}");
        }

        return output;
    }

    private static string? FindBridgeScript()
    {
        // Look relative to this assembly's directory (works for both dev and published builds)
        var assemblyDir = AppContext.BaseDirectory;

        // Build candidates: walk up from assembly dir looking for lib/VirtualDesktopBridge.ps1
        var candidates = new List<string>
        {
            Path.Combine(assemblyDir, "lib", "VirtualDesktopBridge.ps1"),
        };

        // Walk up to 7 parent directories to handle various bin/Debug/net8.0-windows depth
        var dir = new DirectoryInfo(assemblyDir);
        for (int i = 0; i < 7 && dir != null; i++)
        {
            dir = dir.Parent;
            if (dir is not null)
                candidates.Add(Path.Combine(dir.FullName, "lib", "VirtualDesktopBridge.ps1"));
        }

        return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
    }
}
