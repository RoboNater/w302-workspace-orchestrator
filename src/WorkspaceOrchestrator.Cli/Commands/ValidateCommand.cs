using System.CommandLine;
using Spectre.Console;
using WorkspaceOrchestrator.Core.Config;
using WorkspaceOrchestrator.Core.Models;
using WorkspaceOrchestrator.Core.Services;

namespace WorkspaceOrchestrator.Cli.Commands;

public static class ValidateCommand
{
    public static Command Build(ConfigLoader loader)
    {
        var projectArg = new Argument<string>("project", "Project name to validate");
        projectArg.AddCompletions(ctx => loader.ListProjects().Select(p => p.Name));
        var cmd = new Command("validate", "Validate a project config file")
        {
            projectArg
        };

        cmd.SetHandler((project) =>
        {
            string name = project.Replace(".workspace.yaml", "");
            bool valid = true;

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[cyan]Validating:[/] [bold]{Markup.Escape(name)}[/]");
            AnsiConsole.WriteLine();

            // Find config
            string? path = loader.FindConfigPath(name);
            if (path is null)
            {
                Fail($"Config file not found: '{name}.workspace.yaml'");
                AnsiConsole.MarkupLine("  Searched:");
                foreach (var dir in ConfigLoader.GetSearchDirectories())
                    AnsiConsole.MarkupLine($"    [dim]{Markup.Escape(dir)}[/]");
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
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"  [bold]{Markup.Escape(key)}[/] [dim](type: {Markup.Escape(app.Type)})[/]");

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

                    case "chrome":
                    case "edge":
                    {
                        bool browserFound = app.Type.Equals("edge", StringComparison.OrdinalIgnoreCase)
                            ? BrowserLauncher.FindEdgeExe() is not null
                            : BrowserLauncher.FindChromeExe() is not null;

                        if (!browserFound)
                            Warn($"    {app.Type} executable not found on this system");
                        else
                            Ok($"    {app.Type} executable found");

                        if (string.IsNullOrWhiteSpace(app.Profile))
                            Warn("    no profile specified (will use default profile)");
                        else
                            Ok($"    profile: {app.Profile}");

                        if (app.Urls.Count == 0)
                            Warn("    no URLs specified");
                        else
                        {
                            foreach (var url in app.Urls)
                                Ok($"    url: {url}");
                        }
                        break;
                    }
                }

                if (app.Window?.Position is not null)
                    Ok($"    position: ({app.Window.Position.X},{app.Window.Position.Y}) {app.Window.Position.Width}x{app.Window.Position.Height}");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(valid
                ? "[green]✓ Validation passed.[/]"
                : "[yellow]⚠ Validation completed with warnings.[/]");
            AnsiConsole.WriteLine();
        }, projectArg);

        return cmd;
    }

    private static void Ok(string msg)   => AnsiConsole.MarkupLine($"  [green]✓[/] {Markup.Escape(msg)}");
    private static void Warn(string msg) => AnsiConsole.MarkupLine($"  [yellow]⚠[/] {Markup.Escape(msg)}");
    private static void Fail(string msg) => AnsiConsole.MarkupLine($"  [red]✗[/] {Markup.Escape(msg)}");
    private static void Info(string msg) => AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(msg)}[/]");
}
