// PeerInfoReaderTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.Windows.Automation;
using System.Windows.Controls;
using Interaction;
using Protocol.Errors;
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

    /// <summary>
    /// This call exists to tell a caller what <c>peerInvoke</c> will accept, and it was the one call
    /// that did not say (issue #77). Without it, establishing that a CheckBox's peer supports Toggle
    /// required cross-checking a different tool in the other address space.
    /// </summary>
    [WpfFact]
    public void Read_CheckBox_ReportsTogglePattern()
    {
        var checkBox = new CheckBox();
        var reader = new PeerInfoReader();

        GetAutomationPeerInfoResponse info = reader.Read(checkBox);

        Assert.Contains("Toggle", info.Patterns);
        Assert.DoesNotContain("Invoke", info.Patterns);
    }

    [WpfFact]
    public void Read_Button_ReportsInvokePattern()
    {
        var button = new Button();
        var reader = new PeerInfoReader();

        GetAutomationPeerInfoResponse info = reader.Read(button);

        Assert.Contains("Invoke", info.Patterns);
    }

    /// <summary>A TextBlock has a peer, but no action pattern — an empty list, not a failure.</summary>
    [WpfFact]
    public void Read_TextBlock_ReportsNoDrivablePatterns()
    {
        var text = new TextBlock { Text = "Validation" };
        var reader = new PeerInfoReader();

        GetAutomationPeerInfoResponse info = reader.Read(text);

        Assert.Empty(info.Patterns);
    }

    /// <summary>
    /// Distinct from the case above: a bare Border has no <c>AutomationPeer</c> at all, so it fails
    /// rather than reporting an empty pattern list. Pinned so the two stay distinguishable.
    /// </summary>
    [WpfFact]
    public void Read_PlainBorder_WhichHasNoPeerAtAll_ReportsNotDrivable()
    {
        var border = new Border();
        var reader = new PeerInfoReader();

        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(() => reader.Read(border));

        Assert.Equal(ErrorCode.NotDrivable, ex.Code);
    }
}
