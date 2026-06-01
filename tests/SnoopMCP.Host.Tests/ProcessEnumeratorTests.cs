// ProcessEnumeratorTests.cs
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

namespace SnoopMCP.Host.Tests;

using SnoopMCP.Host.Injection;
using SnoopMCP.Protocol.Tools;
using Xunit;

/// <summary>
/// Robustness tests for <see cref="ProcessEnumerator"/>. The test host is not a WPF process and the
/// rest of the running machine is operator-dependent, so the contents of the returned list are not
/// assertable. The guarantee under test is that enumeration completes without throwing and produces
/// a non-null result the host can hand to the MCP serialiser.
/// </summary>
public sealed class ProcessEnumeratorTests
{
    [Fact]
    public void ListWpfProcesses_ReturnsNonNullList_AndDoesNotThrow()
    {
        IReadOnlyList<WpfProcessDto> results = ProcessEnumerator.ListWpfProcesses();

        Assert.NotNull(results);
    }
}
