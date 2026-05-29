// UsageTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli.Tests;

using SnoopMCP.Cli;
using Xunit;

public sealed class UsageTests
{
    [Fact]
    public async Task Main_WithUnknownVerb_ReturnsUsageExitCode()
    {
        int code = await Program.Main(["bogus-verb"]);

        Assert.Equal(64, code);
    }

    [Fact]
    public async Task Main_WithNoArgs_ReturnsUsageExitCode()
    {
        int code = await Program.Main([]);

        Assert.Equal(64, code);
    }
}
