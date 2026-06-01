// IInjectorService.cs
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
/// Abstraction over injecting the SnoopMCP payload into a target process and probing it. The real
/// implementation (wired in Task 31) drives the Snoop generic injector; until then
/// <see cref="NullInjectorService"/> is registered so an attach attempt surfaces a clear error.
/// </summary>
public interface IInjectorService
{
    /// <summary>
    /// Injects the payload into the target process and points it at the supplied pipe.
    /// </summary>
    /// <param name="processId">The target process id.</param>
    /// <param name="pipeName">The named pipe the payload should serve on.</param>
    /// <param name="cancellationToken">A token to observe while injecting.</param>
    Task InjectAsync(int processId, string pipeName, CancellationToken cancellationToken);

    /// <summary>
    /// Probes the target process for the metadata reported back to the client on attach.
    /// </summary>
    /// <param name="processId">The target process id.</param>
    /// <param name="cancellationToken">A token to observe while probing.</param>
    /// <returns>The probe result describing the target process.</returns>
    Task<ProcessProbeResult> ProbeAsync(int processId, CancellationToken cancellationToken);
}
