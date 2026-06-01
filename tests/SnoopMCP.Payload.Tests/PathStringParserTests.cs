// PathStringParserTests.cs
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

using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Errors;
using Xunit;

#endregion

namespace SnoopMCP.Payload.Tests;

public sealed class PathStringParserTests
{
    [Fact]
    public void Parse_SingleStep_NoPredicates()
    {
        var parser = new PathStringParser();
        IReadOnlyList<PathStep> steps = parser.Parse("/Window");

        Assert.Single(steps);
        Assert.Equal("Window", steps[0].TypeName);
        Assert.Empty(steps[0].Attributes);
        Assert.Null(steps[0].Index);
    }

    [Fact]
    public void Parse_MultipleSteps_NoPredicates()
    {
        var parser = new PathStringParser();
        IReadOnlyList<PathStep> steps = parser.Parse("/Window/Grid/StackPanel/Button");

        Assert.Equal(4, steps.Count);
        Assert.Equal("Window", steps[0].TypeName);
        Assert.Equal("Grid", steps[1].TypeName);
        Assert.Equal("StackPanel", steps[2].TypeName);
        Assert.Equal("Button", steps[3].TypeName);
    }

    [Fact]
    public void Parse_StepWithNameAttribute()
    {
        var parser = new PathStringParser();
        IReadOnlyList<PathStep> steps = parser.Parse("/Window[Name='Main']");

        Assert.Single(steps);
        Assert.Equal("Window", steps[0].TypeName);
        Assert.Equal("Main", steps[0].Attributes["Name"]);
    }

    [Fact]
    public void Parse_StepWithMultipleAttributes()
    {
        var parser = new PathStringParser();
        IReadOnlyList<PathStep> steps = parser.Parse("/Button[Name='Save', AutomationId='SaveBtn']");

        Assert.Single(steps);
        Assert.Equal("Save", steps[0].Attributes["Name"]);
        Assert.Equal("SaveBtn", steps[0].Attributes["AutomationId"]);
    }

    [Fact]
    public void Parse_StepWithIndex()
    {
        var parser = new PathStringParser();
        IReadOnlyList<PathStep> steps = parser.Parse("/StackPanel/Button[2]");

        Assert.Equal(2, steps.Count);
        Assert.Equal(2, steps[1].Index);
    }

    [Fact]
    public void Parse_StepWithAttributesAndIndex()
    {
        var parser = new PathStringParser();
        IReadOnlyList<PathStep> steps = parser.Parse("/Button[Name='Save'][1]");

        Assert.Single(steps);
        Assert.Equal("Save", steps[0].Attributes["Name"]);
        Assert.Equal(1, steps[0].Index);
    }

    [Fact]
    public void Parse_EmptyString_Throws()
    {
        var parser = new PathStringParser();
        Assert.Throws<SnoopMcpException>(() => parser.Parse(""));
    }

    [Fact]
    public void Parse_MissingLeadingSlash_Throws()
    {
        var parser = new PathStringParser();
        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(() => parser.Parse("Window/Grid"));
        Assert.Equal(ErrorCode.PathParseError, ex.Code);
    }

    [Fact]
    public void Parse_UnclosedBracket_Throws()
    {
        var parser = new PathStringParser();
        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(() => parser.Parse("/Button[Name='Save'"));
        Assert.Equal(ErrorCode.PathParseError, ex.Code);
    }
}
