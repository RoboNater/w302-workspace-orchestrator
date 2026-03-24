using System.CommandLine;
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

            Console.WriteLine();
            if (projects.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("No project configs found.");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("Create a .workspace.yaml file in one of:");
                foreach (var dir in ConfigLoader.GetSearchDirectories())
                    Console.WriteLine($"  {dir}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Available projects ({projects.Count}):");
                Console.ResetColor();
                Console.WriteLine();
                foreach (var (name, path) in projects)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"  {name,-30}");
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  {path}");
                    Console.ResetColor();
                }
            }
            Console.WriteLine();
        });

        return cmd;
    }
}
