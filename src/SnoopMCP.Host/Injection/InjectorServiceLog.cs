namespace SnoopMCP.Host.Injection;

using Microsoft.Extensions.Logging;

internal static partial class InjectorServiceLog
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Payload injected into pid {processId} on pipe {pipeName}.")]
    public static partial void LogInjectionSucceeded(this ILogger logger, int processId, string pipeName);
}
