using System.CommandLine;
using WorkspaceOrchestrator.Core.Config;
using WorkspaceOrchestrator.Core.Services;

namespace WorkspaceOrchestrator.Cli.Commands;

public static class DeployCommand
{
    public static Command Build(
        ConfigLoader loader,
        DeployService deployService,
        Option<bool> dryRunOption,
        Option<bool> verboseOption)
    {
        var projectArg = new Argument<string>("project", "Project name (matches <project>.workspace.yaml)");
        var cmd = new Command("deploy", "Deploy a project context: launch apps, position windows, assign to virtual desktop")
        {
            projectArg
        };

        cmd.SetHandler((project, dryRun, verbose) =>
        {
            try
            {
                string? configPath = loader.FindConfigPath(project);
                if (configPath is null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"Error: No config found for project '{project}'.");
                    Console.ResetColor();
                    Console.Error.WriteLine($"  Searched: {string.Join(", ", ConfigLoader.GetSearchDirectories())}");
                    Environment.Exit(1);
                    return;
                }

                var config = loader.LoadConfig(configPath);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine();
                Console.WriteLine($"Deploying: {config.Meta.Name}");
                Console.ResetColor();
                if (verbose) Console.WriteLine($"Config:    {configPath}");
                Console.WriteLine();

                if (dryRun)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[DRY RUN] No changes will be made.");
                    Console.ResetColor();
                }

                var record = deployService.Deploy(config, configPath, Console.Out, dryRun);

                Console.WriteLine();
                Console.ForegroundColor = dryRun ? ConsoleColor.Yellow : ConsoleColor.Green;
                Console.WriteLine(dryRun
                    ? $"[Dry run complete] {config.Meta.Name} — no changes made"
                    : $"Deploy complete: {config.Meta.Name}");
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
