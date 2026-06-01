// ElementDescriber.cs
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

using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;

#endregion

namespace SnoopMCP.Payload.Inspection;

/// <summary>
///     Builds a <see cref="DescribeElementResponse" /> snapshot for a single
///     <see cref="DependencyObject" />: identity, bounds, visible text, binding state, and canonical path.
/// </summary>
public sealed class ElementDescriber
{
    /// <summary>
    ///     Initialises a new <see cref="ElementDescriber" />.
    /// </summary>
    /// <param name="registry">The element registry that assigns stable ids.</param>
    /// <param name="emitter">The path emitter (Task 9) that produces canonical path strings.</param>
    public ElementDescriber(ElementRegistry registry, PathStringEmitter emitter)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(emitter);
        mRegistry = registry;
        mEmitter = emitter;
    }

    private readonly PathStringEmitter mEmitter;

    private readonly ElementRegistry mRegistry;

    /// <summary>
    ///     Describes <paramref name="element" /> as a self-contained snapshot suitable for wire transport.
    /// </summary>
    /// <param name="element">The element to describe. Must not be <c>null</c>.</param>
    /// <returns>A populated <see cref="DescribeElementResponse" />.</returns>
    public DescribeElementResponse Describe(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var id = mRegistry.GetOrAssign(element);
        var typeName = element.GetType().Name;
        var name = (element as FrameworkElement)?.Name;
        var automationId = AutomationProperties.GetAutomationId(element);
        BoundsDto bounds = ComputeBounds(element);
        var visibleText = ExtractVisibleText(element);
        var isInTemplate = ResolveTemplatedParent(element) is not null;
        var hasBindingErrors = AnyBindingHasError(element);
        var path = mEmitter.Emit(element);
        var childCount = SafeChildCount(element);
        var dataContextType = (element as FrameworkElement)?.DataContext?.GetType().FullName;
        var hashCode = RuntimeHelpers.GetHashCode(element);

        return new DescribeElementResponse(
            id,
            typeName,
            string.IsNullOrEmpty(name) ? null : name,
            string.IsNullOrEmpty(automationId) ? null : automationId,
            bounds,
            visibleText,
            isInTemplate,
            hasBindingErrors,
            path,
            childCount,
            dataContextType,
            hashCode,
            true);
    }

    private static DependencyObject? ResolveTemplatedParent(DependencyObject element)
    {
        DependencyObject? templated = element switch
        {
            FrameworkElement fe => fe.TemplatedParent,
            FrameworkContentElement fce => fce.TemplatedParent,
            _ => null
        };
        return templated;
    }

    private static int SafeChildCount(DependencyObject element)
    {
        var count = 0;
        var isVisual = element is Visual or Visual3D;
        if (isVisual) count = VisualTreeHelper.GetChildrenCount(element);
        return count;
    }

    private static BoundsDto ComputeBounds(DependencyObject element)
    {
        BoundsDto bounds = new(0, 0, 0, 0);
        if (element is UIElement ui && ui.IsArrangeValid)
            try
            {
                Visual? rootVisual = FindRootVisual(ui);
                if (rootVisual is not null)
                {
                    GeneralTransform transform = ui.TransformToAncestor(rootVisual);
                    Rect rect = transform.TransformBounds(new Rect(new Point(0, 0), ui.RenderSize));
                    bounds = new BoundsDto(rect.X, rect.Y, rect.Width, rect.Height);
                }
            }
            catch (InvalidOperationException)
            {
            }

        return bounds;
    }

    private static Visual? FindRootVisual(Visual start)
    {
        DependencyObject? current = start;
        DependencyObject? lastVisual = start;
        while (current is not null)
        {
            lastVisual = current;
            current = VisualTreeHelper.GetParent(current);
        }

        return lastVisual as Visual;
    }

    private static string ExtractVisibleText(DependencyObject element)
    {
        var builder = new StringBuilder();
        AppendVisibleText(element, builder);
        var trimmed = builder.ToString().Trim();
        var result = trimmed.Length > VisibleTextCharCap
            ? trimmed[..VisibleTextCharCap] + EllipsisChar
            : trimmed;
        return result;
    }

    private static void AppendVisibleText(DependencyObject element, StringBuilder builder)
    {
        var budgetExceeded = builder.Length >= VisibleTextCharCap;
        if (!budgetExceeded)
        {
            AppendOwnText(element, builder);

            var isVisual = element is Visual or Visual3D;
            if (isVisual)
            {
                var childCount = VisualTreeHelper.GetChildrenCount(element);
                for (var i = 0; i < childCount; i++) AppendVisibleText(VisualTreeHelper.GetChild(element, i), builder);
            }
        }
    }

    private static void AppendOwnText(DependencyObject element, StringBuilder builder)
    {
        switch (element)
        {
            case TextBlock tb when !string.IsNullOrEmpty(tb.Text):
                AppendSpaced(builder, tb.Text);
                break;
            case TextBox tx when !string.IsNullOrEmpty(tx.Text):
                AppendSpaced(builder, tx.Text);
                break;
            case ContentControl cc when cc.Content is string s && !string.IsNullOrEmpty(s):
                AppendSpaced(builder, s);
                break;
        }
    }

    private static void AppendSpaced(StringBuilder builder, string fragment)
    {
        if (builder.Length > 0) builder.Append(' ');
        builder.Append(fragment);
    }

    private static bool AnyBindingHasError(DependencyObject element)
    {
        var anyError = false;
        LocalValueEnumerator enumerator = element.GetLocalValueEnumerator();
        while (enumerator.MoveNext() && !anyError)
        {
            LocalValueEntry entry = enumerator.Current;
            BindingExpressionBase? expr = BindingOperations.GetBindingExpressionBase(element, entry.Property);
            if (expr is not null)
            {
                var errored = expr.HasError
                              || expr.Status == BindingStatus.PathError
                              || expr.Status == BindingStatus.UpdateSourceError
                              || expr.Status == BindingStatus.UpdateTargetError;
                if (errored) anyError = true;
            }
        }

        return anyError;
    }

    private const int VisibleTextCharCap = 200;
    private const string EllipsisChar = "…";
}
