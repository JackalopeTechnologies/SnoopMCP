// DetectionTests.cs
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

using Xunit;

#endregion

namespace SnoopMCP.ClientIntegration.Tests;

/// <summary>Tests <see cref="IClientWriter.IsDetected" /> for present/absent agent directories.</summary>
public sealed class DetectionTests : IDisposable
{
    public DetectionTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-detect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
    }

    private readonly string mDir;

    public void Dispose()
    {
        if (Directory.Exists(mDir)) Directory.Delete(mDir, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void IsDetected_FalseWhenDirMissing()
    {
        var missing = Path.Combine(mDir, "nope");
        var writer = new CursorWriter(Path.Combine(missing, "mcp.json"), missing);

        Assert.False(writer.IsDetected());
    }

    [Fact]
    public void IsDetected_TrueWhenDirExists()
    {
        var writer = new CursorWriter(Path.Combine(mDir, "mcp.json"), mDir);

        Assert.True(writer.IsDetected());
    }

    [Fact]
    public void IsDetected_TrueWhenConfigFileExists()
    {
        var missing = Path.Combine(mDir, "nope");
        var config = Path.Combine(mDir, "mcp.json");
        File.WriteAllText(config, "{}");
        var writer = new CursorWriter(config, missing);

        Assert.True(writer.IsDetected());
    }

    [Fact]
    public void ClaudeDesktop_IsDetected_FalseWhenAbsent()
    {
        var missing = Path.Combine(mDir, "nope");
        var writer = new ClaudeDesktopWriter(Path.Combine(missing, "claude_desktop_config.json"), missing);

        Assert.False(writer.IsDetected());
    }

    [Fact]
    public void Codex_IsDetected_FalseWhenAbsent()
    {
        var missing = Path.Combine(mDir, "nope");
        var writer = new CodexWriter(Path.Combine(missing, "config.toml"), missing);

        Assert.False(writer.IsDetected());
    }
}
