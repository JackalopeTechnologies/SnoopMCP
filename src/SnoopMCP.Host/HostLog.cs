// HostLog.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using System.Globalization;
using System.IO;
using System.Text;

/// <summary>
/// Minimal best-effort file logger for the windowless tray host, which has no console to surface
/// errors. Writes timestamped lines to <c>%LOCALAPPDATA%\SnoopMCP\logs\host.log</c>; once the file
/// grows past <see cref="MaxLogBytes"/> it is deleted and started fresh (a hard reset, not a
/// rotation - no prior segment is kept). Every write is guarded so a logging failure can never
/// crash the app; on any I/O error the message is silently dropped.
/// </summary>
internal static class HostLog
{
    private const string AppFolderName = "SnoopMCP";
    private const string LogsFolderName = "logs";
    private const string LogFileName = "host.log";
    private const long MaxLogBytes = 5L * 1024 * 1024;
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
    private const string LineFormat = "{0} [{1}] {2}{3}";
    private const string LevelInfo = "INF";
    private const string LevelWarn = "WRN";
    private const string LevelError = "ERR";

    private static readonly CompositeFormat smLineFormat = CompositeFormat.Parse(LineFormat);
    private static readonly object smGate = new();
    private static readonly string smLogPath = BuildLogPath();

    /// <summary>Logs an informational message.</summary>
    internal static void Info(string message) => Write(LevelInfo, message, null);

    /// <summary>Logs a warning.</summary>
    internal static void Warn(string message) => Write(LevelWarn, message, null);

    /// <summary>Logs an error with optional exception detail.</summary>
    internal static void Error(string message, Exception? exception = null) => Write(LevelError, message, exception);

    private static string BuildLogPath()
    {
        string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDir, AppFolderName, LogsFolderName, LogFileName);
    }

    private static void Write(string level, string message, Exception? exception)
    {
        string detail = exception is null ? string.Empty : Environment.NewLine + exception;
        string line = string.Format(
            CultureInfo.InvariantCulture, smLineFormat,
            DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture), level, message, detail);
        lock (smGate)
        {
            TryAppend(line);
        }
    }

    private static void TryAppend(string line)
    {
        try
        {
            string? dir = Path.GetDirectoryName(smLogPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            if (File.Exists(smLogPath) && new FileInfo(smLogPath).Length > MaxLogBytes)
            {
                File.Delete(smLogPath);
            }
            File.AppendAllText(smLogPath, line + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception)
        {
            // Logging is best-effort: a logging failure must never escape into the app.
        }
    }
}
