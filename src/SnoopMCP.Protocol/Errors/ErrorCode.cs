// ErrorCode.cs
// Copyright (c) 2026 Jackalope Technologies

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
    PathParseError = 11
}
