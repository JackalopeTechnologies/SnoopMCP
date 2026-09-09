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
/// returning; <c>"post"</c> resolves the command and queries <c>CanExecute</c> on the UI thread first —
/// reporting <c>NotDrivable</c>, <c>BindingPathError</c>, or <c>CommandNotExecutable</c> as structured
/// errors — then fires-and-forgets the execution itself and returns <c>Dispatched=true</c> with
/// <c>CanExecute=true</c> immediately. Verify the effect separately (e.g. via <c>waitForValue</c>): only
/// an exception thrown inside <c>Execute</c> goes unreported (it is traced inside the target, never
/// left unobserved).
/// </param>
public sealed record ExecuteCommandRequest(int Id, string? Path, string? Parameter, string? Dispatch);
