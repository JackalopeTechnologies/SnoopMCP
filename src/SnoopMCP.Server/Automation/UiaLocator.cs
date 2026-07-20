// UiaLocator.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Automation;

using System.Windows.Automation;
using SnoopMCP.Protocol.Errors;

/// <summary>Maps a caller "by" locator kind + value to a UIA <see cref="Condition"/>.</summary>
public static class UiaLocator
{
    private const string ByAutomationId = "automationId";
    private const string ByName = "name";
    private const string ByHelpText = "helpText";
    private const string ByControlType = "controlType";

    /// <summary>The accepted locator kinds, in stability order.</summary>
    public static IReadOnlyList<string> KnownKinds { get; } = [ByAutomationId, ByName, ByHelpText, ByControlType];

    /// <summary>Builds a UIA property condition for the given locator, or throws <see cref="SnoopMcpException"/>.</summary>
    public static Condition ToCondition(string by, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(by);
        ArgumentNullException.ThrowIfNull(value);
        return by switch
        {
            ByAutomationId => new PropertyCondition(AutomationElement.AutomationIdProperty, value),
            ByName => new PropertyCondition(AutomationElement.NameProperty, value),
            ByHelpText => new PropertyCondition(AutomationElement.HelpTextProperty, value),
            ByControlType => new PropertyCondition(AutomationElement.ControlTypeProperty, ToControlType(value)),
            _ => throw new SnoopMcpException(
                ErrorCode.InvalidArgument,
                $"Unknown locator '{by}'. Use one of: {string.Join(", ", KnownKinds)}.")
        };
    }

    private static ControlType ToControlType(string name)
    {
        // ControlType.<Name> exposes a ProgrammaticName like "ControlType.Button"; match on the leaf.
        ControlType? result = null;
        foreach (System.Reflection.FieldInfo field in typeof(ControlType).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            if (field.GetValue(null) is ControlType ct
                && string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                result = ct;
                break;
            }
        }
        return result ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, $"Unknown controlType '{name}'.");
    }
}
