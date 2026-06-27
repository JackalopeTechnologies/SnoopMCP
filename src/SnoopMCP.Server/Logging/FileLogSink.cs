// FileLogSink.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Logging;

using System.IO;
using System.Text;

/// <summary>
/// Thread-safe, best-effort append-only sink backing <see cref="FileLoggerProvider"/>. Mirrors the
/// tray host's own <c>HostLog</c>: timestamped lines are appended under a single lock, and once the
/// file grows past <see cref="MaxLogBytes"/> it is deleted and started fresh (a hard reset, not a
/// rotation - no prior segment is kept). Every write is guarded so a logging failure can never
/// escape into the in-process MCP server; on any I/O error the line is silently dropped.
/// </summary>
internal sealed class FileLogSink
{
    private const long MaxLogBytes = 5L * 1024 * 1024;

    private readonly object mGate = new();
    private readonly string mPath;

    /// <summary>
    /// Initialises a new <see cref="FileLogSink"/> appending to the supplied file.
    /// </summary>
    /// <param name="path">The absolute path of the log file to append to.</param>
    public FileLogSink(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        mPath = path;
    }

    /// <summary>
    /// Appends a single pre-formatted line under the sink lock, guarding against any I/O failure.
    /// </summary>
    /// <param name="line">The line to append; a trailing newline is added.</param>
    public void Append(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        lock (mGate)
        {
            TryAppend(line);
        }
    }

    private void TryAppend(string line)
    {
        try
        {
            string? dir = Path.GetDirectoryName(mPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            if (File.Exists(mPath) && new FileInfo(mPath).Length > MaxLogBytes)
            {
                File.Delete(mPath);
            }
            File.AppendAllText(mPath, line + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception)
        {
            // Logging is best-effort: a logging failure must never escape into the server.
        }
    }
}
