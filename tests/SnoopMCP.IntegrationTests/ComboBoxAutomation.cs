// ComboBoxAutomation.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#region Usings

using System.Windows.Automation;

#endregion

namespace SnoopMCP.IntegrationTests;

/// <summary>
///     Opens or closes a ComboBox by its AutomationId via UI Automation. This is the only capture step
///     that drives the sample app through a path other than the MCP tool surface; the walkthrough notes
///     that a human reader would simply click the dropdown open instead. Searches the desktop root by
///     AutomationId (unique for the sample's ThemePicker).
/// </summary>
public static class ComboBoxAutomation
{
    public static bool TrySetDropDownOpen(string automationId, bool open)
    {
        ArgumentException.ThrowIfNullOrEmpty(automationId);
        var ok = false;
        Condition byId = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
        AutomationElement? combo = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, byId);
        if (combo is not null
            && combo.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var patternObj)
            && patternObj is ExpandCollapsePattern pattern)
        {
            if (open)
                pattern.Expand();
            else
                pattern.Collapse();
            ok = true;
        }

        return ok;
    }
}
