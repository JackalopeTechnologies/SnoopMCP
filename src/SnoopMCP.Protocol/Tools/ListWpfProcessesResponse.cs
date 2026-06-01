// ListWpfProcessesResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Response for <c>listWpfProcesses</c>: the WPF processes currently visible to the host as
/// candidate debug targets.
/// </summary>
/// <param name="Processes">The discovered WPF processes, most-recently-enumerated order.</param>
public sealed record ListWpfProcessesResponse(IReadOnlyList<WpfProcessDto> Processes);
