// UsageTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Cli.Tests;

using Cli;
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
