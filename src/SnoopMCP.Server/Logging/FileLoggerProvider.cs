// FileLoggerProvider.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Logging;

using System.IO;
using Microsoft.Extensions.Logging;

/// <summary>
/// An <see cref="ILoggerProvider"/> that persists every category's entries to a single shared
/// <see cref="FileLogSink"/> under <c>%LOCALAPPDATA%\SnoopMCP\logs\server.log</c>. This is what
/// makes an in-process MCP-server failure diagnosable: when a tool handler throws, the MCP SDK logs
/// the underlying exception through <see cref="ILogger"/> before returning its sanitised
/// "An error occurred invoking '…'" text to the client, so without a persistent provider that
/// detail is lost. Entries below <see cref="DefaultMinimumLevel"/> are dropped unless a tighter
/// per-category filter is configured on the logging builder.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    /// <summary>The floor level applied when none is supplied; entries below it are dropped.</summary>
    public const LogLevel DefaultMinimumLevel = LogLevel.Information;

    private const string AppFolderName = "SnoopMCP";
    private const string LogsFolderName = "logs";
    private const string LogFileName = "server.log";

    private readonly FileLogSink mSink;
    private readonly LogLevel mMinLevel;

    /// <summary>
    /// Initialises a new <see cref="FileLoggerProvider"/> writing to the supplied file.
    /// </summary>
    /// <param name="path">The absolute path of the log file to append to.</param>
    /// <param name="minLevel">The floor level; entries below it are dropped.</param>
    public FileLoggerProvider(string path, LogLevel minLevel = DefaultMinimumLevel)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        mSink = new FileLogSink(path);
        mMinLevel = minLevel;
    }

    /// <summary>
    /// Builds the default server log path under <c>%LOCALAPPDATA%\SnoopMCP\logs</c>, the same folder
    /// the tray host writes <c>host.log</c> into.
    /// </summary>
    /// <returns>The absolute path of <c>server.log</c>.</returns>
    public static string DefaultLogPath()
    {
        string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDir, AppFolderName, LogsFolderName, LogFileName);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        ArgumentNullException.ThrowIfNull(categoryName);
        return new FileLogger(categoryName, mSink, mMinLevel);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // The sink opens and closes the file on every append, so it holds no handle to release, and
        // the class is sealed with no finalizer; this method exists only to satisfy ILoggerProvider.
    }
}
