// ExportXamlRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>exportXaml</c> tool.
/// </summary>
/// <param name="Id">Element id whose live state is serialized to XAML.</param>
public sealed record ExportXamlRequest(int Id);
