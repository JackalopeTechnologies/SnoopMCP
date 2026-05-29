// Program.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli;

/// <summary>
/// Management CLI for SnoopMCP: registers the MCP server in LLM clients, manages the per-user logon
/// autostart task, and supervises the host process. Verbs are dispatched from <see cref="Main"/>;
/// each returns a process exit code (0 = success, 2 = partial failure, 64 = usage error).
/// </summary>
public static class Program
{
    private const string VerbRegisterClients = "register-clients";
    private const string VerbUnregisterClients = "unregister-clients";
    private const string VerbInstallAutostart = "install-autostart";
    private const string VerbUninstallAutostart = "uninstall-autostart";
    private const string VerbStatus = "status";
    private const string VerbStart = "start";
    private const string VerbStop = "stop";
    private const int ExitUsage = 64;

    /// <summary>Parses the verb and dispatches; returns the process exit code.</summary>
    /// <param name="args">Command-line arguments; <c>args[0]</c> is the verb.</param>
    public static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string verb = args.Length > 0 ? args[0] : string.Empty;
        Task<int> dispatched = verb switch
        {
            VerbRegisterClients => Task.FromResult(ExitUsage),
            VerbUnregisterClients => Task.FromResult(ExitUsage),
            VerbInstallAutostart => Task.FromResult(InstallAutostart()),
            VerbUninstallAutostart => Task.FromResult(UninstallAutostart()),
            VerbStatus => Task.FromResult(ExitUsage),
            VerbStart => Task.FromResult(ExitUsage),
            VerbStop => Task.FromResult(ExitUsage),
            _ => Task.FromResult(PrintUsage())
        };
        return await dispatched.ConfigureAwait(false);
    }

    private const int ExitOk = 0;
    private const int ExitFailure = 2;
    private const string MsgAutostartCreated = "Autostart task created.";
    private const string MsgAutostartCreateFailed = "Failed to create autostart task.";
    private const string MsgAutostartRemoved = "Autostart task removed.";
    private const string MsgAutostartRemoveFailed = "Failed to remove autostart task.";

    private static int PrintUsage()
    {
        Console.Error.WriteLine(
            "Usage: SnoopMCP.Cli <register-clients|unregister-clients|install-autostart|"
            + "uninstall-autostart|status|start|stop> [--vscode] [--claude-code]");
        return ExitUsage;
    }

    private static int InstallAutostart()
    {
        bool ok = AutostartTask.Create(HostProcess.ExePath());
        Console.WriteLine(ok ? MsgAutostartCreated : MsgAutostartCreateFailed);
        return ok ? ExitOk : ExitFailure;
    }

    private static int UninstallAutostart()
    {
        bool ok = AutostartTask.Remove();
        Console.WriteLine(ok ? MsgAutostartRemoved : MsgAutostartRemoveFailed);
        return ok ? ExitOk : ExitFailure;
    }
}
