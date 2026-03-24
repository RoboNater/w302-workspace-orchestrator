using System.CommandLine;
using Spectre.Console;
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
        projectArg.AddCompletions(ctx => loader.ListProjects().Select(p => p.Name));

        var cmd = new Command("stow", "Stow a project context: close all project windows")
        {
            projectArg
        };

        cmd.SetHandler((project, dryRun, verbose) =>
        {
            try
            {
                if (project is null)
                {
                    AnsiConsole.MarkupLine("[yellow]No project specified.[/] Use 'ws status' to see deployed projects.");
                    return;
                }

                string name = project.Replace(".workspace.yaml", "");

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[cyan]Stowing:[/] [bold]{name}[/]");
                AnsiConsole.WriteLine();

                if (dryRun)
                {
                    AnsiConsole.MarkupLine("[yellow][[DRY RUN]][/] No changes will be made.");
                    int dryCount = stowService.Stow(name, Console.Out, dryRun: true);
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[yellow]Dry run complete:[/] {name} — no changes made");
                }
                else
                {
                    AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .SpinnerStyle(Style.Parse("cyan"))
                        .Start($"Stowing {name}...", ctx =>
                        {
                            stowService.Stow(name, new SpinnerWriter(ctx), dryRun: false);
                        });

                    AnsiConsole.MarkupLine($"[green]✓ Stow complete:[/] [bold]{name}[/]");
                }

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
