// ExecuteCommandResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>executeCommand</c> tool.
/// </summary>
/// <param name="Executed">
/// Whether the command was executed. <c>true</c>/<c>false</c> in <c>"wait"</c> mode once observed;
/// <c>null</c> (omitted on the wire) in <c>"post"</c> mode, where the outcome is not observed.
/// </param>
/// <param name="CanExecute">
/// The command's <c>CanExecute</c> result at dispatch time. <c>null</c> (omitted on the wire) in
/// <c>"post"</c> mode, where the outcome is not observed.
/// </param>
/// <param name="Dispatched">
/// <c>true</c> when the request used <c>"post"</c> dispatch and was fired-and-forgotten rather
/// than awaited; <c>false</c> when the request was awaited to completion (<c>"wait"</c> mode).
/// </param>
public sealed record ExecuteCommandResponse(bool? Executed, bool? CanExecute, bool Dispatched);
