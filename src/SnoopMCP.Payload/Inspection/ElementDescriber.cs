// ElementDescriber.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Inspection;

using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using PathStrings;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Builds a <see cref="DescribeElementResponse"/> snapshot for a single
/// <see cref="DependencyObject"/>: identity, bounds, visible text, binding state, and canonical path.
/// </summary>
public sealed class ElementDescriber
{
    private const int VisibleTextCharCap = 200;
    private const string EllipsisChar = "…";

    private readonly ElementRegistry mRegistry;
    private readonly PathStringEmitter mEmitter;

    /// <summary>
    /// Initialises a new <see cref="ElementDescriber"/>.
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

    /// <summary>
    /// Describes <paramref name="element"/> as a self-contained snapshot suitable for wire transport.
    /// </summary>
    /// <param name="element">The element to describe. Must not be <c>null</c>.</param>
    /// <returns>A populated <see cref="DescribeElementResponse"/>.</returns>
    public DescribeElementResponse Describe(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        int id = mRegistry.GetOrAssign(element);
        string typeName = element.GetType().Name;
        string? name = (element as FrameworkElement)?.Name;
        string? automationId = AutomationProperties.GetAutomationId(element);
        BoundsDto bounds = ComputeBounds(element);
        string visibleText = ExtractVisibleText(element);
        bool isInTemplate = ResolveTemplatedParent(element) is not null;
        bool hasBindingErrors = AnyBindingHasError(element);
        string path = mEmitter.Emit(element);
        int childCount = SafeChildCount(element);
        string? dataContextType = (element as FrameworkElement)?.DataContext?.GetType().FullName;
        int hashCode = RuntimeHelpers.GetHashCode(element);

        return new DescribeElementResponse(
            Id: id,
            Type: typeName,
            Name: string.IsNullOrEmpty(name) ? null : name,
            AutomationId: string.IsNullOrEmpty(automationId) ? null : automationId,
            Bounds: bounds,
            VisibleText: visibleText,
            IsInTemplate: isInTemplate,
            HasBindingErrors: hasBindingErrors,
            Path: path,
            ChildCount: childCount,
            DataContextType: dataContextType,
            HashCode: hashCode,
            IsAlive: true);
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
        int count = 0;
        bool isVisual = element is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            count = VisualTreeHelper.GetChildrenCount(element);
        }
        return count;
    }

    private static BoundsDto ComputeBounds(DependencyObject element)
    {
        BoundsDto bounds = new(0, 0, 0, 0);
        if (element is UIElement { IsArrangeValid: true } ui)
        {
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
        }
        return bounds;
    }

    private static Visual? FindRootVisual(Visual start)
    {
        DependencyObject? current = start;
        DependencyObject lastVisual = start;
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
        string trimmed = builder.ToString().Trim();
        string result = trimmed.Length > VisibleTextCharCap
            ? trimmed[..VisibleTextCharCap] + EllipsisChar
            : trimmed;
        return result;
    }

    private static void AppendVisibleText(DependencyObject element, StringBuilder builder)
    {
        bool budgetExceeded = builder.Length >= VisibleTextCharCap;
        if (!budgetExceeded)
        {
            AppendOwnText(element, builder);

            bool isVisual = element is Visual or System.Windows.Media.Media3D.Visual3D;
            if (isVisual)
            {
                int childCount = VisualTreeHelper.GetChildrenCount(element);
                for (int i = 0; i < childCount; i++)
                {
                    AppendVisibleText(VisualTreeHelper.GetChild(element, i), builder);
                }
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
            case ContentControl { Content: string s } when !string.IsNullOrEmpty(s):
                AppendSpaced(builder, s);
                break;
        }
    }

    private static void AppendSpaced(StringBuilder builder, string fragment)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }
        builder.Append(fragment);
    }

    private static bool AnyBindingHasError(DependencyObject element)
    {
        bool anyError = false;
        LocalValueEnumerator enumerator = element.GetLocalValueEnumerator();
        while (enumerator.MoveNext() && !anyError)
        {
            LocalValueEntry entry = enumerator.Current;
            BindingExpressionBase? expr = BindingOperations.GetBindingExpressionBase(element, entry.Property);
            if (expr is not null)
            {
                bool errored = expr.HasError
                    || expr.Status == BindingStatus.PathError
                    || expr.Status == BindingStatus.UpdateSourceError
                    || expr.Status == BindingStatus.UpdateTargetError;
                if (errored)
                {
                    anyError = true;
                }
            }
        }
        return anyError;
    }
}
