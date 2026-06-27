// FileLogger.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Logging;

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

/// <summary>
/// An <see cref="ILogger"/> that formats each entry as a single timestamped line
/// (<c>yyyy-MM-dd HH:mm:ss.fff [LVL] category: message</c>) with any exception detail appended on
/// the following lines, then forwards it to the shared <see cref="FileLogSink"/>. Entries below the
/// provider's floor level are dropped before formatting.
/// </summary>
internal sealed class FileLogger : ILogger
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
    private const string LineFormat = "{0} [{1}] {2}: {3}{4}";
    private const string LevelTrace = "TRC";
    private const string LevelDebug = "DBG";
    private const string LevelInfo = "INF";
    private const string LevelWarn = "WRN";
    private const string LevelError = "ERR";
    private const string LevelCritical = "CRT";
    private const string LevelDefault = "LOG";

    private static readonly CompositeFormat smLineFormat = CompositeFormat.Parse(LineFormat);

    private readonly string mCategory;
    private readonly FileLogSink mSink;
    private readonly LogLevel mMinLevel;

    /// <summary>
    /// Initialises a new <see cref="FileLogger"/>.
    /// </summary>
    /// <param name="category">The logger category name (typically the source type's full name).</param>
    /// <param name="sink">The shared sink to forward formatted lines to.</param>
    /// <param name="minLevel">The floor level; entries below it are dropped.</param>
    public FileLogger(string category, FileLogSink sink, LogLevel minLevel)
    {
        ArgumentException.ThrowIfNullOrEmpty(category);
        ArgumentNullException.ThrowIfNull(sink);
        mCategory = category;
        mSink = sink;
        mMinLevel = minLevel;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => NullScope.Instance;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= mMinLevel;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        if (IsEnabled(logLevel))
        {
            string detail = exception is null ? string.Empty : Environment.NewLine + exception;
            string line = string.Format(
                CultureInfo.InvariantCulture,
                smLineFormat,
                DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture),
                Abbreviate(logLevel),
                mCategory,
                formatter(state, exception),
                detail);
            mSink.Append(line);
        }
    }

    private static string Abbreviate(LogLevel level) => level switch
    {
        LogLevel.Trace => LevelTrace,
        LogLevel.Debug => LevelDebug,
        LogLevel.Information => LevelInfo,
        LogLevel.Warning => LevelWarn,
        LogLevel.Error => LevelError,
        LogLevel.Critical => LevelCritical,
        _ => LevelDefault
    };
}
