// ServerStateInfo.cs
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

namespace SnoopMCP.Host;

/// <summary>
///     Pure mapping from <see cref="ServerState" /> to the tray's tooltip text and the availability of
///     the Start/Stop actions. No WPF or Win32 dependency, so it is unit-testable in isolation.
/// </summary>
public static class ServerStateInfo
{
    /// <summary>Gets the tooltip text describing the supplied state.</summary>
    /// <param name="state">The current server state.</param>
    /// <returns>A non-empty tooltip string.</returns>
    public static string Tooltip(ServerState state)
    {
        return state switch
        {
            ServerState.Running => RunningTip,
            ServerState.Starting => StartingTip,
            ServerState.Faulted => FaultedTip,
            _ => StoppedTip
        };
    }

    /// <summary>Gets a value indicating whether Start is available in the supplied state.</summary>
    /// <param name="state">The current server state.</param>
    /// <returns><c>true</c> when stopped or faulted.</returns>
    public static bool CanStart(ServerState state)
    {
        return state is ServerState.Stopped or ServerState.Faulted;
    }

    /// <summary>Gets a value indicating whether Stop is available in the supplied state.</summary>
    /// <param name="state">The current server state.</param>
    /// <returns><c>true</c> when running.</returns>
    public static bool CanStop(ServerState state)
    {
        return state is ServerState.Running;
    }

    private const string RunningTip = "SnoopMCP — running on http://127.0.0.1:6300";
    private const string StartingTip = "SnoopMCP — starting…";
    private const string StoppedTip = "SnoopMCP — stopped";
    private const string FaultedTip = "SnoopMCP — error (is port 6300 already in use?)";
}
