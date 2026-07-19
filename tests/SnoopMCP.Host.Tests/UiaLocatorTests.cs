// UiaLocatorTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using System.Windows.Automation;
using Host.Automation;
using Xunit;

public sealed class UiaLocatorTests
{
    [Fact]
    public void ToCondition_AutomationId_BuildsPropertyCondition()
    {
        Condition c = UiaLocator.ToCondition("automationId", "SaveButton");
        var pc = Assert.IsType<PropertyCondition>(c);
        Assert.Equal(AutomationElement.AutomationIdProperty, pc.Property);
        Assert.Equal("SaveButton", pc.Value);
    }

    [Fact]
    public void ToCondition_ControlType_MapsName()
    {
        Condition c = UiaLocator.ToCondition("controlType", "Button");
        var pc = Assert.IsType<PropertyCondition>(c);
        Assert.Equal(AutomationElement.ControlTypeProperty, pc.Property);

        // PropertyCondition normalises a ControlType argument to its raw int Id for storage
        // (verified at runtime); PropertyCondition.Value is System.Int32 here, not ControlType.
        Assert.Equal(ControlType.Button.Id, (int)pc.Value);
    }

    [Fact]
    public void ToCondition_UnknownBy_Throws()
    {
        Assert.Throws<SnoopMCP.Protocol.Errors.SnoopMcpException>(() => UiaLocator.ToCondition("colour", "x"));
    }
}
