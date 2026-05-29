// BindingInspectorTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload.Tests;

using System.Windows.Controls;
using System.Windows.Data;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class BindingInspectorTests
{
    private static BindingInspector CreateInspector()
    {
        return new BindingInspector(new ElementRegistry());
    }

    [StaFact]
    public void Inspect_NoBinding_StateIsNoBinding()
    {
        BindingInspector inspector = CreateInspector();
        var text = new TextBlock { Text = "literal" };

        InspectBindingResponse response = inspector.Inspect(text, "Text");

        Assert.Equal("NoBinding", response.State);
        Assert.Null(response.BindingPath);
    }

    [StaFact]
    public void Inspect_ActiveBinding_StateIsActive()
    {
        BindingInspector inspector = CreateInspector();
        var source = new Source { Value = "live" };
        var text = new TextBlock();
        BindingOperations.SetBinding(text, TextBlock.TextProperty, new Binding("Value") { Source = source });

        InspectBindingResponse response = inspector.Inspect(text, "Text");

        Assert.Equal("Active", response.State);
        Assert.Equal("Value", response.BindingPath);
    }

    [StaFact]
    public void Inspect_BindingMode_Reported()
    {
        BindingInspector inspector = CreateInspector();
        var source = new Source();
        var text = new TextBlock();
        BindingOperations.SetBinding(
            text,
            TextBlock.TextProperty,
            new Binding("Value") { Source = source, Mode = BindingMode.TwoWay });

        InspectBindingResponse response = inspector.Inspect(text, "Text");

        Assert.Equal("TwoWay", response.Mode);
    }

    [StaFact]
    public void Inspect_ResolvedSourceType_Reported()
    {
        BindingInspector inspector = CreateInspector();
        var source = new Source { Value = "x" };
        var text = new TextBlock();
        BindingOperations.SetBinding(text, TextBlock.TextProperty, new Binding("Value") { Source = source });

        InspectBindingResponse response = inspector.Inspect(text, "Text");

        Assert.Equal(typeof(Source).FullName, response.ResolvedSourceType);
        Assert.NotNull(response.ResolvedSourceHashCode);
    }

    [StaFact]
    public void Inspect_BrokenBindingPath_StateIsPathError()
    {
        BindingInspector inspector = CreateInspector();
        var source = new Source { Value = "x" };
        var text = new TextBlock();
        BindingOperations.SetBinding(
            text,
            TextBlock.TextProperty,
            new Binding("DoesNotExist") { Source = source });

        InspectBindingResponse response = inspector.Inspect(text, "Text");

        Assert.NotEqual("Active", response.State);
    }

    [StaFact]
    public void Inspect_UnknownProperty_ThrowsInvalidArgument()
    {
        BindingInspector inspector = CreateInspector();
        var text = new TextBlock();

        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(
            () => inspector.Inspect(text, "NoSuchProperty"));
        Assert.Equal(ErrorCode.InvalidArgument, ex.Code);
    }

    [StaFact]
    public void Inspect_NullElement_Throws()
    {
        BindingInspector inspector = CreateInspector();
        Assert.Throws<ArgumentNullException>(() => inspector.Inspect(null!, "Text"));
    }

    private sealed class Source : System.ComponentModel.INotifyPropertyChanged
    {
        public string Value { get; set; } = string.Empty;

#pragma warning disable CS0067
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
    }
}
