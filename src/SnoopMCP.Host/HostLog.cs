// HostLog.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#region Usings

using System.Globalization;
using System.IO;
using System.Text;

#endregion

namespace SnoopMCP.Host;

/// <summary>
///     Minimal best-effort file logger for the windowless tray host, which has no console to surface
///     errors. Writes timestamped lines to <c>%LOCALAPPDATA%\SnoopMCP\logs\host.log</c>; once the file
///     grows past <see cref="MaxLogBytes" /> it is deleted and started fresh (a hard reset, not a
///     rotation - no prior segment is kept). Every write is guarded so a logging failure can never
///     crash the app; on any I/O error the message is silently dropped.
/// </summary>
internal static class HostLog
{
    /// <summary>Logs an informational message.</summary>
    internal static void Info(string message)
    {
        Write(LevelInfo, message, null);
    }

    /// <summary>Logs a warning.</summary>
    internal static void Warn(string message)
    {
        Write(LevelWarn, message, null);
    }

    /// <summary>Logs an error with optional exception detail.</summary>
    internal static void Error(string message, Exception? exception = null)
    {
        Write(LevelError, message, exception);
    }

    private static string BuildLogPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDir, AppFolderName, LogsFolderName, LogFileName);
    }

    private static void Write(string level, string message, Exception? exception)
    {
        var detail = exception is null ? string.Empty : Environment.NewLine + exception;
        var line = string.Format(
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
            var dir = Path.GetDirectoryName(smLogPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(smLogPath) && new FileInfo(smLogPath).Length > MaxLogBytes) File.Delete(smLogPath);
            File.AppendAllText(smLogPath, line + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception)
        {
            // Logging is best-effort: a logging failure must never escape into the app.
        }
    }

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
    private static readonly object smGate = new object();
    private static readonly string smLogPath = BuildLogPath();
}
