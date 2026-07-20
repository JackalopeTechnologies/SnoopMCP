// ToolParameterOptionalityTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using System.Reflection;
using ModelContextProtocol.Server;
using Tools;
using Xunit;

/// <summary>
/// Guards the contract every documented-optional tool parameter depends on: the MCP SDK's
/// <c>ReflectionAIFunction</c> decides a parameter is REQUIRED purely from the absence of a default
/// value — it never consults nullability. So a parameter declared <c>string? dispatch</c> with no
/// <c>= null</c> is published to clients as mandatory, and omitting it fails the call before it ever
/// reaches a handler with <c>ArgumentException: The arguments dictionary is missing a value for the
/// required parameter '…'</c>, surfaced to the caller only as the SDK's sanitised
/// "An error occurred invoking '…'".
/// That is what made peer_invoke look like a broken AutomationPeer while the call was in fact being
/// rejected upstream (issue #72). This test asserts the rule surface-wide rather than per tool, so a
/// newly added optional parameter cannot reintroduce it.
/// </summary>
public sealed class ToolParameterOptionalityTests
{
    /// <summary>The MCP tool container types whose parameters are published to clients.</summary>
    public static TheoryData<Type> ToolTypes => new() { typeof(McpTools), typeof(UiaTools) };

    [Theory]
    [MemberData(nameof(ToolTypes))]
    public void NullableToolParameters_DeclareDefaults_SoTheyArePublishedAsOptional(Type toolType)
    {
        ArgumentNullException.ThrowIfNull(toolType);
        var nullability = new NullabilityInfoContext();

        string[] offenders = toolType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .SelectMany(method => method.GetParameters().Select(parameter => (Method: method, Parameter: parameter)))
            // The SDK binds CancellationToken itself; it is never part of the published schema.
            .Where(entry => entry.Parameter.ParameterType != typeof(CancellationToken))
            .Where(entry => nullability.Create(entry.Parameter).WriteState == NullabilityState.Nullable)
            .Where(entry => !entry.Parameter.HasDefaultValue)
            .Select(entry => $"{toolType.Name}.{entry.Method.Name}({entry.Parameter.Name})")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Nullable tool parameters without a default value are published as REQUIRED, so clients "
            + "cannot omit them. Add '= null': " + string.Join(", ", offenders));
    }
}
