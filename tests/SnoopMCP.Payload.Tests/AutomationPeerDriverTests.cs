// AutomationPeerDriverTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Threading;
using Interaction;
using Payload;
using Tools;
using Protocol.Wire;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class AutomationPeerDriverTests
{
    [WpfFact]
    public void Invoke_Button_FiresClick()
    {
        bool clicked = false;
        var button = new Button();
        button.Click += (_, _) => clicked = true;
        // A peer needs the element in a rendered tree for some patterns; for InvokePattern a loose Button works.
        var driver = new AutomationPeerDriver();

        driver.Invoke(button, "Invoke");
        PumpDispatcher();

        Assert.True(clicked);
    }

    [WpfFact]
    public void Invoke_Button_LowercasePattern_FiresClick()
    {
        bool clicked = false;
        var button = new Button();
        button.Click += (_, _) => clicked = true;
        var driver = new AutomationPeerDriver();

        driver.Invoke(button, "invoke");
        PumpDispatcher();

        Assert.True(clicked);
    }

    [WpfFact]
    public void PeerInvoke_WaitMode_RunsSynchronously_AndReportsOk()
    {
        bool clicked = false;
        var button = new Button();
        button.Click += (_, _) => clicked = true;
        var registry = new ElementRegistry();
        int id = registry.GetOrAssign(button);
        var driver = new AutomationPeerDriver();
        var marshal = new DispatcherMarshal(Dispatcher.CurrentDispatcher, TimeSpan.FromSeconds(2));
        var handler = new PeerInvokeToolHandler(registry, driver, marshal);
        JsonElement arguments = ToArguments(new PeerInvokeRequest(id, "Invoke", null));

        JsonElement result = handler.ExecuteAsync(arguments, default).GetAwaiter().GetResult();
        PumpDispatcher();
        PeerInvokeResponse? response = result.Deserialize<PeerInvokeResponse>(WireSerializer.JsonOptions);

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.False(response.Dispatched);
        Assert.True(clicked);
    }

    [WpfFact]
    public void PeerInvoke_PostMode_ReturnsImmediately_DispatchedTrue_OkNull()
    {
        var button = new Button();
        var registry = new ElementRegistry();
        int id = registry.GetOrAssign(button);
        var driver = new AutomationPeerDriver();
        var marshal = new DispatcherMarshal(Dispatcher.CurrentDispatcher, TimeSpan.FromSeconds(2));
        var handler = new PeerInvokeToolHandler(registry, driver, marshal);
        JsonElement arguments = ToArguments(new PeerInvokeRequest(id, "Invoke", "post"));

        JsonElement result = handler.ExecuteAsync(arguments, default).GetAwaiter().GetResult();
        PeerInvokeResponse? response = result.Deserialize<PeerInvokeResponse>(WireSerializer.JsonOptions);

        Assert.NotNull(response);
        Assert.True(response!.Dispatched);
        Assert.Null(response.Ok);
    }

    private static JsonElement ToArguments(PeerInvokeRequest request)
    {
        string json = JsonSerializer.Serialize(request, WireSerializer.JsonOptions);
        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Pumps the current dispatcher until it drains everything queued at or above
    /// <see cref="DispatcherPriority.Input"/>. <c>ButtonAutomationPeer.IInvokeProvider.Invoke()</c>
    /// itself dispatches the click via <c>Dispatcher.BeginInvoke(DispatcherPriority.Input, ...)</c>
    /// (so a click handler that opens a dialog does not block the invoking call) — the click has not
    /// actually run until this drains, even though <see cref="AutomationPeerDriver.Invoke"/> already
    /// returned. Scheduling the frame's exit at <see cref="DispatcherPriority.ApplicationIdle"/>
    /// (lower priority than Input) guarantees the queued click runs first.
    /// </summary>
    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
