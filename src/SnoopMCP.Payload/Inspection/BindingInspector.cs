// BindingInspector.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Inspection;

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Payload;
using Protocol.Errors;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Inspects the state of WPF data bindings. <see cref="Inspect"/> (Task 23) is the deep dive on one
/// binding — its <c>BindingExpression</c> state, source, path, mode, and current value.
/// <see cref="ListBindings"/> (Task 24) is the broad audit — every binding on an element, optionally
/// across its visual subtree, each summary stamped with a stable element id the LLM can drill into.
/// </summary>
public sealed class BindingInspector
{
    private const string StateNoBinding = "NoBinding";
    private const string StateActive = "Active";
    private const string StateInactive = "Inactive";
    private const string StateDetached = "Detached";
    private const string StateAsyncRequestPending = "AsyncRequestPending";
    private const string StatePathError = "PathError";
    private const string StateUpdateSourceError = "UpdateSourceError";
    private const string StateUpdateTargetError = "UpdateTargetError";
    private const string StateUnattached = "Unattached";
    private const string DpFieldSuffix = "Property";

    private readonly ElementRegistry mRegistry;

    /// <summary>
    /// Initialises a new <see cref="BindingInspector"/>.
    /// </summary>
    /// <param name="registry">
    /// Element registry used by <c>ListBindings</c> (Task 24) to assign stable ids to binding
    /// summaries. Validated and held here so the dependency contract is enforced at construction.
    /// </param>
    public BindingInspector(ElementRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        mRegistry = registry;
    }

    /// <summary>
    /// Inspects the <c>BindingExpression</c> on a single dependency property and reports its live
    /// state — path, mode, resolved source, current value, and status.
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <param name="propertyName">The dependency property's registered name. Must be non-empty.</param>
    /// <returns>The binding's path, mode, resolved source, current value, and state.</returns>
    /// <exception cref="SnoopMcpException">
    /// Thrown with <see cref="ErrorCode.InvalidArgument"/> when the property is not found on the element.
    /// </exception>
    /// <remarks>
    /// CA1822 disabled: <c>Inspect</c> needs no instance state, but the class is intentionally an
    /// instance type because <c>ListBindings</c> (Task 24) shares the same instance and uses the
    /// registry held in <c>mRegistry</c>.
    /// </remarks>
#pragma warning disable CA1822
    public InspectBindingResponse Inspect(DependencyObject element, string propertyName)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);

        DependencyProperty dp = ResolveDp(element.GetType(), propertyName)
            ?? throw new SnoopMcpException(
                ErrorCode.InvalidArgument,
                $"Dependency property '{propertyName}' not found on {element.GetType().FullName}.");

        BindingExpressionBase? expression = BindingOperations.GetBindingExpressionBase(element, dp);
        InspectBindingResponse response;
        if (expression is null)
        {
            response = new InspectBindingResponse(
                BindingPath: null,
                Mode: null,
                ResolvedSourceType: null,
                ResolvedSourceHashCode: null,
                CurrentValue: null,
                State: StateNoBinding,
                RecentTraceLines: []);
        }
        else
        {
            response = BuildResponse(element, dp, expression);
        }

        return response;
    }

    /// <summary>
    /// Audits every dependency property carrying a <c>BindingExpression</c> on
    /// <paramref name="element"/>, and — when <paramref name="includeDescendants"/> is <c>true</c> —
    /// across its visual subtree. Each row carries the owning element's stable id and type.
    /// </summary>
    /// <param name="element">The element at which the audit begins.</param>
    /// <param name="includeDescendants">When <c>true</c>, recurses through the visual subtree.</param>
    /// <returns>Every binding found, summarised.</returns>
    public ListBindingsResponse ListBindings(DependencyObject element, bool includeDescendants)
    {
        ArgumentNullException.ThrowIfNull(element);
        var sink = new List<BindingSummaryDto>();
        Walk(element, includeDescendants, sink);
        return new ListBindingsResponse(sink);
    }

    private void Walk(DependencyObject element, bool includeDescendants, List<BindingSummaryDto> sink)
    {
        CollectFrom(element, sink);
        bool recurse = includeDescendants && element is Visual or Visual3D;
        if (recurse)
        {
            int count = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
            {
                Walk(VisualTreeHelper.GetChild(element, i), true, sink);
            }
        }
    }

    private void CollectFrom(DependencyObject element, List<BindingSummaryDto> sink)
    {
        int elementId = mRegistry.GetOrAssign(element);
        string elementType = element.GetType().Name;
        LocalValueEnumerator enumerator = element.GetLocalValueEnumerator();
        while (enumerator.MoveNext())
        {
            DependencyProperty dp = enumerator.Current.Property;
            BindingExpressionBase? expression = BindingOperations.GetBindingExpressionBase(element, dp);
            if (expression is not null)
            {
                sink.Add(Summarize(element, dp, expression, elementId, elementType));
            }
        }
    }

    private static BindingSummaryDto Summarize(
        DependencyObject element,
        DependencyProperty dp,
        BindingExpressionBase expression,
        int elementId,
        string elementType)
    {
        var typed = expression as BindingExpression;
        Binding? binding = typed?.ParentBinding;
        object? currentValue = element.GetValue(dp);

        return new BindingSummaryDto(
            ElementId: elementId,
            ElementType: elementType,
            Property: dp.Name,
            BindingPath: binding?.Path?.Path,
            Mode: binding?.Mode.ToString(),
            State: MapState(expression),
            HasError: expression.HasError,
            ResolvedSourceType: typed?.ResolvedSource?.GetType().FullName,
            CurrentValue: Convert.ToString(currentValue, CultureInfo.InvariantCulture));
    }

    private static InspectBindingResponse BuildResponse(
        DependencyObject element,
        DependencyProperty dp,
        BindingExpressionBase expression)
    {
        var typed = expression as BindingExpression;
        Binding? binding = typed?.ParentBinding;
        string state = MapState(expression);
        object? currentValue = element.GetValue(dp);

        return new InspectBindingResponse(
            BindingPath: binding?.Path?.Path,
            Mode: binding?.Mode.ToString(),
            ResolvedSourceType: typed?.ResolvedSource?.GetType().FullName,
            ResolvedSourceHashCode: typed?.ResolvedSource is null
                ? null
                : RuntimeHelpers.GetHashCode(typed.ResolvedSource),
            CurrentValue: Convert.ToString(currentValue, CultureInfo.InvariantCulture),
            State: state,
            RecentTraceLines: []);
    }

    private static string MapState(BindingExpressionBase expression)
    {
        string state = expression.Status switch
        {
            BindingStatus.Active => StateActive,
            BindingStatus.Inactive => StateInactive,
            BindingStatus.Detached => StateDetached,
            BindingStatus.AsyncRequestPending => StateAsyncRequestPending,
            BindingStatus.PathError => StatePathError,
            BindingStatus.UpdateSourceError => StateUpdateSourceError,
            BindingStatus.UpdateTargetError => StateUpdateTargetError,
            BindingStatus.Unattached => StateUnattached,
            _ => expression.Status.ToString()
        };
        return state;
    }

    private static DependencyProperty? ResolveDp(Type ownerType, string propertyName)
    {
        DependencyProperty? found = null;
        string fieldName = propertyName + DpFieldSuffix;
        Type? current = ownerType;
        while (current is not null && found is null)
        {
            FieldInfo? field = current.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            bool isDpField = field is not null && typeof(DependencyProperty).IsAssignableFrom(field.FieldType);
            if (isDpField && field is not null)
            {
                found = field.GetValue(null) as DependencyProperty;
            }

            current = current.BaseType;
        }

        return found;
    }
}
