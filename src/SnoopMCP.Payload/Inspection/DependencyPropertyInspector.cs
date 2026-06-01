// DependencyPropertyInspector.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Inspection;

using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Enumerates the dependency properties reachable on an element (Task 19) and reads a single
/// property's effective value with a best-effort precedence trace (Task 20). The two tools share
/// a class because they share the "introspect dependency properties" intent.
/// </summary>
public sealed class DependencyPropertyInspector
{
    private const string DpFieldSuffix = "Property";
    private const string LocalSource = "Local";
    private const string StyleSetterSource = "StyleSetter";
    private const string InheritedSource = "Inherited";
    private const string DefaultSource = "Default";
    private const string LocalDescription = "Local value on the element";
    private const string DefaultDescription = "Default value from type metadata";

    /// <summary>
    /// Enumerates every dependency property reachable on an element: those declared on the element's
    /// type chain (<c>isAttached=false</c>) plus any attached property currently set on it
    /// (<c>isAttached=true</c>). Deduplicated by owner-type + name.
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <returns>The reachable dependency properties.</returns>
    /// <remarks>
    /// CA1822 disabled: scaffolded for DI in handler; will gain instance state in follow-up phases.
    /// </remarks>
#pragma warning disable CA1822
    public ListDependencyPropertiesResponse ListProperties(DependencyObject element)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(element);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var dtos = new List<DependencyPropertyDto>();

        CollectTypeWalkDps(element.GetType(), seen, dtos);
        CollectLocalAttachedDps(element, seen, dtos);

        return new ListDependencyPropertiesResponse(dtos);
    }

    /// <summary>
    /// Reads a dependency property's current effective value and builds a best-effort precedence
    /// trace explaining which source won and what the losing candidates were.
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <param name="propertyName">The dependency property's registered name. Must be non-empty.</param>
    /// <returns>The current value, its type, the winning source name, and the precedence trace.</returns>
    /// <exception cref="SnoopMcpException">
    /// Thrown with <see cref="ErrorCode.InvalidArgument"/> when the property is not found on the element.
    /// </exception>
    /// <remarks>
    /// Sources that cannot be traced cheaply in v1 (animation, trigger setters, template parent) are
    /// not enumerated as losing candidates; the winning-source name still identifies which family won.
    /// CA1822 disabled: scaffolded for DI in handler; will gain instance state in follow-up phases.
    /// </remarks>
#pragma warning disable CA1822
    public GetDependencyPropertyResponse GetProperty(DependencyObject element, string propertyName)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);

        DependencyProperty dp = ResolveDp(element.GetType(), propertyName)
            ?? throw new SnoopMcpException(
                ErrorCode.InvalidArgument,
                $"Dependency property '{propertyName}' not found on {element.GetType().FullName}.");

        object? currentValue = element.GetValue(dp);
        string winningSource = DependencyPropertyHelper
            .GetValueSource(element, dp)
            .BaseValueSource
            .ToString();

        var trace = new List<PrecedenceEntryDto>();
        AppendLocalEntry(element, dp, trace);
        AppendStyleSetterEntries(element, dp, trace);
        AppendInheritedEntry(element, dp, trace);
        AppendDefaultEntry(element, dp, trace);

        return new GetDependencyPropertyResponse(
            Name: dp.Name,
            CurrentValue: Stringify(currentValue),
            CurrentValueType: currentValue?.GetType().FullName,
            Precedence: trace,
            WinningSource: winningSource);
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

    private static void AppendLocalEntry(DependencyObject element, DependencyProperty dp, List<PrecedenceEntryDto> trace)
    {
        object localValue = element.ReadLocalValue(dp);
        bool hasLocal = localValue != DependencyProperty.UnsetValue;
        if (hasLocal)
        {
            trace.Add(new PrecedenceEntryDto(
                Source: LocalSource,
                Value: Stringify(localValue),
                ValueType: localValue?.GetType().FullName,
                SourceDescription: LocalDescription));
        }
    }

    private static void AppendStyleSetterEntries(
        DependencyObject element,
        DependencyProperty dp,
        List<PrecedenceEntryDto> trace)
    {
        if (element is FrameworkElement { Style: not null } fe)
        {
            WalkStyleChain(fe.Style, dp, trace);
        }
    }

    private static void WalkStyleChain(Style? start, DependencyProperty dp, List<PrecedenceEntryDto> trace)
    {
        Style? current = start;
        int depth = 0;
        while (current is not null)
        {
            IEnumerable<Setter> matches = current.Setters
                .OfType<Setter>()
                .Where(setter => setter.Property == dp);
            foreach (Setter setter in matches)
            {
                trace.Add(new PrecedenceEntryDto(
                    Source: StyleSetterSource,
                    Value: Stringify(setter.Value),
                    ValueType: setter.Value?.GetType().FullName,
                    SourceDescription: $"Style {DescribeStyle(current)} (chain depth {depth})"));
            }

            current = current.BasedOn;
            depth++;
        }
    }

    private static void AppendInheritedEntry(
        DependencyObject element,
        DependencyProperty dp,
        List<PrecedenceEntryDto> trace)
    {
        PropertyMetadata metadata = dp.GetMetadata(element);
        bool inherits = metadata is FrameworkPropertyMetadata { Inherits: true };
        DependencyObject? parent = inherits ? VisualTreeHelper.GetParent(element) : null;
        if (parent is not null)
        {
            object? parentValue = parent.GetValue(dp);
            bool differsFromDefault = !Equals(parentValue, metadata.DefaultValue);
            if (differsFromDefault)
            {
                trace.Add(new PrecedenceEntryDto(
                    Source: InheritedSource,
                    Value: Stringify(parentValue),
                    ValueType: parentValue?.GetType().FullName,
                    SourceDescription: $"Inherited from parent {parent.GetType().Name}"));
            }
        }
    }

    private static void AppendDefaultEntry(
        DependencyObject element,
        DependencyProperty dp,
        List<PrecedenceEntryDto> trace)
    {
        PropertyMetadata metadata = dp.GetMetadata(element);
        object? defaultValue = metadata.DefaultValue;
        trace.Add(new PrecedenceEntryDto(
            Source: DefaultSource,
            Value: Stringify(defaultValue),
            ValueType: defaultValue?.GetType().FullName,
            SourceDescription: DefaultDescription));
    }

    private static string DescribeStyle(Style style)
    {
        string target = style.TargetType?.Name ?? "?";
        return $"TargetType={target}";
    }

    private static string? Stringify(object? value) => value switch
    {
        null => null,
        _ when ReferenceEquals(value, DependencyProperty.UnsetValue) => "{UnsetValue}",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };

    private static void CollectTypeWalkDps(Type startType, HashSet<string> seen, List<DependencyPropertyDto> sink)
    {
        Type? current = startType;
        while (current is not null && typeof(DependencyObject).IsAssignableFrom(current))
        {
            FieldInfo[] fields = current.GetFields(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            IEnumerable<DependencyProperty> dps = fields
                .Where(f => typeof(DependencyProperty).IsAssignableFrom(f.FieldType))
                .Select(f => f.GetValue(null) as DependencyProperty)
                .OfType<DependencyProperty>();
            foreach (DependencyProperty dp in dps)
            {
                AddDto(dp, isAttached: false, seen, sink);
            }

            current = current.BaseType;
        }
    }

    private static void CollectLocalAttachedDps(
        DependencyObject element,
        HashSet<string> seen,
        List<DependencyPropertyDto> sink)
    {
        Type elementType = element.GetType();
        LocalValueEnumerator enumerator = element.GetLocalValueEnumerator();
        while (enumerator.MoveNext())
        {
            DependencyProperty dp = enumerator.Current.Property;
            bool isOnHierarchy = dp.OwnerType.IsAssignableFrom(elementType);
            if (!isOnHierarchy)
            {
                AddDto(dp, isAttached: true, seen, sink);
            }
        }
    }

    private static void AddDto(
        DependencyProperty dp,
        bool isAttached,
        HashSet<string> seen,
        List<DependencyPropertyDto> sink)
    {
        string ownerType = dp.OwnerType.FullName ?? dp.OwnerType.Name;
        string key = $"{ownerType}.{dp.Name}";
        bool added = seen.Add(key);
        if (added)
        {
            sink.Add(new DependencyPropertyDto(
                Name: dp.Name,
                OwnerType: ownerType,
                ValueType: dp.PropertyType.FullName ?? dp.PropertyType.Name,
                IsAttached: isAttached));
        }
    }
}
