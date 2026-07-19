// PrintWindowCaptureTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.IntegrationTests;

using System.Diagnostics;
using Host.Automation;
using Xunit;

/// <summary>
/// Interactive background-capture gate: launches the real SampleWpfApp and drives
/// <see cref="PrintWindowCapture"/> against it out-of-process. Mirrors the launch fixture in
/// <see cref="EndToEndTests"/>/<see cref="UiaDriverTests"/>.
/// </summary>
public sealed class PrintWindowCaptureTests : IDisposable
{
    private const int WaitForInputIdleMs = 5000;
    private const int SettleDelayMs = 1000;
    private const string PngFormat = "png";
    private const int MinPngByteLength = 100;

    private static readonly byte[] smPngSignature = [0x89, 0x50, 0x4E, 0x47];

    private readonly Process mApp;

    public PrintWindowCaptureTests()
    {
        string samplePath = Path.Combine(AppContext.BaseDirectory, "SampleWpfApp.exe");
        Assert.True(File.Exists(samplePath), $"SampleWpfApp.exe not found at {samplePath}.");

        mApp = Process.Start(new ProcessStartInfo { FileName = samplePath, UseShellExecute = false })
            ?? throw new InvalidOperationException("Failed to start SampleWpfApp.");
        mApp.WaitForInputIdle(WaitForInputIdleMs);

        // Let the visual tree settle.
        Thread.Sleep(SettleDelayMs);
    }

    public void Dispose()
    {
        if (!mApp.HasExited)
        {
            mApp.Kill(entireProcessTree: true);
            mApp.WaitForExit(5000);
        }
        mApp.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Capture_ReturnsNonEmptyPng_ForVisibleWindow()
    {
        var capture = new PrintWindowCapture();
        CaptureResult result = capture.Capture(mApp.Id);

        Assert.Equal(PngFormat, result.Format);
        Assert.True(result.Width > 0 && result.Height > 0);
        byte[] bytes = Convert.FromBase64String(result.Base64);
        Assert.True(bytes.Length > MinPngByteLength);
        Assert.Equal(smPngSignature, bytes[..4]);
        await Task.CompletedTask;
    }
}
