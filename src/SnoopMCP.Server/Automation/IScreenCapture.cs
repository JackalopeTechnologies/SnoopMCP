// IScreenCapture.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Automation;

/// <summary>Captures a target window's rendered content to a PNG, even when occluded.</summary>
public interface IScreenCapture
{
    /// <summary>Captures the main window of the target process.</summary>
    CaptureResult Capture(int pid);
}
