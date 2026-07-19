// CaptureResult.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Automation;

/// <summary>A background window capture: a base64 PNG plus its pixel dimensions.</summary>
/// <param name="Format">Always "png".</param>
public sealed record CaptureResult(string Format, int Width, int Height, string Base64);
