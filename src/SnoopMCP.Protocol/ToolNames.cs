// ToolNames.cs
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

namespace SnoopMCP.Protocol;

/// <summary>
/// Canonical wire tool-name strings shared by the in-process payload (which registers handlers
/// under these names) and the MCP host (which dispatches to them). Both sides agree on the string;
/// these constants keep the host free of magic-string literals.
/// </summary>
public static class ToolNames
{
    /// <summary>Connectivity smoke-test handler that echoes its arguments back.</summary>
    public const string Echo = "echo";

    /// <summary>Describe an element by id.</summary>
    public const string DescribeElement = "describeElement";

    /// <summary>Enumerate every active visual root (window, popup, etc.).</summary>
    public const string ListVisualRoots = "listVisualRoots";

    /// <summary>Enumerate visual or logical children of an element.</summary>
    public const string GetChildren = "getChildren";

    /// <summary>Get the visual or logical parent of an element.</summary>
    public const string GetParent = "getParent";

    /// <summary>Get the templated parent of an element, if any.</summary>
    public const string GetTemplatedParent = "getTemplatedParent";

    /// <summary>Find elements under a root matching an AND-combined predicate.</summary>
    public const string FindElements = "findElements";

    /// <summary>Hit test a root-relative point and return the deepest visual.</summary>
    public const string HitTest = "hitTest";

    /// <summary>Resolve a canonical path string back to a live element.</summary>
    public const string ResolvePath = "resolvePath";

    /// <summary>Describe the CLR type shape of an element's DataContext.</summary>
    public const string DescribeDataContext = "describeDataContext";

    /// <summary>Read a dotted property path off an element's DataContext.</summary>
    public const string ReadDataContextPath = "readDataContextPath";

    /// <summary>List the dependency properties reachable on an element.</summary>
    public const string ListDependencyProperties = "listDependencyProperties";

    /// <summary>Get a dependency property's value and precedence trace.</summary>
    public const string GetDependencyProperty = "getDependencyProperty";

    /// <summary>Resolve the applied Style and its BasedOn chain.</summary>
    public const string ResolveStyle = "resolveStyle";

    /// <summary>Resolve the applied ControlTemplate.</summary>
    public const string ResolveTemplate = "resolveTemplate";

    /// <summary>Inspect the BindingExpression on a property.</summary>
    public const string InspectBinding = "inspectBinding";

    /// <summary>List every BindingExpression on an element (and optionally its descendants).</summary>
    public const string ListBindings = "listBindings";

    /// <summary>Serialize an element to XAML reflecting its current live state.</summary>
    public const string ExportXaml = "exportXaml";
}
