// Program.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli;

using SnoopMCP.ClientIntegration;

/// <summary>
/// Management CLI for SnoopMCP: registers the MCP server in LLM clients, manages the per-user logon
/// autostart task, and supervises the host process. Verbs are dispatched from <see cref="Main"/>;
/// each returns a process exit code (0 = success, 2 = failure, 64 = usage error).
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
    private const int ExitOk = 0;
    private const int ExitFailure = 2;
    private const string FlagVsCode = "--vscode";
    private const string FlagClaudeCode = "--claude-code";
    private const string FlagCodex = "--codex";
    private const string MsgAutostartCreated = "Autostart task created.";
    private const string MsgAutostartCreateFailed = "Failed to create autostart task.";
    private const string MsgAutostartRemoved = "Autostart task removed.";
    private const string MsgAutostartRemoveFailed = "Failed to remove autostart task.";
    private const string MsgAutostartPresent = "Autostart task: present.";
    private const string MsgAutostartAbsent = "Autostart task: absent.";
    private const string MsgHostReachable = "Host: reachable on :6300.";
    private const string MsgHostNotReachable = "Host: not reachable.";
    private const string MsgHostStarted = "Host started.";
    private const string MsgHostStartFailed = "Host failed to start.";

    /// <summary>Parses the verb and dispatches; returns the process exit code.</summary>
    /// <param name="args">Command-line arguments; <c>args[0]</c> is the verb.</param>
    public static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string verb = args.Length > 0 ? args[0] : string.Empty;
        Task<int> dispatched = verb switch
        {
            VerbRegisterClients => Task.FromResult(RegisterClients(args)),
            VerbUnregisterClients => Task.FromResult(UnregisterClients(args)),
            VerbInstallAutostart => Task.FromResult(InstallAutostart()),
            VerbUninstallAutostart => Task.FromResult(UninstallAutostart()),
            VerbStatus => StatusAsync(),
            VerbStart => Task.FromResult(Start()),
            VerbStop => Task.FromResult(Stop()),
            _ => Task.FromResult(PrintUsage())
        };
        return await dispatched.ConfigureAwait(false);
    }

    private static async Task<int> StatusAsync()
    {
        ClientRegistration.Status(SelectWriters([]), Console.Out);
        Console.WriteLine(AutostartTask.Exists() ? MsgAutostartPresent : MsgAutostartAbsent);
        using var client = new HttpClient();
        bool healthy = await HostHealthProbe
            .IsHealthyAsync(client, HostHealthProbe.HealthUrl, default)
            .ConfigureAwait(false);
        Console.WriteLine(healthy ? MsgHostReachable : MsgHostNotReachable);
        return ExitOk;
    }

    private static int Start()
    {
        bool started = HostProcess.Start();
        Console.WriteLine(started ? MsgHostStarted : MsgHostStartFailed);
        return started ? ExitOk : ExitFailure;
    }

    private static int Stop()
    {
        int stopped = HostProcess.Stop();
        Console.WriteLine($"Stopped {stopped} host process(es).");
        return ExitOk;
    }

    private static int PrintUsage()
    {
        Console.Error.WriteLine(
            "Usage: SnoopMCP.Cli <register-clients|unregister-clients|install-autostart|"
            + "uninstall-autostart|status|start|stop> [--vscode] [--claude-code] [--codex]");
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

    private static List<IClientWriter> SelectWriters(string[] args)
    {
        bool wantVsCode = HasFlag(args, FlagVsCode);
        bool wantClaude = HasFlag(args, FlagClaudeCode);
        bool wantCodex = HasFlag(args, FlagCodex);
        bool all = !wantVsCode && !wantClaude && !wantCodex;
        var writers = new List<IClientWriter>();
        if (wantClaude || all)
        {
            writers.Add(ClaudeCodeWriter.ForCurrentUser());
        }
        if (wantVsCode || all)
        {
            writers.Add(VsCodeMcpWriter.ForCurrentUser());
        }
        if (wantCodex || all)
        {
            writers.Add(CodexWriter.ForCurrentUser());
        }
        return writers;
    }

    private static bool HasFlag(string[] args, string flag)
    {
        return Array.Exists(args, a => string.Equals(a, flag, StringComparison.Ordinal));
    }

    private static int RegisterClients(string[] args)
    {
        return ClientRegistration.RegisterAll(SelectWriters(args), McpEndpoint.Default, Console.Out);
    }

    private static int UnregisterClients(string[] args)
    {
        return ClientRegistration.UnregisterAll(SelectWriters(args), Console.Out);
    }
}
