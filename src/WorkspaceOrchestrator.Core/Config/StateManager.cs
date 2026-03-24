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

    // ─── Snapshot persistence ─────────────────────────────────────────────────

    private string GetSnapshotPath(string projectName)
        => Path.Combine(Path.GetDirectoryName(_statePath)!, "snapshots", $"{projectName}.json");

    /// <summary>Persist a window snapshot to ~/.workspaces/snapshots/&lt;project&gt;.json.</summary>
    public void SaveSnapshot(WindowSnapshot snapshot)
    {
        string path = GetSnapshotPath(snapshot.Project);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, JsonOptions));
    }

    /// <summary>Load a project's most recent snapshot, or null if none exists.</summary>
    public WindowSnapshot? LoadSnapshot(string projectName)
    {
        string path = GetSnapshotPath(projectName);
        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<WindowSnapshot>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Delete the snapshot file for a project (no-op if none exists).</summary>
    public void DeleteSnapshot(string projectName)
    {
        string path = GetSnapshotPath(projectName);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>Return all saved snapshots across all projects.</summary>
    public IReadOnlyList<WindowSnapshot> GetAllSnapshots()
    {
        string dir = Path.Combine(Path.GetDirectoryName(_statePath)!, "snapshots");
        if (!Directory.Exists(dir)) return [];

        var result = new List<WindowSnapshot>();
        foreach (string file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                string json = File.ReadAllText(file);
                var snap = JsonSerializer.Deserialize<WindowSnapshot>(json, JsonOptions);
                if (snap is not null) result.Add(snap);
            }
            catch { /* skip corrupt snapshot files */ }
        }
        return result;
    }
}
