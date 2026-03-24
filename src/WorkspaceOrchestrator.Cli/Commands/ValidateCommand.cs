using System.CommandLine;
using WorkspaceOrchestrator.Core.Config;
using WorkspaceOrchestrator.Core.Models;

namespace WorkspaceOrchestrator.Cli.Commands;

public static class ValidateCommand
{
    public static Command Build(ConfigLoader loader)
    {
        var projectArg = new Argument<string>("project", "Project name to validate");
        var cmd = new Command("validate", "Validate a project config file")
        {
            projectArg
        };

        cmd.SetHandler((project) =>
        {
            string name = project.Replace(".workspace.yaml", "");
            bool valid = true;

            Console.WriteLine();
            Console.WriteLine($"Validating: {name}");
            Console.WriteLine();

            // Find config
            string? path = loader.FindConfigPath(name);
            if (path is null)
            {
                Fail($"Config file not found: '{name}.workspace.yaml'");
                Console.WriteLine("  Searched:");
                foreach (var dir in ConfigLoader.GetSearchDirectories())
                    Console.WriteLine($"    {dir}");
                Environment.Exit(1);
                return;
            }

            Info($"Config file: {path}");

            // Parse
            ProjectContext config;
            try
            {
                config = loader.LoadConfig(path);
                Ok("YAML parsed successfully");
            }
            catch (Exception ex)
            {
                Fail($"YAML parse error: {ex.Message}");
                Environment.Exit(1);
                return;
            }

            // Check meta
            if (string.IsNullOrWhiteSpace(config.Meta.Name))
            {
                Warn("meta.name is empty");
                valid = false;
            }
            else
            {
                Ok($"meta.name: {config.Meta.Name}");
            }

            // Check apps
            if (config.Applications.Count == 0)
            {
                Warn("No applications defined");
            }

            foreach (var (key, app) in config.Applications)
            {
                Console.WriteLine();
                Console.WriteLine($"  App: {key} (type: {app.Type})");

                switch (app.Type.ToLowerInvariant())
                {
                    case "vscode":
                        if (!string.IsNullOrEmpty(app.Workspace) && !Directory.Exists(app.Workspace.Replace('/', '\\')))
                            Warn($"    workspace path not found: {app.Workspace}");
                        else if (!string.IsNullOrEmpty(app.Workspace))
                            Ok($"    workspace: {app.Workspace}");
                        break;

                    case "windows-terminal":
                    case "terminal":
                        if (app.Tabs.Count == 0)
                            Warn("    no tabs defined");
                        foreach (var tab in app.Tabs)
                        {
                            if (!Directory.Exists(tab.Directory.Replace('/', '\\')))
                                Warn($"    tab '{tab.Title}' directory not found: {tab.Directory}");
                            else
                                Ok($"    tab '{tab.Title}': {tab.Directory}");
                        }
                        break;

                    case "file-explorer":
                    case "explorer":
                        foreach (var p in app.Paths)
                        {
                            if (!Directory.Exists(p.Replace('/', '\\')))
                                Warn($"    path not found: {p}");
                            else
                                Ok($"    path: {p}");
                        }
                        break;
                }

                if (app.Window?.Position is not null)
                    Ok($"    position: ({app.Window.Position.X},{app.Window.Position.Y}) {app.Window.Position.Width}x{app.Window.Position.Height}");
            }

            Console.WriteLine();
            if (valid)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Validation passed.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Validation completed with warnings.");
            }
            Console.ResetColor();
            Console.WriteLine();
        }, projectArg);

        return cmd;
    }

    private static void Ok(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  ✓ ");
        Console.ResetColor();
        Console.WriteLine(msg);
    }

    private static void Warn(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("  ⚠ ");
        Console.ResetColor();
        Console.WriteLine(msg);
    }

    private static void Fail(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("  ✗ ");
        Console.ResetColor();
        Console.WriteLine(msg);
    }

    private static void Info(string msg)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {msg}");
        Console.ResetColor();
    }
}
