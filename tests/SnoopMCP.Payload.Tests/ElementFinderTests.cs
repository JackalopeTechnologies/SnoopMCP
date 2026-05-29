// ElementFinderTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Controls;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class ElementFinderTests
{
    private static ElementFinder CreateFinder(ElementRegistry registry)
    {
        var emitter = new PathStringEmitter();
        var describer = new ElementDescriber(registry, emitter);
        return new ElementFinder(describer);
    }

    [StaFact]
    public void Find_ByType_MatchesSubstringOfFullTypeName()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var grid = new Grid();
        grid.Children.Add(new Button());
        grid.Children.Add(new TextBlock());

        var predicate = new ElementPredicateDto { Type = "Button" };

        FindElementsResponse response = finder.Find(grid, predicate);

        Assert.Single(response.Matches);
        Assert.Equal("Button", response.Matches[0].Type);
    }

    [StaFact]
    public void Find_ByName_ExactMatch()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var grid = new Grid();
        grid.Children.Add(new Button { Name = "Save" });
        grid.Children.Add(new Button { Name = "Cancel" });

        var predicate = new ElementPredicateDto { Name = "Cancel" };

        FindElementsResponse response = finder.Find(grid, predicate);

        Assert.Single(response.Matches);
        Assert.Equal("Cancel", response.Matches[0].Name);
    }

    [StaFact]
    public void Find_ByAutomationId_ExactMatch()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var grid = new Grid();
        var tagged = new Button();
        System.Windows.Automation.AutomationProperties.SetAutomationId(tagged, "ThemePicker");
        grid.Children.Add(tagged);
        grid.Children.Add(new Button());

        var predicate = new ElementPredicateDto { AutomationId = "ThemePicker" };

        FindElementsResponse response = finder.Find(grid, predicate);

        Assert.Single(response.Matches);
        Assert.Equal("ThemePicker", response.Matches[0].AutomationId);
    }

    [StaFact]
    public void Find_ByTextContains_CaseInsensitive()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = "Hello World" });
        stack.Children.Add(new TextBlock { Text = "Goodbye" });

        var predicate = new ElementPredicateDto { TextContains = "hello" };

        FindElementsResponse response = finder.Find(stack, predicate);

        Assert.Contains(response.Matches, m => m.VisibleText.Contains("Hello"));
        Assert.DoesNotContain(response.Matches, m => m.VisibleText == "Goodbye");
    }

    [StaFact]
    public void Find_ByPropertyEquals_StringifiedComparison()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var grid = new Grid();
        grid.Children.Add(new Button { IsEnabled = false });
        grid.Children.Add(new Button { IsEnabled = true });

        var predicate = new ElementPredicateDto
        {
            PropertyEquals = new PropertyEqualsDto("IsEnabled", "False")
        };

        FindElementsResponse response = finder.Find(grid, predicate);

        Assert.Single(response.Matches);
    }

    [StaFact]
    public void Find_ByPropertyEquals_UnknownPropertyMatchesNothing()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var grid = new Grid();
        grid.Children.Add(new Button());

        var predicate = new ElementPredicateDto
        {
            PropertyEquals = new PropertyEqualsDto("ThisDoesNotExist", "anything")
        };

        FindElementsResponse response = finder.Find(grid, predicate);

        Assert.Empty(response.Matches);
    }

    [StaFact]
    public void Find_ByMultiplePredicates_AndCombined()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var grid = new Grid();
        grid.Children.Add(new Button { Name = "Save" });
        grid.Children.Add(new TextBlock { Name = "Save" });

        var predicate = new ElementPredicateDto
        {
            Type = "Button",
            Name = "Save"
        };

        FindElementsResponse response = finder.Find(grid, predicate);

        Assert.Single(response.Matches);
        Assert.Equal("Button", response.Matches[0].Type);
        Assert.Equal("Save", response.Matches[0].Name);
    }

    [StaFact]
    public void Find_HasAncestor_MatchesWhenAnyAncestorMatches()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var outer = new Grid { Name = "Outer" };
        var inner = new StackPanel();
        var deep = new Button { Name = "Deep" };
        outer.Children.Add(inner);
        inner.Children.Add(deep);

        var predicate = new ElementPredicateDto
        {
            Type = "Button",
            HasAncestor = new ElementPredicateDto { Name = "Outer" }
        };

        FindElementsResponse response = finder.Find(outer, predicate);

        Assert.Single(response.Matches);
        Assert.Equal("Deep", response.Matches[0].Name);
    }

    [StaFact]
    public void Find_HasAncestor_NoMatchWhenAncestorMissing()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var outer = new Grid { Name = "Different" };
        outer.Children.Add(new Button { Name = "B" });

        var predicate = new ElementPredicateDto
        {
            Type = "Button",
            HasAncestor = new ElementPredicateDto { Name = "MissingAncestor" }
        };

        FindElementsResponse response = finder.Find(outer, predicate);

        Assert.Empty(response.Matches);
    }

    [StaFact]
    public void Find_HasDescendant_MatchesWhenAnyDescendantMatches()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var root = new Grid();
        var holder = new StackPanel { Name = "HasButton" };
        holder.Children.Add(new Button { Name = "TheButton" });
        var empty = new StackPanel { Name = "Empty" };
        root.Children.Add(holder);
        root.Children.Add(empty);

        var predicate = new ElementPredicateDto
        {
            Type = "StackPanel",
            HasDescendant = new ElementPredicateDto { Name = "TheButton" }
        };

        FindElementsResponse response = finder.Find(root, predicate);

        Assert.Single(response.Matches);
        Assert.Equal("HasButton", response.Matches[0].Name);
    }

    [StaFact]
    public void Find_InTemplateOf_MatchesElementsInsideMatchingTemplatedHost()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var host = new Button { Name = "Host", Content = "x" };
        host.ApplyTemplate();
        var page = new Grid();
        page.Children.Add(host);

        var predicate = new ElementPredicateDto
        {
            InTemplateOf = new ElementPredicateDto { Name = "Host" }
        };

        FindElementsResponse response = finder.Find(page, predicate);

        Assert.NotEmpty(response.Matches);
        foreach (DescribeElementResponse match in response.Matches)
        {
            Assert.True(
                match.IsInTemplate,
                $"Match {match.Type} (id={match.Id}) was expected to be in template.");
        }
    }

    [StaFact]
    public void Find_EmptyPredicate_ReturnsRootAndDescendants()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var grid = new Grid();
        grid.Children.Add(new Button());
        grid.Children.Add(new TextBlock());

        FindElementsResponse response = finder.Find(grid, new ElementPredicateDto());

        Assert.True(
            response.Matches.Count >= 3,
            $"Expected root + 2 children = at least 3 matches; got {response.Matches.Count}.");
    }

    [StaFact]
    public void Find_NullRoot_Throws()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);

        Assert.Throws<ArgumentNullException>(() => finder.Find(null!, new ElementPredicateDto()));
    }

    [StaFact]
    public void Find_NullPredicate_Throws()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var grid = new Grid();

        Assert.Throws<ArgumentNullException>(() => finder.Find(grid, null!));
    }
}
