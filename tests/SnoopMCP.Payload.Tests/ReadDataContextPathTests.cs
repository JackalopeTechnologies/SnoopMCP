// ReadDataContextPathTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Controls;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class ReadDataContextPathTests
{
    [StaFact]
    public void ReadPath_NullDataContext_PathReachableFalse()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid();

        ReadDataContextPathResponse response = inspector.ReadPath(grid, "Anything");

        Assert.False(response.PathReachable);
        Assert.Equal(string.Empty, response.FailureAt);
    }

    [StaFact]
    public void ReadPath_SimpleProperty_ReturnsValue()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new Model { Name = "Alice" } };

        ReadDataContextPathResponse response = inspector.ReadPath(grid, "Name");

        Assert.True(response.PathReachable);
        Assert.Equal("Alice", response.Value);
        Assert.Equal(typeof(string).FullName, response.ValueType);
    }

    [StaFact]
    public void ReadPath_DottedPath_WalksChain()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid
        {
            DataContext = new Model
            {
                Inner = new Inner { Label = "Deep" }
            }
        };

        ReadDataContextPathResponse response = inspector.ReadPath(grid, "Inner.Label");

        Assert.True(response.PathReachable);
        Assert.Equal("Deep", response.Value);
    }

    [StaFact]
    public void ReadPath_NullIntermediate_FailureAtSegmentBefore()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new Model { Inner = null } };

        ReadDataContextPathResponse response = inspector.ReadPath(grid, "Inner.Label");

        Assert.False(response.PathReachable);
        Assert.Equal("Inner", response.FailureAt);
    }

    [StaFact]
    public void ReadPath_UnknownProperty_FailureAtIncludesSegment()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new Model() };

        ReadDataContextPathResponse response = inspector.ReadPath(grid, "DoesNotExist");

        Assert.False(response.PathReachable);
        Assert.Equal("DoesNotExist", response.FailureAt);
    }

    [StaFact]
    public void ReadPath_IntegerValue_StringifiedInvariant()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new Model { Age = 42 } };

        ReadDataContextPathResponse response = inspector.ReadPath(grid, "Age");

        Assert.True(response.PathReachable);
        Assert.Equal("42", response.Value);
        Assert.Equal(typeof(int).FullName, response.ValueType);
    }

    [StaFact]
    public void ReadPath_NullLeafValue_PathReachableTrue_ValueNull()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new Model { Name = null! } };

        ReadDataContextPathResponse response = inspector.ReadPath(grid, "Name");

        Assert.True(response.PathReachable);
        Assert.Null(response.Value);
        Assert.Null(response.ValueType);
    }

    [StaFact]
    public void ReadPath_EmptyPath_Throws()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new Model() };

        Assert.Throws<ArgumentException>(() => inspector.ReadPath(grid, ""));
    }

    [StaFact]
    public void ReadPath_NullPath_Throws()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new Model() };

        Assert.Throws<ArgumentNullException>(() => inspector.ReadPath(grid, null!));
    }

    [StaFact]
    public void ReadPath_NullElement_Throws()
    {
        var inspector = new DataContextInspector();
        Assert.Throws<ArgumentNullException>(() => inspector.ReadPath(null!, "X"));
    }

    private sealed class Model
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public Inner? Inner { get; set; }
    }

    private sealed class Inner
    {
        public string Label { get; set; } = string.Empty;
    }
}
