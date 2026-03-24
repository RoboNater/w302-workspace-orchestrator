using System.CommandLine;
using Spectre.Console;
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
        projectArg.AddCompletions(ctx => loader.ListProjects().Select(p => p.Name));

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

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[cyan]Switching to:[/] [bold]{name}[/]");
                AnsiConsole.WriteLine();

                // Stow all currently deployed projects
                var deployed = stateManager.GetAllDeployed();
                if (deployed.Count > 0)
                {
                    foreach (var d in deployed)
                    {
                        if (string.Equals(d.Project, name, StringComparison.OrdinalIgnoreCase))
                        {
                            AnsiConsole.MarkupLine($"  [dim]'{name}' is already deployed — will redeploy.[/]");
                            continue;
                        }

                        AnsiConsole.MarkupLine($"  [dim]Stowing:[/] {d.Project}");

                        if (!dryRun)
                        {
                            AnsiConsole.Status()
                                .Spinner(Spinner.Known.Dots)
                                .SpinnerStyle(Style.Parse("dim"))
                                .Start($"Stowing {d.Project}...", ctx =>
                                {
                                    stowService.Stow(d.Project, new SpinnerWriter(ctx), dryRun: false);
                                });
                        }
                        else
                        {
                            stowService.Stow(d.Project, Console.Out, dryRun: true);
                        }
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("  [dim]No projects currently deployed.[/]");
                }

                AnsiConsole.WriteLine();

                // Deploy target
                string? configPath = loader.FindConfigPath(name);
                if (configPath is null)
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] No config found for project '[bold]{name}[/]'.");
                    Environment.Exit(1);
                    return;
                }

                var config = loader.LoadConfig(configPath);
                AnsiConsole.MarkupLine($"  [white]Deploying:[/] {config.Meta.Name}");

                if (!dryRun)
                {
                    AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .SpinnerStyle(Style.Parse("cyan"))
                        .Start($"Deploying {config.Meta.Name}...", ctx =>
                        {
                            deployService.Deploy(config, configPath, new SpinnerWriter(ctx), dryRun: false);
                        });
                }
                else
                {
                    deployService.Deploy(config, configPath, Console.Out, dryRun: true);
                }

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(dryRun
                    ? $"[yellow]Dry run complete:[/] Switch to '{name}' — no changes made"
                    : $"[green]✓ Switched to:[/] [bold]{name}[/]");
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
