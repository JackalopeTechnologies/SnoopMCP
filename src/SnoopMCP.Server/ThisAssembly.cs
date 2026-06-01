// ThisAssembly.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host;

using System.Reflection;

/// <summary>
/// Exposes this (server) assembly's informational version without a magic string. Untagged developer
/// builds fall back to <see cref="DefaultDevVersion"/> so clients can tell they are not running a
/// tagged release.
/// </summary>
internal static class ThisAssembly
{
    private const string DefaultDevVersion = "0.0.0-dev";

    /// <summary>Gets the assembly informational version, or a dev fallback when none is present.</summary>
    public static string InformationalVersion { get; } =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? DefaultDevVersion;
}
