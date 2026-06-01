// HealthStatus.cs
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
///     The <c>/health</c> response: a liveness marker, the host's informational version, and whether a
///     target is currently attached. Serialised to JSON by the endpoint.
/// </summary>
/// <param name="Status">Always <c>ok</c> when the host is serving requests.</param>
/// <param name="Version">The host's informational version.</param>
/// <param name="Attached">True when a WPF target session is open.</param>
public sealed record HealthStatus(string Status, string Version, bool Attached)
{
    /// <summary>Creates an <c>ok</c> health status for the given version and attach state.</summary>
    public static HealthStatus Create(string version, bool attached)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);
        return new HealthStatus(OkStatus, version, attached);
    }

    private const string OkStatus = "ok";
}
