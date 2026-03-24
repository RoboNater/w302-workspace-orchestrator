using System.CommandLine;
using System.Diagnostics;
using Spectre.Console;
using WorkspaceOrchestrator.Core.Config;

namespace WorkspaceOrchestrator.Cli.Commands;

public static class EditCommand
{
    // VS Code executable path
    private static readonly string VsCodeExe = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "Microsoft VS Code", "Code.exe");

    public static Command Build(ConfigLoader loader)
    {
        var projectArg = new Argument<string>("project", "Project name whose config to open in VS Code");
        projectArg.AddCompletions(ctx => loader.ListProjects().Select(p => p.Name));

        var cmd = new Command("edit", "Open a project config file in VS Code")
        {
            projectArg
        };

        cmd.SetHandler((project) =>
        {
            string name = project.Replace(".workspace.yaml", "");

            string? path = loader.FindConfigPath(name);
            if (path is null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] No config found for project '[bold]{name}[/]'.");
                AnsiConsole.MarkupLine($"  Searched: {string.Join(", ", ConfigLoader.GetSearchDirectories())}");
                Environment.Exit(1);
                return;
            }

            if (!File.Exists(VsCodeExe))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] VS Code not found at: {VsCodeExe}");
                AnsiConsole.MarkupLine("  Install VS Code or open the config manually:");
                AnsiConsole.MarkupLine($"  [dim]{path}[/]");
                Environment.Exit(1);
                return;
            }

            AnsiConsole.MarkupLine($"[cyan]Opening config:[/] {path}");

            var psi = new ProcessStartInfo
            {
                FileName        = VsCodeExe,
                Arguments       = $"\"{path}\"",
                UseShellExecute = true,
            };
            Process.Start(psi);

        }, projectArg);

        return cmd;
    }
}
