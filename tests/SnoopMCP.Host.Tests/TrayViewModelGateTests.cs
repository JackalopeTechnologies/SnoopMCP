// TrayViewModelGateTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using System.ComponentModel;
using Host;
using Host.Automation;
using Xunit;

public sealed class TrayViewModelGateTests : IDisposable
{
    private readonly string mPath = Path.Combine(Path.GetTempPath(), "gate-" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        if (File.Exists(mPath))
        {
            File.Delete(mPath);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ToggleInteraction_FlipsGate_AndRaisesPropertyChanged()
    {
        var gate = new InteractionGate(mPath);
        var controller = new ServerController([]);
        var vm = new TrayViewModel(controller, () => { }, (_, _) => { }, (_, _) => { }, gate);
        bool raised = false;
        vm.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(TrayViewModel.InteractionEnabled);

        Assert.False(vm.InteractionEnabled);
        vm.ToggleInteractionCommand.Execute(null);

        Assert.True(vm.InteractionEnabled);
        Assert.True(gate.IsEnabled);
        Assert.True(raised);
    }

    [Fact]
    public void ToolTipText_NonAdmin_NotesElevatedUnavailable()
    {
        var gate = new InteractionGate(mPath);
        var controller = new ServerController([]);
        var nonAdminVm = new TrayViewModel(controller, () => { }, (_, _) => { }, (_, _) => { }, gate, canDriveElevatedTargets: false);
        var adminVm = new TrayViewModel(controller, () => { }, (_, _) => { }, (_, _) => { }, gate, canDriveElevatedTargets: true);

        Assert.Contains("elevated-target driving unavailable", nonAdminVm.ToolTipText, StringComparison.Ordinal);
        Assert.DoesNotContain("elevated-target driving unavailable", adminVm.ToolTipText, StringComparison.Ordinal);
    }
}
