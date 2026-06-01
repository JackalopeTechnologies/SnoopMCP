// WpfProcessDto.cs
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

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// A WPF process discovered by <c>listWpfProcesses</c> as a candidate debug target. Reported
/// pre-attach by the host (no injection required), so an LLM client can pick a target by name or
/// window title instead of needing a PID supplied out-of-band.
/// </summary>
/// <param name="Pid">The process id to pass to <c>attach</c>.</param>
/// <param name="ProcessName">The process image name (no extension).</param>
/// <param name="MainWindowTitle">The main window title, or empty if the process has none.</param>
/// <param name="Bitness"><c>x64</c> or <c>x86</c> (best effort; empty if it could not be determined).</param>
/// <param name="RuntimeVersion">The .NET host (<c>hostfxr.dll</c>) version, or <c>Unknown</c>.</param>
/// <param name="FrameworkVersion">The WPF (<c>PresentationFramework.dll</c>) version, or <c>Unknown</c>.</param>
/// <param name="Attachable">True when v1 can attach (x64). Non-x64 targets are listed but not attachable.</param>
public sealed record WpfProcessDto(
    int Pid,
    string ProcessName,
    string MainWindowTitle,
    string Bitness,
    string RuntimeVersion,
    string FrameworkVersion,
    bool Attachable);
