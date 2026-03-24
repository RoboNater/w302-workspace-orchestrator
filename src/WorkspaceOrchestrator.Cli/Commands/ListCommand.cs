using System.CommandLine;
using Spectre.Console;
using WorkspaceOrchestrator.Core.Config;

namespace WorkspaceOrchestrator.Cli.Commands;

public static class ListCommand
{
    public static Command Build(ConfigLoader loader)
    {
        var cmd = new Command("list", "List all available project configs");

        cmd.SetHandler(() =>
        {
            var projects = loader.ListProjects();

            AnsiConsole.WriteLine();
            if (projects.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No project configs found.[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("Create a [bold].workspace.yaml[/] file in one of:");
                foreach (var dir in ConfigLoader.GetSearchDirectories())
                    AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(dir)}[/]");
            }
            else
            {
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .BorderColor(Color.Grey)
                    .AddColumn(new TableColumn("[cyan]Project[/]"))
                    .AddColumn(new TableColumn("[dim]Config Path[/]"));

                foreach (var (name, path) in projects)
                    table.AddRow($"[bold]{Markup.Escape(name)}[/]", $"[dim]{Markup.Escape(path)}[/]");

                AnsiConsole.Write(table);
                AnsiConsole.MarkupLine($"[dim]{projects.Count} project(s) found.[/]");
            }

            AnsiConsole.WriteLine();
        });

        return cmd;
    }
}
