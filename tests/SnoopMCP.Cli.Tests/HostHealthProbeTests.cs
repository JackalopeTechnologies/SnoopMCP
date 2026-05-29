// HostHealthProbeTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli.Tests;

using System.Net;
using SnoopMCP.Cli;
using Xunit;

public sealed class HostHealthProbeTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> mResponder;

        public StubHandler(Func<HttpResponseMessage> responder)
        {
            mResponder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(mResponder());
        }
    }

    [Fact]
    public async Task IsHealthyAsync_On200_ReturnsTrue()
    {
        using var client = new HttpClient(new StubHandler(() => new HttpResponseMessage(HttpStatusCode.OK)));

        bool healthy = await HostHealthProbe.IsHealthyAsync(client, "http://127.0.0.1:6300/health", default);

        Assert.True(healthy);
    }

    [Fact]
    public async Task IsHealthyAsync_On500_ReturnsFalse()
    {
        using var client = new HttpClient(
            new StubHandler(() => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        bool healthy = await HostHealthProbe.IsHealthyAsync(client, "http://127.0.0.1:6300/health", default);

        Assert.False(healthy);
    }

    [Fact]
    public async Task IsHealthyAsync_OnConnectionRefused_ReturnsFalse()
    {
        using var client = new HttpClient(
            new StubHandler(() => throw new HttpRequestException("refused")));

        bool healthy = await HostHealthProbe.IsHealthyAsync(client, "http://127.0.0.1:6300/health", default);

        Assert.False(healthy);
    }
}
