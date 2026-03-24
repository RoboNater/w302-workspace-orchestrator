namespace WorkspaceOrchestrator.Core.Models;

/// <summary>
/// Snapshot of a visible top-level window at enumeration time.
/// </summary>
public record WindowInfo(
    nint   Hwnd,
    string Title,
    uint   ProcessId,
    string ProcessName,
    int    X,
    int    Y,
    int    Width,
    int    Height
);
