using System.CommandLine;
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

            Console.WriteLine();
            if (deployed.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("No projects currently deployed.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Deployed projects ({deployed.Count}):");
                Console.ResetColor();
                Console.WriteLine();

                foreach (var p in deployed)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("  ● ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"{p.Project,-30}");
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  deployed {FormatAge(p.DeployedAt)}");
                    Console.ResetColor();

                    foreach (var app in p.Apps)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"      {app.Type,-10}  {app.TitlePattern}");
                        Console.ResetColor();
                    }
                    Console.WriteLine();
                }
            }
            Console.WriteLine();
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
