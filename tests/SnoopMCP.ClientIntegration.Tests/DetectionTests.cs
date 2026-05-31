// DetectionTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration.Tests;

using SnoopMCP.ClientIntegration;
using Xunit;

/// <summary>Tests <see cref="IClientWriter.IsDetected"/> for present/absent agent directories.</summary>
public sealed class DetectionTests : IDisposable
{
    private readonly string mDir;

    public DetectionTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-detect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
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
    public void IsDetected_FalseWhenDirMissing()
    {
        string missing = Path.Combine(mDir, "nope");
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
        string missing = Path.Combine(mDir, "nope");
        string config = Path.Combine(mDir, "mcp.json");
        File.WriteAllText(config, "{}");
        var writer = new CursorWriter(config, missing);

        Assert.True(writer.IsDetected());
    }

    [Fact]
    public void ClaudeDesktop_IsDetected_FalseWhenAbsent()
    {
        string missing = Path.Combine(mDir, "nope");
        var writer = new ClaudeDesktopWriter(Path.Combine(missing, "claude_desktop_config.json"), missing);

        Assert.False(writer.IsDetected());
    }

    [Fact]
    public void Codex_IsDetected_FalseWhenAbsent()
    {
        string missing = Path.Combine(mDir, "nope");
        var writer = new CodexWriter(Path.Combine(missing, "config.toml"), missing);

        Assert.False(writer.IsDetected());
    }
}
