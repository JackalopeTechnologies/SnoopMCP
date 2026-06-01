// TemplateResolver.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Inspection;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Payload;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Resolves the <see cref="ControlTemplate"/> applied to a <see cref="Control"/> into a wire
/// response: the template's type and key, the runtime template tree (the template's visual-tree
/// expansion, each node carrying a stable id), and the named parts reachable in that expansion.
/// </summary>
public sealed class TemplateResolver
{
    private readonly ElementRegistry mRegistry;

    /// <summary>
    /// Initialises a new <see cref="TemplateResolver"/>.
    /// </summary>
    /// <param name="registry">Element registry used to assign stable ids to template-tree nodes.</param>
    /// <param name="describer">
    /// The element describer. Reserved for richer per-node descriptions in a follow-up phase;
    /// validated here so the dependency contract is enforced at construction.
    /// </param>
    public TemplateResolver(ElementRegistry registry, ElementDescriber describer)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(describer);
        mRegistry = registry;
    }

    /// <summary>
    /// Resolves the control template applied to <paramref name="element"/>. When the element is not a
    /// <see cref="Control"/> or has no template, the type, key, source, and tree are <c>null</c> and
    /// the named parts are empty.
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <returns>The template type, key, source, runtime tree, and named parts.</returns>
    public ResolveTemplateResponse Resolve(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        ResolveTemplateResponse response;
        if (element is not Control control || control.Template is null)
        {
            response = new ResolveTemplateResponse(
                TemplateType: null,
                TemplateKey: null,
                TemplateSource: null,
                TemplateTree: null,
                NamedParts: []);
        }
        else
        {
            response = BuildResponse(control);
        }

        return response;
    }

    private ResolveTemplateResponse BuildResponse(Control control)
    {
        ControlTemplate template = control.Template;
        string templateType = template.GetType().FullName ?? template.GetType().Name;
        string? templateKey = template.TargetType?.Name;

        TemplateNodeDto? tree = BuildTree(control);
        List<NamedPartDto> parts = CollectNamedParts(control, template);

        return new ResolveTemplateResponse(
            TemplateType: templateType,
            TemplateKey: templateKey,
            TemplateSource: null,
            TemplateTree: tree,
            NamedParts: parts);
    }

    private TemplateNodeDto? BuildTree(DependencyObject root)
    {
        TemplateNodeDto? node = null;
        bool isVisual = root is Visual or Visual3D;
        if (isVisual)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            var children = new List<TemplateNodeDto>(count);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                TemplateNodeDto? childNode = BuildTree(child);
                if (childNode is not null)
                {
                    children.Add(childNode);
                }
            }

            node = new TemplateNodeDto(
                ElementId: mRegistry.GetOrAssign(root),
                Type: root.GetType().Name,
                Name: (root as FrameworkElement)?.Name,
                Children: children);
        }

        return node;
    }

    private List<NamedPartDto> CollectNamedParts(Control control, ControlTemplate template)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var parts = new List<NamedPartDto>();

        CollectFactoryNames(template.VisualTree, seen);
        CollectVisualTreeNames(control, seen);
        ResolveParts(control, template, seen, parts);

        return parts;
    }

    private static void CollectFactoryNames(FrameworkElementFactory? factory, HashSet<string> sink)
    {
        FrameworkElementFactory? current = factory;
        while (current is not null)
        {
            if (current.Name is { Length: > 0 } name)
            {
                sink.Add(name);
            }

            CollectFactoryNames(current.FirstChild, sink);
            current = current.NextSibling;
        }
    }

    private static void CollectVisualTreeNames(DependencyObject node, HashSet<string> sink)
    {
        bool isVisual = node is Visual or Visual3D;
        if (isVisual)
        {
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(node, i);
                if (child is FrameworkElement { Name: { Length: > 0 } name })
                {
                    sink.Add(name);
                }

                CollectVisualTreeNames(child, sink);
            }
        }
    }

    private void ResolveParts(
        Control control,
        ControlTemplate template,
        HashSet<string> names,
        List<NamedPartDto> sink)
    {
        foreach (string name in names)
        {
            if (template.FindName(name, control) is DependencyObject part)
            {
                sink.Add(new NamedPartDto(
                    PartName: name,
                    PartType: part.GetType().FullName ?? part.GetType().Name,
                    ElementId: mRegistry.GetOrAssign(part)));
            }
        }
    }
}
