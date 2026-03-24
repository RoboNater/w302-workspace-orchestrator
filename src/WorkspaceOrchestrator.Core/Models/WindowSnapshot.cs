using System.Text.Json.Serialization;

namespace WorkspaceOrchestrator.Core.Models;

/// <summary>
/// Captured window positions for all apps in a deployed project.
/// Persisted to ~/.workspaces/snapshots/&lt;project&gt;.json on stow.
/// Used by DeployService to restore the last-known layout on next deploy.
/// </summary>
public class WindowSnapshot
{
    [JsonPropertyName("project")]
    public string Project { get; set; } = string.Empty;

    [JsonPropertyName("capturedAt")]
    public DateTimeOffset CapturedAt { get; set; }

    [JsonPropertyName("apps")]
    public List<AppSnapshot> Apps { get; set; } = new();
}

/// <summary>
/// Captured position of one app window at snapshot time.
/// </summary>
public class AppSnapshot
{
    /// <summary>App type: "terminal", "vscode", "explorer", "chrome", "edge".</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("processName")]
    public string ProcessName { get; set; } = string.Empty;

    [JsonPropertyName("titlePattern")]
    public string TitlePattern { get; set; } = string.Empty;

    /// <summary>HWND at snapshot time (may be stale on next deploy — informational only).</summary>
    [JsonPropertyName("hwnd")]
    public long Hwnd { get; set; }

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}
