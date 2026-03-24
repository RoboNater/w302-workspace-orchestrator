using System.CommandLine;
using Spectre.Console;
using WorkspaceOrchestrator.Core.Config;
using WorkspaceOrchestrator.Core.Services;

namespace WorkspaceOrchestrator.Cli.Commands;

public static class SnapshotCommand
{
    public static Command Build(
        ConfigLoader    loader,
        StateManager    stateManager,
        SnapshotService snapshotService)
    {
        var projectArg = new Argument<string?>("project", () => null,
            "Project to snapshot (omit to snapshot all deployed projects)");
        projectArg.AddCompletions(ctx => loader.ListProjects().Select(p => p.Name));

        var listOption   = new Option<bool>("--list",   "List all saved snapshots");
        var deleteOption = new Option<string?>("--delete",
            "Delete the saved snapshot for a project");
        deleteOption.AddCompletions(ctx =>
            stateManager.GetAllSnapshots().Select(s => s.Project));

        var cmd = new Command("snapshot",
            "Capture or view saved window-position snapshots")
        {
            projectArg,
            listOption,
            deleteOption,
        };

        cmd.SetHandler((project, list, delete) =>
        {
            // ── --list ────────────────────────────────────────────────────────
            if (list)
            {
                ShowSnapshotList(stateManager);
                return;
            }

            // ── --delete ──────────────────────────────────────────────────────
            if (delete is not null)
            {
                string target = delete.Replace(".workspace.yaml", "");
                stateManager.DeleteSnapshot(target);
                AnsiConsole.MarkupLine($"[green]✓[/] Deleted snapshot for [bold]{Markup.Escape(target)}[/].");
                return;
            }

            // ── capture ───────────────────────────────────────────────────────
            if (project is not null)
            {
                string name = project.Replace(".workspace.yaml", "");
                CaptureOne(name, stateManager, snapshotService);
            }
            else
            {
                // Snapshot all deployed projects
                var deployed = stateManager.GetAllDeployed();
                if (deployed.Count == 0)
                {
                    AnsiConsole.MarkupLine(
                        "[yellow]No projects currently deployed.[/] Deploy a project first, or specify a project name.");
                    return;
                }

                foreach (var p in deployed)
                    CaptureOne(p.Project, stateManager, snapshotService);
            }

        }, projectArg, listOption, deleteOption);

        return cmd;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static void CaptureOne(string projectName, StateManager stateManager,
        SnapshotService snapshotService)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[cyan]Snapshotting:[/] [bold]{Markup.Escape(projectName)}[/]");
        AnsiConsole.WriteLine();

        var writer = new System.IO.StringWriter();
        var snap   = snapshotService.Capture(projectName, writer);

        // Echo captured lines
        foreach (var line in writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries))
            AnsiConsole.MarkupLine($"  {Markup.Escape(line.TrimEnd())}");

        if (snap is null)
        {
            AnsiConsole.MarkupLine($"  [yellow]Snapshot skipped — '{Markup.Escape(projectName)}' not in deploy journal.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        stateManager.SaveSnapshot(snap);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            $"[green]✓ Snapshot saved:[/] [bold]{Markup.Escape(projectName)}[/]  " +
            $"[dim]({snap.Apps.Count} window(s))[/]");

        // Show table of captured positions
        if (snap.Apps.Count > 0)
        {
            AnsiConsole.WriteLine();
            var table = new Table()
                .RoundedBorder()
                .AddColumn("[dim]Type[/]")
                .AddColumn("[dim]Position[/]")
                .AddColumn("[dim]Size[/]");

            foreach (var a in snap.Apps)
            {
                table.AddRow(
                    Markup.Escape(a.Type),
                    $"[dim]{a.X}, {a.Y}[/]",
                    $"[dim]{a.Width} × {a.Height}[/]");
            }

            AnsiConsole.Write(table);
        }

        AnsiConsole.WriteLine();
    }

    private static void ShowSnapshotList(StateManager stateManager)
    {
        var snapshots = stateManager.GetAllSnapshots()
            .OrderBy(s => s.Project)
            .ToList();

        AnsiConsole.WriteLine();
        if (snapshots.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No snapshots saved yet.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        AnsiConsole.MarkupLine($"[cyan]Saved snapshots ({snapshots.Count}):[/]");
        AnsiConsole.WriteLine();

        var table = new Table()
            .RoundedBorder()
            .AddColumn("[dim]Project[/]")
            .AddColumn("[dim]Captured[/]")
            .AddColumn("[dim]Windows[/]");

        foreach (var s in snapshots)
        {
            table.AddRow(
                Markup.Escape(s.Project),
                Markup.Escape(FormatAge(s.CapturedAt)),
                s.Apps.Count.ToString());
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static string FormatAge(DateTimeOffset ts)
    {
        var age = DateTimeOffset.UtcNow - ts;
        if (age.TotalSeconds < 60)  return "just now";
        if (age.TotalMinutes < 60)  return $"{(int)age.TotalMinutes}m ago";
        if (age.TotalHours   < 24)  return $"{(int)age.TotalHours}h ago";
        return $"{(int)age.TotalDays}d ago";
    }
}
