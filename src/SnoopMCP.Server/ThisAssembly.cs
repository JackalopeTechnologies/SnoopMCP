// ThisAssembly.cs
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

using System.Reflection;

/// <summary>
/// Exposes this (server) assembly's informational version without a magic string. Untagged developer
/// builds fall back to <see cref="DefaultDevVersion"/> so clients can tell they are not running a
/// tagged release.
/// </summary>
internal static class ThisAssembly
{
    private const string DefaultDevVersion = "0.0.0-dev";

    /// <summary>Gets the assembly informational version, or a dev fallback when none is present.</summary>
    public static string InformationalVersion { get; } =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? DefaultDevVersion;
}
