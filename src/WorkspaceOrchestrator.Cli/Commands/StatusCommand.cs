using System.CommandLine;
using Spectre.Console;
using WorkspaceOrchestrator.Core.Config;

namespace WorkspaceOrchestrator.Cli.Commands;

public static class StatusCommand
{
    public static Command Build(StateManager stateManager)
    {
        var cmd = new Command("status", "Show currently deployed projects");

        cmd.SetHandler(() =>
        {
            var deployed = stateManager.GetAllDeployed();

            AnsiConsole.WriteLine();
            if (deployed.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]No projects currently deployed.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[cyan]Deployed projects ({deployed.Count}):[/]");
                AnsiConsole.WriteLine();

                foreach (var p in deployed)
                {
                    AnsiConsole.MarkupLine(
                        $"  [green]●[/] [bold]{Markup.Escape(p.Project)}[/]  " +
                        $"[dim]deployed {FormatAge(p.DeployedAt)}[/]");

                    var appTable = new Table()
                        .HideHeaders()
                        .Border(TableBorder.None)
                        .AddColumn("")
                        .AddColumn("")
                        .AddColumn("");

                    foreach (var app in p.Apps)
                    {
                        appTable.AddRow(
                            $"    [dim]{Markup.Escape(app.Type)}[/]",
                            $"[dim]{Markup.Escape(app.ProcessName)}[/]",
                            $"[dim]{Markup.Escape(app.TitlePattern)}[/]");
                    }

                    AnsiConsole.Write(appTable);
                    AnsiConsole.WriteLine();
                }
            }

            AnsiConsole.WriteLine();
        });

        return cmd;
    }

    private static string FormatAge(DateTimeOffset deployed)
    {
        var age = DateTimeOffset.UtcNow - deployed;
        if (age.TotalSeconds < 60)  return "just now";
        if (age.TotalMinutes < 60)  return $"{(int)age.TotalMinutes}m ago";
        if (age.TotalHours < 24)    return $"{(int)age.TotalHours}h ago";
        return $"{(int)age.TotalDays}d ago";
    }
}
