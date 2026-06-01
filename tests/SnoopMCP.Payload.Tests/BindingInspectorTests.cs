// BindingInspectorTests.cs
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

using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using Xunit;

#endregion

namespace SnoopMCP.Payload.Tests;

public sealed class BindingInspectorTests
{
    private sealed class Source : INotifyPropertyChanged
    {
        public string Value { get; set; } = string.Empty;

#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
    }

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

        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(() => inspector.Inspect(text, "NoSuchProperty"));
        Assert.Equal(ErrorCode.InvalidArgument, ex.Code);
    }

    [StaFact]
    public void Inspect_NullElement_Throws()
    {
        BindingInspector inspector = CreateInspector();
        Assert.Throws<ArgumentNullException>(() => inspector.Inspect(null!, "Text"));
    }
}
