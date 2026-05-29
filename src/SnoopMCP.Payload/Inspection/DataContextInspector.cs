// DataContextInspector.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload.Inspection;

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
