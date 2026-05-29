// DataContextInspector.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload.Inspection;

using System.Globalization;
using System.Reflection;
using System.Windows;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Inspects the CLR object hanging off an element's <c>DataContext</c>: type shape (Task 17)
/// and dotted-path reads (Task 18). The same class hosts both methods because they share
/// the "walk into the DataContext" intent.
/// </summary>
public sealed class DataContextInspector
{
    /// <summary>
    /// Snapshots the CLR type shape of an element's DataContext.
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <returns>
    /// A response whose <see cref="DescribeDataContextResponse.DataContext"/> is the type-shape info,
    /// or <c>null</c> when the element has no DataContext.
    /// </returns>
    /// <remarks>
    /// CA1822 disabled: scaffolded for DI in handler; will gain instance state in follow-up phases.
    /// </remarks>
#pragma warning disable CA1822
    public DescribeDataContextResponse DescribeDataContext(DependencyObject element)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(element);

        object? dataContext = ResolveDataContext(element);
        DataContextInfo? info = dataContext is null ? null : BuildInfo(dataContext);
        return new DescribeDataContextResponse(info);
    }

    /// <summary>
    /// Reads a dot-separated property path off an element's DataContext, e.g.
    /// <c>SelectedCustomer.Address.Street</c>.
    /// </summary>
    /// <param name="element">The element whose DataContext roots the walk.</param>
    /// <param name="path">The dot-separated property path. Must be non-empty.</param>
    /// <returns>
    /// The stringified leaf value and its runtime type when the path is reachable; otherwise a
    /// response whose <see cref="ReadDataContextPathResponse.PathReachable"/> is <c>false</c> and
    /// <see cref="ReadDataContextPathResponse.FailureAt"/> locates the offending segment.
    /// </returns>
    /// <remarks>
    /// CA1822 disabled: scaffolded for DI in handler; will gain instance state in follow-up phases.
    /// </remarks>
#pragma warning disable CA1822
    public ReadDataContextPathResponse ReadPath(DependencyObject element, string path)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrEmpty(path);

        object? root = ResolveDataContext(element);
        ReadDataContextPathResponse response = root is null
            ? new ReadDataContextPathResponse(null, null, false, string.Empty)
            : WalkPath(root, path);
        return response;
    }

    private static ReadDataContextPathResponse WalkPath(object root, string path)
    {
        string[] segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        object? current = root;
        string traversed = string.Empty;
        bool failed = false;
        string? failedAt = null;

        for (int i = 0; i < segments.Length && !failed; i++)
        {
            string segment = segments[i];
            string nextTraversed = traversed.Length == 0 ? segment : $"{traversed}.{segment}";
            (object? value, string newTraversed, bool stepFailed, string? stepFailedAt) =
                StepInto(current, segment, traversed, nextTraversed);
            current = value;
            traversed = newTraversed;
            failed = stepFailed;
            failedAt = stepFailedAt;
        }

        return BuildPathResult(current, failed, failedAt);
    }

    private static (object? Value, string Traversed, bool Failed, string? FailedAt) StepInto(
        object? current,
        string segment,
        string traversed,
        string nextTraversed)
    {
        (object? Value, string Traversed, bool Failed, string? FailedAt) result;
        if (current is null)
        {
            result = (null, traversed, true, traversed);
        }
        else
        {
            PropertyInfo? prop = current.GetType().GetProperty(
                segment,
                BindingFlags.Public | BindingFlags.Instance);
            result = prop is { CanRead: true } readable
                ? (readable.GetValue(current), nextTraversed, false, null)
                : (null, traversed, true, nextTraversed);
        }
        return result;
    }

    private static ReadDataContextPathResponse BuildPathResult(object? current, bool failed, string? failedAt)
    {
        ReadDataContextPathResponse result;
        if (failed)
        {
            result = new ReadDataContextPathResponse(null, null, false, failedAt);
        }
        else
        {
            string? value = current is null ? null : Convert.ToString(current, CultureInfo.InvariantCulture);
            string? valueType = current?.GetType().FullName;
            result = new ReadDataContextPathResponse(value, valueType, true, null);
        }
        return result;
    }

    private static object? ResolveDataContext(DependencyObject element)
    {
        object? value = element switch
        {
            FrameworkElement fe => fe.DataContext,
            FrameworkContentElement fce => fce.DataContext,
            _ => null
        };
        return value;
    }

    private static DataContextInfo BuildInfo(object dataContext)
    {
        Type type = dataContext.GetType();
        string typeName = type.Name;
        string ns = type.Namespace ?? string.Empty;
        List<string> baseTypes = CollectBaseTypes(type);
        List<string> interfaces = CollectInterfaces(type);
        List<DeclaredPropertyDto> properties = CollectDeclaredProperties(type);

        return new DataContextInfo(typeName, ns, baseTypes, interfaces, properties);
    }

    private static List<string> CollectBaseTypes(Type type)
    {
        var names = new List<string>();
        Type? current = type.BaseType;
        while (current is not null)
        {
            names.Add(current.FullName ?? current.Name);
            current = current.BaseType;
        }
        return names;
    }

    private static List<string> CollectInterfaces(Type type)
    {
        Type[] interfaces = type.GetInterfaces();
        var names = new List<string>(interfaces.Length);
        foreach (Type iface in interfaces)
        {
            names.Add(iface.FullName ?? iface.Name);
        }
        return names;
    }

    private static List<DeclaredPropertyDto> CollectDeclaredProperties(Type type)
    {
        PropertyInfo[] props = type.GetProperties(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var dtos = new List<DeclaredPropertyDto>(props.Length);
        foreach (PropertyInfo prop in props)
        {
            string propType = prop.PropertyType.FullName ?? prop.PropertyType.Name;
            dtos.Add(new DeclaredPropertyDto(
                Name: prop.Name,
                Type: propType,
                CanRead: prop.CanRead,
                CanWrite: prop.CanWrite));
        }
        return dtos;
    }
}
