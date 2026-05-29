// DependencyPropertyInspector.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload.Inspection;

using System.Reflection;
using System.Windows;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Enumerates the dependency properties reachable on an element (Task 19) and reads a single
/// property's effective value with a best-effort precedence trace (Task 20). The two tools share
/// a class because they share the "introspect dependency properties" intent.
/// </summary>
public sealed class DependencyPropertyInspector
{
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
