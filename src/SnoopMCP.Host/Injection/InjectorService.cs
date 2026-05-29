namespace SnoopMCP.Host.Injection;

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using SnoopMCP.Protocol.Errors;

public sealed class InjectorService : IInjectorService
{
    private const string LauncherRelativePath = @"injector\Snoop.InjectorLauncher.x64.exe";
    private const string PayloadRelativePath = @"payload\SnoopMCP.Payload.dll";
    private const string PayloadClassName = "SnoopMCP.Payload.PayloadEntryPoint";
    private const string PayloadMethodName = "Inject";
    private const int AccessDeniedExitCode = 5;

    private const string ArgTargetPid = "--targetPID";
    private const string ArgAssembly = "--assembly";
    private const string ArgClassName = "--className";
    private const string ArgMethodName = "--methodName";
    private const string ArgSettingsFile = "--settingsFile";

    private readonly ProcessProbe mProbe;
    private readonly ILogger<InjectorService> mLogger;

    public InjectorService(ProcessProbe probe, ILogger<InjectorService> logger)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(logger);
        mProbe = probe;
        mLogger = logger;
    }

    public Task<ProcessProbeResult> ProbeAsync(int processId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(mProbe.Probe(processId));
    }

    public async Task InjectAsync(int processId, string pipeName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(pipeName);

        string launcherPath = LocateLauncher();
        string payloadPath = LocatePayload();

        var startInfo = new ProcessStartInfo(launcherPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(launcherPath) ?? AppContext.BaseDirectory
        };
        startInfo.ArgumentList.Add(ArgTargetPid);
        startInfo.ArgumentList.Add(processId.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(ArgAssembly);
        startInfo.ArgumentList.Add(payloadPath);
        startInfo.ArgumentList.Add(ArgClassName);
        startInfo.ArgumentList.Add(PayloadClassName);
        startInfo.ArgumentList.Add(ArgMethodName);
        startInfo.ArgumentList.Add(PayloadMethodName);
        startInfo.ArgumentList.Add(ArgSettingsFile);
        startInfo.ArgumentList.Add(pipeName);

        using Process launcher = Process.Start(startInfo)
            ?? throw new SnoopMcpException(ErrorCode.AttachFailed, "Failed to start the injector launcher.");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        launcher.OutputDataReceived += (_, e) => AppendLine(stdout, e.Data);
        launcher.ErrorDataReceived += (_, e) => AppendLine(stderr, e.Data);
        launcher.BeginOutputReadLine();
        launcher.BeginErrorReadLine();

        await launcher.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        int exitCode = launcher.ExitCode;
        bool succeeded = exitCode == 0;
        if (succeeded)
        {
            mLogger.LogInjectionSucceeded(processId, pipeName);
        }
        else
        {
            string detail = $"launcher exit {exitCode}. stdout: {stdout}; stderr: {stderr}";
            ErrorCode code = exitCode == AccessDeniedExitCode ? ErrorCode.AccessDenied : ErrorCode.PayloadLoadFailed;
            throw new SnoopMcpException(code, $"Injection into pid {processId} failed: {detail}");
        }
    }

    private static void AppendLine(StringBuilder sink, string? line)
    {
        bool hasContent = !string.IsNullOrEmpty(line);
        if (hasContent)
        {
            sink.AppendLine(line);
        }
    }

    private static string LocateLauncher()
    {
        string candidate = Path.Combine(AppContext.BaseDirectory, LauncherRelativePath);
        bool exists = File.Exists(candidate);
        if (!exists)
        {
            throw new SnoopMcpException(
                ErrorCode.AttachFailed,
                $"Injector launcher not found at {candidate}. Was SnoopMCP.Injection built and staged (Task 30)?");
        }
        return candidate;
    }

    private static string LocatePayload()
    {
        string candidate = Path.Combine(AppContext.BaseDirectory, PayloadRelativePath);
        bool exists = File.Exists(candidate);
        if (!exists)
        {
            throw new SnoopMcpException(
                ErrorCode.PayloadLoadFailed,
                $"Payload not found at {candidate}. Verify CopyPayloadBinaries staged the payload closure.");
        }
        return candidate;
    }
}
