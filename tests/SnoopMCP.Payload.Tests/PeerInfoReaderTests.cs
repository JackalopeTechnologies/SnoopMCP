// PeerInfoReaderTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.Windows.Automation;
using System.Windows.Controls;
using Interaction;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class PeerInfoReaderTests
{
    [WpfFact]
    public void Read_ButtonWithAutomationId_ReturnsIt()
    {
        var button = new Button();
        AutomationProperties.SetAutomationId(button, "SaveBtn");
        var reader = new PeerInfoReader();

        GetAutomationPeerInfoResponse info = reader.Read(button);

        Assert.Equal("SaveBtn", info.AutomationId);
        Assert.False(string.IsNullOrEmpty(info.ClassName));
        Assert.Contains("Button", info.ControlType);
    }
}
