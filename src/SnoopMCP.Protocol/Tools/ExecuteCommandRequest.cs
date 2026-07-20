// ExecuteCommandRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>executeCommand</c> tool.
/// </summary>
/// <param name="Id">Element id: an ICommandSource (e.g. Button) or the root for <paramref name="Path"/>.</param>
/// <param name="Path">Optional dotted DataContext path to an ICommand; when null, the element's own Command is used.</param>
/// <param name="Parameter">Optional command parameter (string); when null, the element's CommandParameter is used.</param>
/// <param name="Dispatch">
/// Dispatch mode: <c>null</c> or <c>"wait"</c> (default) waits for the command to execute before
/// returning; <c>"post"</c> fires-and-forgets and returns <c>Dispatched=true</c> immediately —
/// verify the effect separately (e.g. via <c>waitForValue</c>). In <c>"post"</c> mode the command's
/// outcome is not surfaced to the caller: a <c>CommandNotExecutable</c> (CanExecute==false) or any
/// exception thrown by the posted work runs unobserved on the target's dispatcher and is not reported.
/// </param>
public sealed record ExecuteCommandRequest(int Id, string? Path, string? Parameter, string? Dispatch);
