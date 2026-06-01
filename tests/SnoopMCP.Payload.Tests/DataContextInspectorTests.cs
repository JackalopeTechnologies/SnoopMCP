// DataContextInspectorTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class DataContextInspectorTests
{
    [StaFact]
    public void DescribeDataContext_NullDataContext_ReturnsNullInfo()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid();

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.Null(response.DataContext);
    }

    [StaFact]
    public void DescribeDataContext_SimpleClass_ReturnsTypeName()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new TestModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        Assert.Equal("TestModel", response.DataContext!.TypeName);
    }

    [StaFact]
    public void DescribeDataContext_ReturnsNamespace()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new TestModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        Assert.Equal(typeof(TestModel).Namespace, response.DataContext!.Namespace);
    }

    [StaFact]
    public void DescribeDataContext_BaseTypesIncludeObject()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new TestModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        Assert.Contains(typeof(object).FullName, response.DataContext!.BaseTypes);
    }

    [StaFact]
    public void DescribeDataContext_DerivedType_BaseTypesIncludeImmediateAndUltimateBases()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new DerivedModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        Assert.Contains(typeof(BaseModel).FullName, response.DataContext!.BaseTypes);
        Assert.Contains(typeof(object).FullName, response.DataContext.BaseTypes);
    }

    [StaFact]
    public void DescribeDataContext_InterfacesIncludeINotifyPropertyChanged()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new NotifyingModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        Assert.Contains(typeof(INotifyPropertyChanged).FullName, response.DataContext!.Interfaces);
    }

    [StaFact]
    public void DescribeDataContext_DeclaredPropertiesEnumerated()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new TestModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        Assert.Contains(response.DataContext!.DeclaredProperties, p => p.Name == "Name");
        Assert.Contains(response.DataContext.DeclaredProperties, p => p.Name == "Age");
    }

    [StaFact]
    public void DescribeDataContext_PropertyType_IsFullName()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new TestModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        DeclaredPropertyDto nameProp = response.DataContext!.DeclaredProperties.First(p => p.Name == "Name");
        Assert.Equal(typeof(string).FullName, nameProp.Type);
    }

    [StaFact]
    public void DescribeDataContext_ReadWriteProperty_BothFlagsTrue()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new TestModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        DeclaredPropertyDto nameProp = response.DataContext!.DeclaredProperties.First(p => p.Name == "Name");
        Assert.True(nameProp.CanRead);
        Assert.True(nameProp.CanWrite);
    }

    [StaFact]
    public void DescribeDataContext_ReadOnlyProperty_CanWriteIsFalse()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new ReadOnlyModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        DeclaredPropertyDto prop = response.DataContext!.DeclaredProperties.First(p => p.Name == "Id");
        Assert.True(prop.CanRead);
        Assert.False(prop.CanWrite);
    }

    [StaFact]
    public void DescribeDataContext_DeclaredOnly_ExcludesInheritedProperties()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new DerivedModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        Assert.Contains(response.DataContext!.DeclaredProperties, p => p.Name == "Child");
        Assert.DoesNotContain(response.DataContext.DeclaredProperties, p => p.Name == "Parent");
    }

    [StaFact]
    public void DescribeDataContext_ContentElement_AlsoSupported()
    {
        var inspector = new DataContextInspector();
        var run = new Run { DataContext = new TestModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(run);

        Assert.NotNull(response.DataContext);
        Assert.Equal("TestModel", response.DataContext!.TypeName);
    }

    [StaFact]
    public void DescribeDataContext_PlainDependencyObject_ReturnsNullInfo()
    {
        var inspector = new DataContextInspector();
        var plain = new DependencyObject();

        DescribeDataContextResponse response = inspector.DescribeDataContext(plain);

        Assert.Null(response.DataContext);
    }

    [StaFact]
    public void DescribeDataContext_NullElement_Throws()
    {
        var inspector = new DataContextInspector();

        Assert.Throws<ArgumentNullException>(() => inspector.DescribeDataContext(null!));
    }

    private sealed class TestModel
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    private sealed class ReadOnlyModel
    {
        public int Id { get; } = 42;
    }

    private sealed class NotifyingModel : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
    }

    private class BaseModel
    {
        public string Parent { get; set; } = string.Empty;
    }

    private sealed class DerivedModel : BaseModel
    {
        public string Child { get; set; } = string.Empty;
    }
}
