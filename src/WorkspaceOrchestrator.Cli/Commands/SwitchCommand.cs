using System.CommandLine;
using WorkspaceOrchestrator.Core.Config;
using WorkspaceOrchestrator.Core.Services;

namespace WorkspaceOrchestrator.Cli.Commands;

public static class SwitchCommand
{
    public static Command Build(
        ConfigLoader loader,
        DeployService deployService,
        StowService stowService,
        StateManager stateManager,
        Option<bool> dryRunOption,
        Option<bool> verboseOption)
    {
        var projectArg = new Argument<string>("project", "Project to switch to");

        var cmd = new Command("switch",
            "Atomic context switch: stow the current project, then deploy the target project")
        {
            projectArg
        };

        cmd.SetHandler((project, dryRun, verbose) =>
        {
            try
            {
                string name = project.Replace(".workspace.yaml", "");

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine();
                Console.WriteLine($"Switching to: {name}");
                Console.ResetColor();
                Console.WriteLine();

                // Stow all currently deployed projects
                var deployed = stateManager.GetAllDeployed();
                if (deployed.Count > 0)
                {
                    foreach (var d in deployed)
                    {
                        if (string.Equals(d.Project, name, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"  '{name}' is already deployed — will redeploy.");
                            continue;
                        }

                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"  Stowing: {d.Project}");
                        Console.ResetColor();
                        stowService.Stow(d.Project, Console.Out, dryRun);
                    }
                }
                else
                {
                    Console.WriteLine("  No projects currently deployed.");
                }

                Console.WriteLine();

                // Deploy target
                string? configPath = loader.FindConfigPath(name);
                if (configPath is null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"Error: No config found for project '{name}'.");
                    Console.ResetColor();
                    Environment.Exit(1);
                    return;
                }

                var config = loader.LoadConfig(configPath);
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"  Deploying: {config.Meta.Name}");
                Console.ResetColor();
                deployService.Deploy(config, configPath, Console.Out, dryRun);

                Console.WriteLine();
                Console.ForegroundColor = dryRun ? ConsoleColor.Yellow : ConsoleColor.Green;
                Console.WriteLine(dryRun
                    ? $"[Dry run complete] Switch to '{name}' — no changes made"
                    : $"Switched to: {name}");
                Console.ResetColor();
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                if (verbose) Console.Error.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }, projectArg, dryRunOption, verboseOption);

        return cmd;
    }
}
