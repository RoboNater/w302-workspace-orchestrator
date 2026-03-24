using YamlDotNet.Serialization;

namespace WorkspaceOrchestrator.Core.Models;

/// <summary>
/// Deserialization root for a .workspace.yaml config file.
/// </summary>
public class ProjectContext
{
    [YamlMember(Alias = "version")]
    public int Version { get; set; } = 1;

    [YamlMember(Alias = "meta")]
    public ProjectMeta Meta { get; set; } = new();

    [YamlMember(Alias = "virtual_desktops")]
    public VirtualDesktopConfig? VirtualDesktops { get; set; }

    [YamlMember(Alias = "hotkey")]
    public string? Hotkey { get; set; }

    [YamlMember(Alias = "applications")]
    public Dictionary<string, AppConfig> Applications { get; set; } = new();
}

public class ProjectMeta
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;
}

public class VirtualDesktopConfig
{
    /// <summary>1-based desktop number.</summary>
    [YamlMember(Alias = "primary")]
    public int Primary { get; set; } = 1;
}

public class AppConfig
{
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = string.Empty;

    // VS Code
    [YamlMember(Alias = "workspace")]
    public string? Workspace { get; set; }

    // Explorer
    [YamlMember(Alias = "paths")]
    public List<string> Paths { get; set; } = new();

    // Browser
    [YamlMember(Alias = "profile")]
    public string? Profile { get; set; }

    [YamlMember(Alias = "urls")]
    public List<string> Urls { get; set; } = new();

    [YamlMember(Alias = "window")]
    public WindowConfig? Window { get; set; }

    // Terminal
    [YamlMember(Alias = "tabs")]
    public List<TabConfig> Tabs { get; set; } = new();
}

public class WindowConfig
{
    [YamlMember(Alias = "monitor")]
    public int Monitor { get; set; }

    [YamlMember(Alias = "position")]
    public WindowPosition? Position { get; set; }
}

public class WindowPosition
{
    [YamlMember(Alias = "x")]
    public int X { get; set; }

    [YamlMember(Alias = "y")]
    public int Y { get; set; }

    [YamlMember(Alias = "width")]
    public int Width { get; set; }

    [YamlMember(Alias = "height")]
    public int Height { get; set; }
}

public class TabConfig
{
    [YamlMember(Alias = "title")]
    public string Title { get; set; } = string.Empty;

    [YamlMember(Alias = "directory")]
    public string Directory { get; set; } = string.Empty;

    [YamlMember(Alias = "shell")]
    public string Shell { get; set; } = "pwsh";

    [YamlMember(Alias = "run_on_deploy")]
    public string? RunOnDeploy { get; set; }
}
