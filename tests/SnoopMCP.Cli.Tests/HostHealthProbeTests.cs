// HostHealthProbeTests.cs
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

using System.Net;
using Xunit;

#endregion

namespace SnoopMCP.Cli.Tests;

public sealed class HostHealthProbeTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public StubHandler(Func<HttpResponseMessage> responder)
        {
            mResponder = responder;
        }

        private readonly Func<HttpResponseMessage> mResponder;

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

        var healthy = await HostHealthProbe.IsHealthyAsync(client, "http://127.0.0.1:6300/health", default);

        Assert.True(healthy);
    }

    [Fact]
    public async Task IsHealthyAsync_On500_ReturnsFalse()
    {
        using var client = new HttpClient(
            new StubHandler(() => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var healthy = await HostHealthProbe.IsHealthyAsync(client, "http://127.0.0.1:6300/health", default);

        Assert.False(healthy);
    }

    [Fact]
    public async Task IsHealthyAsync_OnConnectionRefused_ReturnsFalse()
    {
        using var client = new HttpClient(
            new StubHandler(() => throw new HttpRequestException("refused")));

        var healthy = await HostHealthProbe.IsHealthyAsync(client, "http://127.0.0.1:6300/health", default);

        Assert.False(healthy);
    }
}
