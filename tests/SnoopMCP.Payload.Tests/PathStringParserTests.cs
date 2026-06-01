// PathStringParserTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Errors;
using Xunit;

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
        var ex = Assert.Throws<SnoopMcpException>(() => parser.Parse("Window/Grid"));
        Assert.Equal(ErrorCode.PathParseError, ex.Code);
    }

    [Fact]
    public void Parse_UnclosedBracket_Throws()
    {
        var parser = new PathStringParser();
        var ex = Assert.Throws<SnoopMcpException>(() => parser.Parse("/Button[Name='Save'"));
        Assert.Equal(ErrorCode.PathParseError, ex.Code);
    }
}
