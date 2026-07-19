// HealthStatusTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using Host;
using Xunit;

public sealed class HealthStatusTests
{
    [Fact]
    public void Create_SetsOkStatus_AndPassesThroughVersionAndAttached()
    {
        var health = HealthStatus.Create("1.2.3", attached: true, interactionEnabled: false);

        Assert.Equal("ok", health.Status);
        Assert.Equal("1.2.3", health.Version);
        Assert.True(health.Attached);
    }

    [Fact]
    public void Create_NullOrEmptyVersion_Throws()
    {
        Assert.Throws<ArgumentException>(() => HealthStatus.Create(string.Empty, attached: false, interactionEnabled: false));
    }

    [Fact]
    public void Create_CarriesInteractionEnabled()
    {
        HealthStatus s = HealthStatus.Create("1.4.0", attached: false, interactionEnabled: true);
        Assert.True(s.InteractionEnabled);
        Assert.Equal("ok", s.Status);
    }
}
