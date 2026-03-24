using System.CommandLine;
using WorkspaceOrchestrator.Cli.Commands;
using WorkspaceOrchestrator.Core.Config;
using WorkspaceOrchestrator.Core.Services;

// Ensure Unicode output renders correctly (checkmarks, bullets, etc.)
Console.OutputEncoding = System.Text.Encoding.UTF8;

// Build shared services
var configLoader     = new ConfigLoader();
var stateManager     = new StateManager();
var windowManager    = new WindowManager();
var vdService        = new VirtualDesktopService();
var terminalLauncher = new TerminalLauncher();
var browserLauncher  = new BrowserLauncher(windowManager);
var deployService    = new DeployService(windowManager, vdService, terminalLauncher, browserLauncher, stateManager);
var stowService      = new StowService(windowManager, stateManager);

// Root command
var root = new RootCommand("ws — Workspace Orchestrator: deploy/stow project contexts on Windows 11");

// Global options available on all subcommands
var dryRunOption  = new Option<bool>("--dry-run", "Show what would happen without making changes");
var verboseOption = new Option<bool>(new[] { "--verbose", "-v" }, "Extra diagnostic output");
root.AddGlobalOption(dryRunOption);
root.AddGlobalOption(verboseOption);

// Subcommands
root.AddCommand(DeployCommand.Build(configLoader, deployService, dryRunOption, verboseOption));
root.AddCommand(StowCommand.Build(configLoader, stowService, dryRunOption, verboseOption));
root.AddCommand(SwitchCommand.Build(configLoader, deployService, stowService, stateManager, dryRunOption, verboseOption));
root.AddCommand(ListCommand.Build(configLoader));
root.AddCommand(StatusCommand.Build(stateManager));
root.AddCommand(ValidateCommand.Build(configLoader));
root.AddCommand(EditCommand.Build(configLoader));

return await root.InvokeAsync(args);
