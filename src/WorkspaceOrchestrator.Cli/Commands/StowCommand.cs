using System.CommandLine;
using WorkspaceOrchestrator.Core.Config;
using WorkspaceOrchestrator.Core.Services;

namespace WorkspaceOrchestrator.Cli.Commands;

public static class StowCommand
{
    public static Command Build(
        ConfigLoader loader,
        StowService stowService,
        Option<bool> dryRunOption,
        Option<bool> verboseOption)
    {
        var projectArg = new Argument<string?>("project", () => null,
            "Project name to stow (omit to stow all deployed projects)");

        var cmd = new Command("stow", "Stow a project context: close all project windows")
        {
            projectArg
        };

        cmd.SetHandler((project, dryRun, verbose) =>
        {
            try
            {
                // Resolve project name
                string? name = project;
                if (name is null)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("No project specified. Use 'ws status' to see deployed projects.");
                    Console.ResetColor();
                    return;
                }

                // Accept project name with or without .workspace.yaml extension
                name = name.Replace(".workspace.yaml", "");

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine();
                Console.WriteLine($"Stowing: {name}");
                Console.ResetColor();
                Console.WriteLine();

                if (dryRun)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[DRY RUN] No changes will be made.");
                    Console.ResetColor();
                }

                int closed = stowService.Stow(name, Console.Out, dryRun);

                Console.WriteLine();
                Console.ForegroundColor = dryRun ? ConsoleColor.Yellow : ConsoleColor.Green;
                Console.WriteLine(dryRun
                    ? $"[Dry run complete] {name} — no changes made"
                    : $"Stow complete: {name} ({closed} window(s) closed)");
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
