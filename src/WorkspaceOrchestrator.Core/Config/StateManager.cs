using System.Text.Json;
using WorkspaceOrchestrator.Core.Models;

namespace WorkspaceOrchestrator.Core.Config;

/// <summary>
/// Reads and writes the deploy journal at <c>~/.workspaces/state.json</c>.
/// The journal tracks which projects are currently deployed so stow can clean up reliably.
/// </summary>
public class StateManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _statePath;

    public StateManager(string? statePath = null)
    {
        _statePath = statePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".workspaces",
            "state.json");
    }

    public string StatePath => _statePath;

    /// <summary>Load current state from disk (returns empty state if file doesn't exist).</summary>
    public DeployState Load()
    {
        if (!File.Exists(_statePath))
            return new DeployState();

        try
        {
            string json = File.ReadAllText(_statePath);
            return JsonSerializer.Deserialize<DeployState>(json, JsonOptions) ?? new DeployState();
        }
        catch
        {
            // Corrupt state file — start fresh rather than crash
            return new DeployState();
        }
    }

    /// <summary>Persist state to disk, creating the directory if needed.</summary>
    public void Save(DeployState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
        string json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(_statePath, json);
    }

    /// <summary>Record a project as deployed.</summary>
    public void RecordDeploy(DeployedProject project)
    {
        var state = Load();
        // Remove any existing entry for this project (idempotent)
        state.DeployedProjects.RemoveAll(p =>
            string.Equals(p.Project, project.Project, StringComparison.OrdinalIgnoreCase));
        state.DeployedProjects.Add(project);
        Save(state);
    }

    /// <summary>Remove a project's deploy record (called after successful stow).</summary>
    public void RecordStow(string projectName)
    {
        var state = Load();
        state.DeployedProjects.RemoveAll(p =>
            string.Equals(p.Project, projectName, StringComparison.OrdinalIgnoreCase));
        Save(state);
    }

    /// <summary>Get the deploy record for a project, or null if not deployed.</summary>
    public DeployedProject? GetDeployedProject(string projectName)
    {
        var state = Load();
        return state.DeployedProjects.FirstOrDefault(p =>
            string.Equals(p.Project, projectName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Get all currently deployed projects.</summary>
    public IReadOnlyList<DeployedProject> GetAllDeployed()
        => Load().DeployedProjects;
}
