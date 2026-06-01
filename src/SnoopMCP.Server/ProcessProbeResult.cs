// ProcessProbeResult.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host;

/// <summary>
/// Metadata about a target process gathered before injection, reported back to the client on attach.
/// </summary>
/// <param name="ProcessName">The target process's name.</param>
/// <param name="RuntimeVersion">The CLR runtime version hosting the target.</param>
/// <param name="FrameworkVersion">The target framework version the target was built against.</param>
/// <param name="Bitness">The target process bitness, e.g. <c>x64</c> or <c>x86</c>.</param>
public sealed record ProcessProbeResult(
    string ProcessName,
    string RuntimeVersion,
    string FrameworkVersion,
    string Bitness);
