// ProcessEnumeratorTests.cs
// Copyright (c) 2026 Jackalope Technologies

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
