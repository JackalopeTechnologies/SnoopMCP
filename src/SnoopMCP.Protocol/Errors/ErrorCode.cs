// ErrorCode.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Errors;

/// <summary>
/// Structured error codes returned across the SnoopMCP wire protocol.
/// </summary>
public enum ErrorCode
{
    /// <summary>Default value; indicates an unclassified failure.</summary>
    Unknown = 0,

    /// <summary>The host failed to attach to the target process.</summary>
    AttachFailed = 1,

    /// <summary>The in-process payload assembly failed to load.</summary>
    PayloadLoadFailed = 2,

    /// <summary>A dispatcher-bound operation timed out.</summary>
    DispatcherTimeout = 3,

    /// <summary>The active session was lost (host crashed or detached).</summary>
    SessionLost = 4,

    /// <summary>The caller is not authorised to perform the requested action.</summary>
    AccessDenied = 5,

    /// <summary>A previously-handed-out element id no longer resolves to a live object.</summary>
    ElementExpired = 6,

    /// <summary>One or more tool arguments are missing or invalid.</summary>
    InvalidArgument = 7,

    /// <summary>The requested tool name is not registered.</summary>
    ToolNotFound = 8,

    /// <summary>The wire payload violated the protocol contract.</summary>
    ProtocolError = 9,

    /// <summary>A binding path expression could not be evaluated.</summary>
    BindingPathError = 10,

    /// <summary>A textual path could not be parsed.</summary>
    PathParseError = 11,

    /// <summary>The host interaction gate is disabled; the requested mutating action was refused.</summary>
    InteractionDisabled = 12,

    /// <summary>No UIA action pattern (and no payload fallback) can drive the target element.</summary>
    NotDrivable = 13,

    /// <summary>The target value element is read-only.</summary>
    ValueReadOnly = 14,

    /// <summary>The window cannot be captured (e.g. minimized, no printable content).</summary>
    CaptureUnavailable = 15,

    /// <summary>An element handle expired and could not be re-resolved by its locator.</summary>
    UiaElementStale = 16,

    /// <summary>A locator re-resolved to more than one element; the caller must disambiguate.</summary>
    UiaAmbiguousLocator = 17,

    /// <summary>A UI Automation call exceeded its timeout against an unresponsive target.</summary>
    TargetUnresponsive = 18,

    /// <summary>A mutating action timed out on the dispatcher; it may still have applied — verify.</summary>
    ActionPending = 19,

    /// <summary>Reserved (append-only): a fire-and-forget/dialog-opening action was posted. NOT thrown — successful fire-and-forget is signaled by the response's Dispatched field, not by this code. Kept for potential future error use.</summary>
    ActionDispatched = 20,

    /// <summary>The bound command's CanExecute returned false.</summary>
    CommandNotExecutable = 21
}
