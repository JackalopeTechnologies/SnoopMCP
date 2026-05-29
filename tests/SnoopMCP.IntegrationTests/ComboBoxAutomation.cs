// ComboBoxAutomation.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.IntegrationTests;

using System.Windows.Automation;

/// <summary>
/// Opens or closes a ComboBox by its AutomationId via UI Automation. This is the only capture step
/// that drives the sample app through a path other than the MCP tool surface; the walkthrough notes
/// that a human reader would simply click the dropdown open instead. Searches the desktop root by
/// AutomationId (unique for the sample's ThemePicker).
/// </summary>
public static class ComboBoxAutomation
{
    public static bool TrySetDropDownOpen(string automationId, bool open)
    {
        ArgumentException.ThrowIfNullOrEmpty(automationId);
        bool ok = false;
        Condition byId = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
        AutomationElement? combo = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, byId);
        if (combo is not null
            && combo.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out object patternObj)
            && patternObj is ExpandCollapsePattern pattern)
        {
            if (open)
            {
                pattern.Expand();
            }
            else
            {
                pattern.Collapse();
            }
            ok = true;
        }
        return ok;
    }
}
