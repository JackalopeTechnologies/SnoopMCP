// ToolNamesTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tests;

using Xunit;

/// <summary>
/// Stability tests for the wire-encoded <see cref="ToolNames"/> string constants.
/// </summary>
public sealed class ToolNamesTests
{
    [Fact]
    public void DrivingToolNames_AreStable()
    {
        Assert.Equal("getAutomationPeerInfo", ToolNames.GetAutomationPeerInfo);
        Assert.Equal("peerInvoke", ToolNames.PeerInvoke);
        Assert.Equal("executeCommand", ToolNames.ExecuteCommand);
        Assert.Equal("waitForValue", ToolNames.WaitForValue);
    }
}
