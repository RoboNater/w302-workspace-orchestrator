using System.CommandLine;
using System.Text;
using Spectre.Console;
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
        projectArg.AddCompletions(ctx => loader.ListProjects().Select(p => p.Name));

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
                    AnsiConsole.MarkupLine($"[red]Error:[/] No config found for project '[bold]{project}[/]'.");
                    AnsiConsole.MarkupLine($"  Searched: {string.Join(", ", ConfigLoader.GetSearchDirectories())}");
                    Environment.Exit(1);
                    return;
                }

                var config = loader.LoadConfig(configPath);

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[cyan]Deploying:[/] [bold]{config.Meta.Name}[/]");
                if (verbose) AnsiConsole.MarkupLine($"[dim]Config:   {configPath}[/]");
                AnsiConsole.WriteLine();

                if (dryRun)
                    AnsiConsole.MarkupLine("[yellow][[DRY RUN]][/] No changes will be made.");

                // Route step messages through a spinner (non-dry-run) or stdout (dry-run)
                if (!dryRun)
                {
                    AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .SpinnerStyle(Style.Parse("cyan"))
                        .Start($"Deploying {config.Meta.Name}...", ctx =>
                        {
                            var writer = new SpinnerWriter(ctx);
                            deployService.Deploy(config, configPath, writer, dryRun: false);
                        });
                }
                else
                {
                    deployService.Deploy(config, configPath, Console.Out, dryRun: true);
                }

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(dryRun
                    ? $"[yellow]Dry run complete:[/] {config.Meta.Name} — no changes made"
                    : $"[green]✓ Deploy complete:[/] [bold]{config.Meta.Name}[/]");
                AnsiConsole.WriteLine();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
                if (verbose) AnsiConsole.WriteException(ex);
                Environment.Exit(1);
            }
        }, projectArg, dryRunOption, verboseOption);

        return cmd;
    }
}
