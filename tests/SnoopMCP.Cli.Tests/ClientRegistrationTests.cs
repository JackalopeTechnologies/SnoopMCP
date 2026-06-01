// ClientRegistrationTests.cs
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
using Xunit;

#endregion

namespace SnoopMCP.Cli.Tests;

#region Usings

using ClientRegistration = ClientRegistration;

#endregion

public sealed class ClientRegistrationTests : IDisposable
{
    public ClientRegistrationTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mWriters =
        [
            new ClaudeCodeWriter(
                Path.Combine(mDir, ".claude.json"),
                Path.Combine(mDir, ".claude", "settings.json"),
                Path.Combine(mDir, ".claude", "skills")),
            new VsCodeMcpWriter(Path.Combine(mDir, "mcp.json"), mDir)
        ];
    }

    private readonly string mDir;
    private readonly IReadOnlyList<IClientWriter> mWriters;

    public void Dispose()
    {
        if (Directory.Exists(mDir)) Directory.Delete(mDir, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void RegisterAll_RegistersEveryWriter_AndReturnsOk()
    {
        using var log = new StringWriter();

        var code = ClientRegistration.RegisterAll(mWriters, McpEndpoint.Default, log);

        Assert.Equal(0, code);
        Assert.All(mWriters, w => Assert.True(w.GetStatus().IsRegistered));
    }

    [Fact]
    public void UnregisterAll_RemovesFromEveryWriter()
    {
        using var log = new StringWriter();
        ClientRegistration.RegisterAll(mWriters, McpEndpoint.Default, log);

        var code = ClientRegistration.UnregisterAll(mWriters, log);

        Assert.Equal(0, code);
        Assert.All(mWriters, w => Assert.False(w.GetStatus().IsRegistered));
    }

    [Fact]
    public void Status_ReportsEachWriter()
    {
        using var log = new StringWriter();
        mWriters[0].Register(McpEndpoint.Default);

        var code = ClientRegistration.Status(mWriters, log);

        Assert.Equal(0, code);
        var output = log.ToString();
        Assert.Contains(mWriters[0].ClientName, output);
        Assert.Contains(mWriters[1].ClientName, output);
    }
}
