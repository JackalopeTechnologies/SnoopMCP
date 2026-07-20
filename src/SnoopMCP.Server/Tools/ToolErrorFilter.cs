// ToolErrorFilter.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tools;

using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Protocol.Errors;

/// <summary>
/// A call-tool filter that gives every failure a typed, self-describing message.
/// </summary>
/// <remarks>
/// <para>
/// The MCP SDK replaces any exception that is not an <see cref="McpException"/> with the sanitised
/// text "An error occurred invoking '…'", which names neither the failure nor its reason. The real
/// exception only ever reached <c>%LOCALAPPDATA%\SnoopMCP\logs\server.log</c> — a file an agent
/// driving the app has no reason to know exists. That asymmetry actively misled: a bare failure from
/// one tool, next to a clean <c>[NotDrivable] …</c> from its sibling, reads as a deeper class of
/// problem than it is. See issue #81.
/// </para>
/// <para>
/// This runs as a filter rather than as try/catch inside each tool because the most misleading
/// failures happen BEFORE any tool body executes: the SDK's argument marshaller rejects the call
/// while binding parameters (issue #72), and response serialization fails after the body returns
/// (issue #73). Neither is reachable from inside a tool method.
/// </para>
/// </remarks>
public static class ToolErrorFilter
{
    /// <summary>The input-schema property listing a tool's mandatory arguments.</summary>
    private const string RequiredProperty = "required";

    /// <summary>Separates the tool name from the detail that follows it in a message.</summary>
    private const string ToolPrefixSeparator = ": ";

    /// <summary>
    /// Creates the filter: it validates mandatory arguments up front, then defers to the rest of the
    /// pipeline and re-describes anything that escapes, leaving classification to <see cref="Describe"/>.
    /// </summary>
    /// <returns>A filter to add to <c>McpServerOptions.Filters.Request.CallToolFilters</c>.</returns>
    /// <remarks>
    /// The pipeline body lives in <see cref="InvokeAsync"/> rather than an inline lambda because
    /// STR0002/STR0005 scan lambda bodies as part of the enclosing method, so an inline body would
    /// count its <c>return</c> against this method's single-return shape.
    /// </remarks>
    public static McpRequestFilter<CallToolRequestParams, CallToolResult> Create() =>
        next => (request, cancellationToken) => InvokeAsync(next, request, cancellationToken);

    private static async ValueTask<CallToolResult> InvokeAsync(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next,
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
    {
        // Checked BEFORE the pipeline runs: the SDK's argument marshaller rejects a missing argument
        // deep inside the tool invocation, where its exception is replaced by the sanitised text and
        // becomes unreachable. Validating here is the only way that failure can name what is missing.
        EnsureRequiredArguments(request);

        CallToolResult result;
        try
        {
            result = await next(request, cancellationToken).ConfigureAwait(false);
        }
        catch (McpException)
        {
            // Already typed - a tool promoted its own SnoopMcpException. Leave it exactly as is.
            throw;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not a failure to relabel; the SDK maps it to the protocol's own signal.
            throw;
        }
        catch (Exception ex)
        {
            throw Describe(ex, request.Params?.Name);
        }
        return result;
    }

    /// <summary>
    /// Rejects a call that omits an argument the matched tool's own input schema marks as required,
    /// naming the tool and the missing parameter.
    /// </summary>
    /// <param name="request">The incoming call-tool request.</param>
    private static void EnsureRequiredArguments(RequestContext<CallToolRequestParams> request)
    {
        CallToolRequestParams? parameters = request.Params;
        if (parameters is not null && request.MatchedPrimitive is McpServerTool tool)
        {
            EnsureSchemaRequirements(parameters, tool.ProtocolTool.InputSchema);
        }
    }

    /// <summary>Walks a tool's declared <c>required</c> list, checking each against the supplied arguments.</summary>
    /// <param name="parameters">The call's name and arguments.</param>
    /// <param name="schema">The matched tool's published input schema.</param>
    private static void EnsureSchemaRequirements(CallToolRequestParams parameters, JsonElement schema)
    {
        if (schema.ValueKind == JsonValueKind.Object
            && schema.TryGetProperty(RequiredProperty, out JsonElement required)
            && required.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in required.EnumerateArray())
            {
                ThrowIfArgumentMissing(parameters, entry.GetString());
            }
        }
    }

    /// <summary>Throws a typed error when <paramref name="name"/> is absent from the call's arguments.</summary>
    /// <param name="parameters">The call's name and arguments.</param>
    /// <param name="name">The required argument's name.</param>
    private static void ThrowIfArgumentMissing(CallToolRequestParams parameters, string? name)
    {
        bool missing = !string.IsNullOrEmpty(name)
            && (parameters.Arguments is null || !parameters.Arguments.ContainsKey(name));
        if (missing)
        {
            throw new McpException(
                $"[{ErrorCode.InvalidArgument}] {parameters.Name}{ToolPrefixSeparator}"
                + $"missing required argument '{name}'.");
        }
    }

    /// <summary>
    /// Classifies <paramref name="exception"/> into a SnoopMCP error code and renders it in the same
    /// <c>[Code] message</c> shape the tools already use, preserving the original as the inner
    /// exception so the server log keeps the full stack.
    /// </summary>
    /// <param name="exception">The exception that escaped the tool pipeline.</param>
    /// <param name="toolName">The tool being invoked, when the request named one.</param>
    /// <returns>A typed exception whose message the SDK propagates verbatim to the client.</returns>
    public static McpException Describe(Exception exception, string? toolName)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Callers inside a tool body already know which tool they are; only the filter, which sees
        // arbitrary requests, needs to name it.
        string prefix = string.IsNullOrEmpty(toolName) ? string.Empty : toolName + ToolPrefixSeparator;
        (ErrorCode code, string detail) = exception switch
        {
            // Carries its own classification already.
            SnoopMcpException snoop => (snoop.Code, snoop.Message),

            // The SDK's argument marshaller: its message already names the offending parameter.
            ArgumentException argument => (ErrorCode.InvalidArgument, argument.Message),

            // Response serialization: the tool succeeded but its result could not be written.
            JsonException or NotSupportedException => (ErrorCode.ProtocolError, $"{prefix}{exception.Message}"),

            _ => (ErrorCode.Unknown, $"{prefix}{exception.GetType().Name}: {exception.Message}")
        };

        return new McpException($"[{code}] {detail}", exception);
    }
}
