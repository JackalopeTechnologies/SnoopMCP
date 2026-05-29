// SnoopMcpException.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Errors;

/// <summary>
/// Exception type carrying a structured <see cref="ErrorCode"/> for protocol-level failures.
/// </summary>
public class SnoopMcpException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="SnoopMcpException"/> class with a code and message.
    /// </summary>
    /// <param name="code">The structured error code.</param>
    /// <param name="message">A human-readable description of the failure.</param>
    public SnoopMcpException(ErrorCode code, string message) : base(message)
    {
        Code = code;
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="SnoopMcpException"/> class with a code, message, and inner exception.
    /// </summary>
    /// <param name="code">The structured error code.</param>
    /// <param name="message">A human-readable description of the failure.</param>
    /// <param name="inner">The exception that triggered this one.</param>
    public SnoopMcpException(ErrorCode code, string message, Exception inner) : base(message, inner)
    {
        Code = code;
    }

    /// <summary>Gets the structured error code associated with this exception.</summary>
    public ErrorCode Code { get; }
}
