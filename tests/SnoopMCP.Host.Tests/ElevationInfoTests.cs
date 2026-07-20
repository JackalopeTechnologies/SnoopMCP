// ElevationInfoTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using Host.Automation;
using Xunit;

public sealed class ElevationInfoTests
{
    [Fact]
    public void IsElevated_And_CanElevate_AreConsistent_AndDoNotThrow()
    {
        bool isElevated = ElevationInfo.IsElevated();
        bool canElevate = ElevationInfo.CanElevate();

        if (isElevated)
        {
            Assert.True(canElevate);
        }
    }
}
