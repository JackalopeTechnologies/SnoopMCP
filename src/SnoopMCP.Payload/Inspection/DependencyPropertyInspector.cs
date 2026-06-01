// DependencyPropertyInspector.cs
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

using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;

#endregion

namespace SnoopMCP.Payload.Inspection;

/// <summary>
///     Enumerates the dependency properties reachable on an element (Task 19) and reads a single
///     property's effective value with a best-effort precedence trace (Task 20). The two tools share
///     a class because they share the "introspect dependency properties" intent.
/// </summary>
public sealed class DependencyPropertyInspector
{
    /// <summary>
    ///     Enumerates every dependency property reachable on an element: those declared on the element's
    ///     type chain (<c>isAttached=false</c>) plus any attached property currently set on it
    ///     (<c>isAttached=true</c>). Deduplicated by owner-type + name.
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <returns>The reachable dependency properties.</returns>
    /// <remarks>
    ///     CA1822 disabled: scaffolded for DI in handler; will gain instance state in follow-up phases.
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
    ///     Reads a dependency property's current effective value and builds a best-effort precedence
    ///     trace explaining which source won and what the losing candidates were.
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <param name="propertyName">The dependency property's registered name. Must be non-empty.</param>
    /// <returns>The current value, its type, the winning source name, and the precedence trace.</returns>
    /// <exception cref="SnoopMcpException">
    ///     Thrown with <see cref="ErrorCode.InvalidArgument" /> when the property is not found on the element.
    /// </exception>
    /// <remarks>
    ///     Sources that cannot be traced cheaply in v1 (animation, trigger setters, template parent) are
    ///     not enumerated as losing candidates; the winning-source name still identifies which family won.
    ///     CA1822 disabled: scaffolded for DI in handler; will gain instance state in follow-up phases.
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

        var currentValue = element.GetValue(dp);
        var winningSource = DependencyPropertyHelper
            .GetValueSource(element, dp)
            .BaseValueSource
            .ToString();

        var trace = new List<PrecedenceEntryDto>();
        AppendLocalEntry(element, dp, trace);
        AppendStyleSetterEntries(element, dp, trace);
        AppendInheritedEntry(element, dp, trace);
        AppendDefaultEntry(element, dp, trace);

        return new GetDependencyPropertyResponse(
            dp.Name,
            Stringify(currentValue),
            currentValue?.GetType().FullName,
            trace,
            winningSource);
    }

    private static DependencyProperty? ResolveDp(Type ownerType, string propertyName)
    {
        DependencyProperty? found = null;
        var fieldName = propertyName + DpFieldSuffix;
        Type? current = ownerType;
        while (current is not null && found is null)
        {
            FieldInfo? field = current.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            var isDpField = field is not null && typeof(DependencyProperty).IsAssignableFrom(field.FieldType);
            if (isDpField && field is not null) found = field.GetValue(null) as DependencyProperty;

            current = current.BaseType;
        }

        return found;
    }

    private static void AppendLocalEntry(DependencyObject element, DependencyProperty dp,
        List<PrecedenceEntryDto> trace)
    {
        var localValue = element.ReadLocalValue(dp);
        var hasLocal = localValue != DependencyProperty.UnsetValue;
        if (hasLocal)
            trace.Add(new PrecedenceEntryDto(
                LocalSource,
                Stringify(localValue),
                localValue?.GetType().FullName,
                LocalDescription));
    }

    private static void AppendStyleSetterEntries(
        DependencyObject element,
        DependencyProperty dp,
        List<PrecedenceEntryDto> trace)
    {
        if (element is FrameworkElement { Style: not null } fe) WalkStyleChain(fe.Style, dp, trace);
    }

    private static void WalkStyleChain(Style? start, DependencyProperty dp, List<PrecedenceEntryDto> trace)
    {
        Style? current = start;
        var depth = 0;
        while (current is not null)
        {
            IEnumerable<Setter> matches = current.Setters
                .OfType<Setter>()
                .Where(setter => setter.Property == dp);
            foreach (Setter setter in matches)
                trace.Add(new PrecedenceEntryDto(
                    StyleSetterSource,
                    Stringify(setter.Value),
                    setter.Value?.GetType().FullName,
                    $"Style {DescribeStyle(current)} (chain depth {depth})"));

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
        var inherits = metadata is FrameworkPropertyMetadata { Inherits: true };
        DependencyObject? parent = inherits ? VisualTreeHelper.GetParent(element) : null;
        if (parent is not null)
        {
            var parentValue = parent.GetValue(dp);
            var differsFromDefault = !Equals(parentValue, metadata.DefaultValue);
            if (differsFromDefault)
                trace.Add(new PrecedenceEntryDto(
                    InheritedSource,
                    Stringify(parentValue),
                    parentValue?.GetType().FullName,
                    $"Inherited from parent {parent.GetType().Name}"));
        }
    }

    private static void AppendDefaultEntry(
        DependencyObject element,
        DependencyProperty dp,
        List<PrecedenceEntryDto> trace)
    {
        PropertyMetadata metadata = dp.GetMetadata(element);
        var defaultValue = metadata.DefaultValue;
        trace.Add(new PrecedenceEntryDto(
            DefaultSource,
            Stringify(defaultValue),
            defaultValue?.GetType().FullName,
            DefaultDescription));
    }

    private static string DescribeStyle(Style style)
    {
        var target = style.TargetType?.Name ?? "?";
        return $"TargetType={target}";
    }

    private static string? Stringify(object? value)
    {
        return value switch
        {
            null => null,
            _ when ReferenceEquals(value, DependencyProperty.UnsetValue) => "{UnsetValue}",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

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
            foreach (DependencyProperty dp in dps) AddDto(dp, false, seen, sink);

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
            var isOnHierarchy = dp.OwnerType.IsAssignableFrom(elementType);
            if (!isOnHierarchy) AddDto(dp, true, seen, sink);
        }
    }

    private static void AddDto(
        DependencyProperty dp,
        bool isAttached,
        HashSet<string> seen,
        List<DependencyPropertyDto> sink)
    {
        var ownerType = dp.OwnerType.FullName ?? dp.OwnerType.Name;
        var key = $"{ownerType}.{dp.Name}";
        var added = seen.Add(key);
        if (added)
            sink.Add(new DependencyPropertyDto(
                dp.Name,
                ownerType,
                dp.PropertyType.FullName ?? dp.PropertyType.Name,
                isAttached));
    }

    private const string DpFieldSuffix = "Property";
    private const string LocalSource = "Local";
    private const string StyleSetterSource = "StyleSetter";
    private const string InheritedSource = "Inherited";
    private const string DefaultSource = "Default";
    private const string LocalDescription = "Local value on the element";
    private const string DefaultDescription = "Default value from type metadata";
}
