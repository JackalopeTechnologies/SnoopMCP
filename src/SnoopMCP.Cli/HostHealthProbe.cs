// HostHealthProbe.cs
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

namespace SnoopMCP.Cli;

/// <summary>
///     Probes the host's <c>/health</c> endpoint over HTTP. A 2xx response means the host is up; a
///     connection failure or timeout means it is not (reported as unhealthy, never thrown).
/// </summary>
public static class HostHealthProbe
{
    /// <summary>Returns true when the host answers <paramref name="healthUrl" /> with a success status.</summary>
    public static async Task<bool> IsHealthyAsync(HttpClient client, string healthUrl, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrEmpty(healthUrl);
        var healthy = false;
        try
        {
            HttpResponseMessage response = await client.GetAsync(healthUrl, ct).ConfigureAwait(false);
            healthy = response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            // Host not listening → unhealthy, not an error.
        }
        catch (TaskCanceledException)
        {
            // Probe timed out → unhealthy, not an error.
        }

        return healthy;
    }

    /// <summary>The host's health URL on localhost.</summary>
    public const string HealthUrl = "http://127.0.0.1:6300/health";
}
