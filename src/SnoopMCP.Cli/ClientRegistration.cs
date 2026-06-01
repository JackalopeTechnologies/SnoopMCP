// ClientRegistration.cs
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

#region Usings

using SnoopMCP.ClientIntegration;

#endregion

namespace SnoopMCP.Cli;

/// <summary>
///     Drives a set of <see cref="IClientWriter" /> over register / unregister / status, logging each
///     outcome and collapsing the per-writer results into a process exit code (0 = all succeeded,
///     2 = at least one failed).
/// </summary>
public static class ClientRegistration
{
    /// <summary>Registers the endpoint in every writer.</summary>
    /// <param name="writers">The set of client writers to register with.</param>
    /// <param name="endpoint">The MCP endpoint to register.</param>
    /// <param name="log">Writer receiving per-client outcome messages.</param>
    public static int RegisterAll(IReadOnlyList<IClientWriter> writers, McpEndpoint endpoint, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(writers);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(log);
        var failures = 0;
        foreach (IClientWriter writer in writers)
        {
            RegisterResult result = writer.Register(endpoint);
            log.WriteLine(result.Message);
            failures += result.Success ? 0 : 1;
        }

        return failures == 0 ? ExitOk : ExitPartialFailure;
    }

    /// <summary>Removes the SnoopMCP entry from every writer.</summary>
    /// <param name="writers">The set of client writers to unregister from.</param>
    /// <param name="log">Writer receiving per-client outcome messages.</param>
    public static int UnregisterAll(IReadOnlyList<IClientWriter> writers, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(writers);
        ArgumentNullException.ThrowIfNull(log);
        var failures = 0;
        foreach (IClientWriter writer in writers)
        {
            UnregisterResult result = writer.Unregister();
            log.WriteLine(result.Message);
            failures += result.Success ? 0 : 1;
        }

        return failures == 0 ? ExitOk : ExitPartialFailure;
    }

    /// <summary>Logs each writer's registration status. Always returns success.</summary>
    /// <param name="writers">The set of client writers to report status for.</param>
    /// <param name="log">Writer receiving per-client status messages.</param>
    public static int Status(IReadOnlyList<IClientWriter> writers, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(writers);
        ArgumentNullException.ThrowIfNull(log);
        foreach (IClientWriter writer in writers)
        {
            StatusResult result = writer.GetStatus();
            log.WriteLine(result.Message);
        }

        return ExitOk;
    }

    private const int ExitOk = 0;
    private const int ExitPartialFailure = 2;
}
