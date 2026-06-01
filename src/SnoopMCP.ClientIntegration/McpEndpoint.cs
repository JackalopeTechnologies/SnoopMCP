// McpEndpoint.cs
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
/// Identity of the SnoopMCP MCP server as written into a client's config: the entry name, the
/// transport type, and the HTTP URL. <see cref="Default"/> is the canonical SnoopMCP endpoint.
/// </summary>
/// <param name="Name">The config entry key (e.g. <c>snoopmcp</c>).</param>
/// <param name="Type">The MCP transport type (e.g. <c>http</c>).</param>
/// <param name="Url">The Streamable-HTTP endpoint URL.</param>
public sealed record McpEndpoint(string Name, string Type, string Url)
{
    private const string DefaultName = "snoopmcp";
    private const string DefaultType = "http";
    private const string DefaultUrl = "http://127.0.0.1:6300/mcp";

    /// <summary>The canonical SnoopMCP HTTP endpoint registered into every supported client.</summary>
    public static McpEndpoint Default { get; } = new(DefaultName, DefaultType, DefaultUrl);
}
