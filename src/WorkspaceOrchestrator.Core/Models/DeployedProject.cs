using System.Text.Json.Serialization;

namespace WorkspaceOrchestrator.Core.Models;

/// <summary>
/// Deploy journal root — persisted to ~/.workspaces/state.json.
/// </summary>
public class DeployState
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("deployedProjects")]
    public List<DeployedProject> DeployedProjects { get; set; } = new();
}

/// <summary>
/// Record of a single deployed project, written at deploy time and removed at stow time.
/// </summary>
public class DeployedProject
{
    [JsonPropertyName("project")]
    public string Project { get; set; } = string.Empty;

    [JsonPropertyName("deployedAt")]
    public DateTimeOffset DeployedAt { get; set; }

    [JsonPropertyName("configPath")]
    public string ConfigPath { get; set; } = string.Empty;

    [JsonPropertyName("apps")]
    public List<DeployedApp> Apps { get; set; } = new();
}

/// <summary>
/// Record of one deployed application window, used by stow to find and close it.
/// </summary>
public class DeployedApp
{
    /// <summary>App type: "terminal", "vscode", "explorer", "browser".</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Regex pattern to match window title at stow time.
    /// More reliable than HWND which may be stale.
    /// </summary>
    [JsonPropertyName("titlePattern")]
    public string TitlePattern { get; set; } = string.Empty;

    /// <summary>
    /// Process name to scope the title search (e.g. "WindowsTerminal", "Code", "explorer").
    /// </summary>
    [JsonPropertyName("processName")]
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>Last-known HWND (0 if not captured or already closed).</summary>
    [JsonPropertyName("hwnd")]
    public long Hwnd { get; set; }
}
