using WorkspaceOrchestrator.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WorkspaceOrchestrator.Core.Config;

/// <summary>
/// Finds and deserializes .workspace.yaml project config files.
/// </summary>
public class ConfigLoader
{
    /// <summary>
    /// Config search locations in priority order.
    /// </summary>
    public static IEnumerable<string> GetSearchDirectories()
    {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".workspaces");
        yield return Directory.GetCurrentDirectory();
        yield return AppContext.BaseDirectory;
    }

    /// <summary>
    /// Find the path to a project's .workspace.yaml file, or null if not found.
    /// </summary>
    public string? FindConfigPath(string projectName)
    {
        foreach (var dir in GetSearchDirectories())
        {
            var path = Path.Combine(dir, $"{projectName}.workspace.yaml");
            if (File.Exists(path)) return path;
        }
        return null;
    }

    /// <summary>
    /// Load and parse a project config from the given file path.
    /// </summary>
    public ProjectContext LoadConfig(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Config file not found: {filePath}");

        string yaml = File.ReadAllText(filePath);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<ProjectContext>(yaml);
    }

    /// <summary>
    /// Find and load a project config by name.
    /// Throws if not found.
    /// </summary>
    public ProjectContext LoadProject(string projectName)
    {
        string? path = FindConfigPath(projectName);
        if (path is null)
            throw new FileNotFoundException(
                $"No config found for project '{projectName}'. " +
                $"Expected '{projectName}.workspace.yaml' in: {string.Join(", ", GetSearchDirectories())}");

        return LoadConfig(path);
    }

    /// <summary>
    /// List all project configs available across all search directories.
    /// Returns (name, path) pairs, deduplicated by name (first found wins).
    /// </summary>
    public IReadOnlyList<(string Name, string Path)> ListProjects()
    {
        var seen  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(string, string)>();

        foreach (var dir in GetSearchDirectories())
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.GetFiles(dir, "*.workspace.yaml"))
            {
                string name = Path.GetFileName(file).Replace(".workspace.yaml", "");
                if (seen.Add(name))
                    result.Add((name, file));
            }
        }

        return result;
    }
}
