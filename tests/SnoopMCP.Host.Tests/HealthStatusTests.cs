// HealthStatusTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host.Tests;

using SnoopMCP.Host;
using Xunit;

public sealed class HealthStatusTests
{
    [Fact]
    public void Create_SetsOkStatus_AndPassesThroughVersionAndAttached()
    {
        HealthStatus health = HealthStatus.Create("1.2.3", attached: true);

        Assert.Equal("ok", health.Status);
        Assert.Equal("1.2.3", health.Version);
        Assert.True(health.Attached);
    }

    [Fact]
    public void Create_NullVersion_Throws()
    {
        Assert.Throws<ArgumentException>(() => HealthStatus.Create(string.Empty, attached: false));
    }
}
