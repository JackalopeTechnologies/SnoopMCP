// SkillResources.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Guidance;

using System.ComponentModel;
using System.IO;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>Serves the canonical skills to clients that discover guidance through MCP resources.</summary>
[McpServerResourceType]
public sealed class SkillResources
{
    /// <summary>Stable address of the live-inspection guidance.</summary>
    public const string InspectionUri = "snoopmcp://skills/snoopmcp-first";
    /// <summary>Stable address of the interaction and screenshot guidance.</summary>
    public const string InteractionUri = "snoopmcp://skills/snoopmcp-uia";
    private const string InspectionName = "snoopmcp-first";
    private const string InteractionName = "snoopmcp-uia";
    private const string MarkdownMimeType = "text/markdown";
    private static readonly TimeSpan smCacheLifetime = TimeSpan.FromMinutes(5);

    /// <summary>Points connecting clients to the maintained guidance rather than duplicating its text.</summary>
    public static string Instructions =>
        $"Read {InspectionUri} and {InteractionUri} for the canonical SnoopMCP usage and safety guidance.";

    /// <summary>Returns live-inspection guidance without attaching to or changing an application.</summary>
    [McpServerResource(UriTemplate = InspectionUri, Name = InspectionName, MimeType = MarkdownMimeType)]
    [Description("Canonical SnoopMCP guidance for WPF inspection and inspection-session ownership.")]
    public static ReadResourceResult Inspection() => Read(InspectionName, InspectionUri);

    /// <summary>Returns interaction guidance without changing the user's interaction gate.</summary>
    [McpServerResource(UriTemplate = InteractionUri, Name = InteractionName, MimeType = MarkdownMimeType)]
    [Description("Canonical SnoopMCP guidance for app interaction, waits, screenshots, and the interaction gate.")]
    public static ReadResourceResult Interaction() => Read(InteractionName, InteractionUri);

    private static ReadResourceResult Read(string name, string uri)
    {
        using Stream stream = typeof(SkillResources).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing canonical skill resource: {name}");
        using var reader = new StreamReader(stream);
        return new ReadResourceResult
        {
            Contents = [new TextResourceContents { Uri = uri, MimeType = MarkdownMimeType, Text = reader.ReadToEnd() }],
            TimeToLive = smCacheLifetime,
            CacheScope = CacheScope.Private
        };
    }
}
