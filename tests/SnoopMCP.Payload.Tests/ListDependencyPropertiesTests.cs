// ListDependencyPropertiesTests.cs
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

using System.Windows.Controls;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Tools;
using Xunit;

#endregion

namespace SnoopMCP.Payload.Tests;

public sealed class ListDependencyPropertiesTests
{
    [StaFact]
    public void List_Button_IncludesInheritedDpsLikeIsEnabled()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button();

        ListDependencyPropertiesResponse response = inspector.ListProperties(button);

        Assert.Contains(response.Properties, p => p.Name == "IsEnabled");
    }

    [StaFact]
    public void List_Button_IncludesOwnDpLikeIsCancel()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button();

        ListDependencyPropertiesResponse response = inspector.ListProperties(button);

        Assert.Contains(
            response.Properties,
            p => p.Name == "IsCancel" && p.OwnerType.EndsWith(".Button", StringComparison.Ordinal));
    }

    [StaFact]
    public void List_HasNoDuplicateNameOwnerPairs()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button();

        ListDependencyPropertiesResponse response = inspector.ListProperties(button);

        var seen = new HashSet<string>();
        foreach (DependencyPropertyDto dto in response.Properties)
        {
            var key = $"{dto.OwnerType}.{dto.Name}";
            Assert.True(seen.Add(key), $"Duplicate DP {key}");
        }
    }

    [StaFact]
    public void List_LocallySetAttachedDp_MarkedIsAttachedTrue()
    {
        var inspector = new DependencyPropertyInspector();
        var grid = new Grid();
        var button = new Button();
        Grid.SetRow(button, 2);
        grid.Children.Add(button);

        ListDependencyPropertiesResponse response = inspector.ListProperties(button);

        DependencyPropertyDto? rowEntry = response.Properties
            .FirstOrDefault(p => p.Name == "Row" && p.OwnerType.EndsWith(".Grid", StringComparison.Ordinal));
        Assert.NotNull(rowEntry);
        Assert.True(rowEntry!.IsAttached);
    }

    [StaFact]
    public void List_OwnerType_FullName_NotShortName()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button();

        ListDependencyPropertiesResponse response = inspector.ListProperties(button);

        DependencyPropertyDto? isEnabled = response.Properties.FirstOrDefault(p => p.Name == "IsEnabled");
        Assert.NotNull(isEnabled);
        Assert.Contains('.', isEnabled!.OwnerType);
    }

    [StaFact]
    public void List_NullElement_Throws()
    {
        var inspector = new DependencyPropertyInspector();
        Assert.Throws<ArgumentNullException>(() => inspector.ListProperties(null!));
    }
}
