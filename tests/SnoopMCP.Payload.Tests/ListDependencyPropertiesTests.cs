// ListDependencyPropertiesTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload.Tests;

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Tools;
using Xunit;

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
            string key = $"{dto.OwnerType}.{dto.Name}";
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
