using System.CommandLine;
using System.Diagnostics;
using Spectre.Console;
using WorkspaceOrchestrator.Core.Config;
using WorkspaceOrchestrator.Core.Services;

namespace WorkspaceOrchestrator.Cli.Commands;

/// <summary>
/// ws hotkeys list | start | stop
///
/// list  — shows all hotkeys defined across project configs
/// start — registers hotkeys and blocks in a Win32 message loop; writes PID file so stop works
/// stop  — reads PID file and terminates the daemon process
/// </summary>
public static class HotkeysCommand
{
    private static readonly string PidFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".workspaces", "hotkeys.pid");

    public static Command Build(
        ConfigLoader loader,
        DeployService deployService,
        StowService stowService,
        StateManager stateManager)
    {
        var cmd = new Command("hotkeys", "Manage global hotkeys for instant project switching");
        cmd.AddCommand(BuildListCommand(loader));
        cmd.AddCommand(BuildStartCommand(loader, deployService, stowService, stateManager));
        cmd.AddCommand(BuildStopCommand());
        return cmd;
    }

    // ── ws hotkeys list ───────────────────────────────────────────────────────

    static Command BuildListCommand(ConfigLoader loader)
    {
        var cmd = new Command("list", "Show hotkeys configured across all project configs");
        cmd.SetHandler(() =>
        {
            var projects = loader.ListProjects();
            var table = new Table().RoundedBorder();
            table.AddColumn("[bold]Project[/]");
            table.AddColumn("[bold]Hotkey[/]");

            bool any = false;
            foreach (var (name, path) in projects)
            {
                try
                {
                    var ctx = loader.LoadConfig(path);
                    if (!string.IsNullOrWhiteSpace(ctx.Hotkey))
                    {
                        // Validate the hotkey string and report parse errors
                        string hotkeyDisplay;
                        try
                        {
                            HotkeyService.ParseHotkey(ctx.Hotkey);
                            hotkeyDisplay = ctx.Hotkey;
                        }
                        catch (ArgumentException ex)
                        {
                            hotkeyDisplay = $"[red]{Markup.Escape(ctx.Hotkey)}[/] [dim](invalid: {Markup.Escape(ex.Message)})[/]";
                        }

                        table.AddRow(name, hotkeyDisplay);
                        any = true;
                    }
                }
                catch { /* skip unreadable configs */ }
            }

            AnsiConsole.WriteLine();
            if (any)
            {
                AnsiConsole.MarkupLine("[bold]Configured Hotkeys[/]");
                AnsiConsole.Write(table);
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]No hotkeys configured.[/]");
                AnsiConsole.MarkupLine("  Add [italic]hotkey: \"Ctrl+Alt+1\"[/] to a [italic].workspace.yaml[/] file, then run [italic]ws hotkeys start[/].");
            }
            AnsiConsole.WriteLine();
        });
        return cmd;
    }

    // ── ws hotkeys start ──────────────────────────────────────────────────────

    static Command BuildStartCommand(
        ConfigLoader loader,
        DeployService deployService,
        StowService stowService,
        StateManager stateManager)
    {
        var cmd = new Command("start",
            "Start the global hotkey daemon (blocks; run in a dedicated terminal or background)");

        cmd.SetHandler(() =>
        {
            // Collect projects that have a hotkey
            var registrations = new List<HotkeyService.HotkeyRegistration>();
            int id = 1;
            foreach (var (name, path) in loader.ListProjects())
            {
                try
                {
                    var ctx = loader.LoadConfig(path);
                    if (!string.IsNullOrWhiteSpace(ctx.Hotkey))
                        registrations.Add(new(id++, name, ctx.Hotkey));
                }
                catch { /* skip unreadable configs */ }
            }

            if (registrations.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No hotkeys configured.[/]");
                AnsiConsole.MarkupLine("  Add [italic]hotkey: \"Ctrl+Alt+1\"[/] to a [italic].workspace.yaml[/] file, then try again.");
                return;
            }

            // Write PID file so 'ws hotkeys stop' can find this process
            WritePidFile();

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[cyan bold]Hotkey Daemon[/]");
            AnsiConsole.MarkupLine("[dim]─────────────────────────────────[/]");
            foreach (var r in registrations)
                AnsiConsole.MarkupLine($"  [cyan]{r.Hotkey,-20}[/]→ [bold]{r.ProjectName}[/]");
            AnsiConsole.MarkupLine("[dim]─────────────────────────────────[/]");
            AnsiConsole.MarkupLine("[dim]Press Ctrl+C or run 'ws hotkeys stop' to exit.[/]");
            AnsiConsole.WriteLine();

            using var svc = new HotkeyService();

            // When a hotkey fires: stow all other deployed projects then deploy the target
            svc.HotkeyPressed += projectName =>
            {
                AnsiConsole.MarkupLine($"[cyan]→ Hotkey:[/] switching to [bold]{projectName}[/]");

                try
                {
                    // Stow every project that isn't the target
                    var deployed = stateManager.GetAllDeployed();
                    foreach (var d in deployed)
                    {
                        if (!string.Equals(d.Project, projectName, StringComparison.OrdinalIgnoreCase))
                        {
                            AnsiConsole.MarkupLine($"  [dim]Stowing {d.Project}...[/]");
                            stowService.Stow(d.Project, Console.Out, dryRun: false);
                        }
                    }

                    // Deploy the target
                    string? configPath = loader.FindConfigPath(projectName);
                    if (configPath is null)
                    {
                        AnsiConsole.MarkupLine($"  [red]Config not found for '{Markup.Escape(projectName)}'[/]");
                        return;
                    }

                    var config = loader.LoadConfig(configPath);
                    deployService.Deploy(config, configPath, Console.Out, dryRun: false);
                    AnsiConsole.MarkupLine($"  [green]✓ Switched to {Markup.Escape(projectName)}[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"  [red]Error:[/] {Markup.Escape(ex.Message)}");
                }
            };

            // Ctrl+C → graceful shutdown via WM_QUIT
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;   // prevent abrupt process kill
                svc.Stop();
            };

            // Block on Win32 message loop until Stop() or process kill
            svc.Run(registrations, Console.Out);

            // Clean up PID file on normal exit
            try { File.Delete(PidFilePath); } catch { }
            AnsiConsole.MarkupLine("[dim]Hotkey daemon stopped.[/]");
        });

        return cmd;
    }

    // ── ws hotkeys stop ───────────────────────────────────────────────────────

    static Command BuildStopCommand()
    {
        var cmd = new Command("stop", "Stop a running hotkey daemon");
        cmd.SetHandler(() =>
        {
            if (!File.Exists(PidFilePath))
            {
                AnsiConsole.MarkupLine("[yellow]No hotkey daemon appears to be running.[/] (PID file not found)");
                return;
            }

            string pidText = File.ReadAllText(PidFilePath).Trim();
            if (!int.TryParse(pidText, out int pid))
            {
                AnsiConsole.MarkupLine($"[red]Invalid PID file content:[/] {Markup.Escape(pidText)}");
                return;
            }

            try
            {
                var proc = Process.GetProcessById(pid);
                proc.Kill();
                try { File.Delete(PidFilePath); } catch { }
                AnsiConsole.MarkupLine($"[green]✓ Hotkey daemon stopped[/] (PID {pid})");
            }
            catch (ArgumentException)
            {
                // Process already gone
                AnsiConsole.MarkupLine($"[yellow]Process {pid} is not running[/] — daemon may have already exited.");
                try { File.Delete(PidFilePath); } catch { }
            }
        });
        return cmd;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    static void WritePidFile()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PidFilePath)!);
            File.WriteAllText(PidFilePath, Environment.ProcessId.ToString());
        }
        catch { /* non-fatal; stop command will report missing PID */ }
    }
}
