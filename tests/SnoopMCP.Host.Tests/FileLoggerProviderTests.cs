// FileLoggerProviderTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using System.IO;
using Microsoft.Extensions.Logging;
using Host.Logging;
using Xunit;

public sealed class FileLoggerProviderTests
{
    [Fact]
    public void Log_Error_WritesMessageCategoryLevelAndExceptionDetail()
    {
        string path = Path.Combine(Path.GetTempPath(), $"snoopmcp-log-{Guid.NewGuid():N}.log");
        try
        {
            using (var provider = new FileLoggerProvider(path, LogLevel.Information))
            {
                ILogger logger = provider.CreateLogger("SnoopMCP.TestCategory");
                logger.LogError(new InvalidOperationException("boom-detail"), "find_elements failed");
            }

            string contents = File.ReadAllText(path);
            Assert.Contains("find_elements failed", contents);
            Assert.Contains("SnoopMCP.TestCategory", contents);
            Assert.Contains("[ERR]", contents);
            Assert.Contains(nameof(InvalidOperationException), contents);
            Assert.Contains("boom-detail", contents);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void IsEnabled_HonoursTheConfiguredFloorLevel()
    {
        string path = Path.Combine(Path.GetTempPath(), $"snoopmcp-log-{Guid.NewGuid():N}.log");
        using var provider = new FileLoggerProvider(path, LogLevel.Information);
        ILogger logger = provider.CreateLogger("SnoopMCP.TestCategory");

        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.True(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.False(logger.IsEnabled(LogLevel.None));
    }

    [Fact]
    public void Log_BelowFloorLevel_WritesNothing()
    {
        string path = Path.Combine(Path.GetTempPath(), $"snoopmcp-log-{Guid.NewGuid():N}.log");
        try
        {
            using (var provider = new FileLoggerProvider(path, LogLevel.Warning))
            {
                ILogger logger = provider.CreateLogger("SnoopMCP.TestCategory");
                logger.LogInformation("routine chatter that should be dropped");
            }

            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
