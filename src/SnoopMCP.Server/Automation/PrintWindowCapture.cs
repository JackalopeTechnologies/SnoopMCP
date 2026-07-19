// PrintWindowCapture.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Automation;

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using SnoopMCP.Protocol.Errors;

/// <summary>
/// Background window capture via <c>PrintWindow(PW_RENDERFULLCONTENT)</c>: renders the window's own
/// content (including GPU-composited surfaces) to an off-screen bitmap without raising or focusing it.
/// Occluded windows capture correctly; minimized windows cannot and surface <see cref="ErrorCode.CaptureUnavailable"/>.
/// </summary>
public sealed class PrintWindowCapture : IScreenCapture
{
    private const uint PwRenderFullContent = 0x00000002;
    private const string PngFormat = "png";

    /// <inheritdoc />
    public CaptureResult Capture(int pid)
    {
        nint hwnd = MainWindowHandle(pid);
        if (IsIconic(hwnd))
        {
            throw new SnoopMcpException(ErrorCode.CaptureUnavailable, "Window is minimized; cannot capture content.");
        }
        if (!GetWindowRect(hwnd, out Rect rect))
        {
            throw new SnoopMcpException(ErrorCode.CaptureUnavailable, "Could not read window bounds.");
        }
        int width = rect.pmRight - rect.pmLeft;
        int height = rect.pmBottom - rect.pmTop;
        if (width <= 0 || height <= 0)
        {
            throw new SnoopMcpException(ErrorCode.CaptureUnavailable, "Window has no drawable area.");
        }

        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            nint hdc = g.GetHdc();
            try
            {
                bool ok = PrintWindow(hwnd, hdc, PwRenderFullContent);
                if (!ok)
                {
                    throw new SnoopMcpException(ErrorCode.CaptureUnavailable, "PrintWindow failed.");
                }
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return new CaptureResult(PngFormat, width, height, Convert.ToBase64String(ms.ToArray()));
    }

    private static nint MainWindowHandle(int pid)
    {
        using Process process = Process.GetProcessById(pid);
        nint hwnd = process.MainWindowHandle;
        if (hwnd == nint.Zero)
        {
            throw new SnoopMcpException(ErrorCode.CaptureUnavailable, $"Process {pid} has no main window.");
        }
        return hwnd;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int pmLeft;
        public int pmTop;
        public int pmRight;
        public int pmBottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(nint hWnd, nint hdcBlt, uint nFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hWnd);
}
