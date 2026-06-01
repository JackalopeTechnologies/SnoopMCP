// IClientWriter.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace SnoopMCP.ClientIntegration;

/// <summary>
///     Registers, removes, and reports the SnoopMCP MCP server entry in one LLM client's configuration
///     file, idempotently and without disturbing the client's other settings.
/// </summary>
public interface IClientWriter
{
    /// <summary>Display name of the client (for logs / status), e.g. <c>Claude Code</c>.</summary>
    string ClientName { get; }

    /// <summary>Adds or updates the SnoopMCP entry; preserves all other content.</summary>
    RegisterResult Register(McpEndpoint endpoint);

    /// <summary>Removes the SnoopMCP entry if present; a missing file or entry is a successful no-op.</summary>
    UnregisterResult Unregister();

    /// <summary>Reports whether the SnoopMCP entry is currently present with the expected URL.</summary>
    StatusResult GetStatus();

    /// <summary>
    ///     True when the target agent appears to be installed on this machine (its well-known config
    ///     directory or file exists). "All"/installer runs use this so absent agents are never touched;
    ///     an explicit single-agent register ignores it.
    /// </summary>
    bool IsDetected();
}
