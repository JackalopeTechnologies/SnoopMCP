// ClientRegistrationTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli.Tests;

using SnoopMCP.ClientIntegration;
using SnoopMCP.Cli;
using Xunit;

public sealed class ClientRegistrationTests : IDisposable
{
    private readonly string mDir;
    private readonly IReadOnlyList<IClientWriter> mWriters;

    public ClientRegistrationTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mWriters =
        [
            new ClaudeCodeWriter(Path.Combine(mDir, ".claude.json")),
            new VsCodeMcpWriter(Path.Combine(mDir, "mcp.json"))
        ];
    }

    public void Dispose()
    {
        if (Directory.Exists(mDir))
        {
            Directory.Delete(mDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void RegisterAll_RegistersEveryWriter_AndReturnsOk()
    {
        using var log = new StringWriter();

        int code = ClientRegistration.RegisterAll(mWriters, McpEndpoint.Default, log);

        Assert.Equal(0, code);
        Assert.All(mWriters, w => Assert.True(w.GetStatus().IsRegistered));
    }

    [Fact]
    public void UnregisterAll_RemovesFromEveryWriter()
    {
        using var log = new StringWriter();
        ClientRegistration.RegisterAll(mWriters, McpEndpoint.Default, log);

        int code = ClientRegistration.UnregisterAll(mWriters, log);

        Assert.Equal(0, code);
        Assert.All(mWriters, w => Assert.False(w.GetStatus().IsRegistered));
    }

    [Fact]
    public void Status_ReportsEachWriter()
    {
        using var log = new StringWriter();
        mWriters[0].Register(McpEndpoint.Default);

        int code = ClientRegistration.Status(mWriters, log);

        Assert.Equal(0, code);
        string output = log.ToString();
        Assert.Contains(mWriters[0].ClientName, output);
        Assert.Contains(mWriters[1].ClientName, output);
    }
}
