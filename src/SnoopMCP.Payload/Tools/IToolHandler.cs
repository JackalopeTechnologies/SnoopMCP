// IToolHandler.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload.Tools;

using System.Text.Json;

/// <summary>
/// Contract for tool handlers dispatched by the payload pipe server.
/// </summary>
public interface IToolHandler
{
    /// <summary>Gets the wire-protocol name of the tool this handler implements.</summary>
    string ToolName { get; }

    /// <summary>
    /// Executes the tool with the supplied JSON arguments.
    /// </summary>
    /// <param name="arguments">The tool-specific argument payload.</param>
    /// <param name="cancellationToken">A token to observe while executing.</param>
    /// <returns>The tool-specific JSON result.</returns>
    Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken);
}
