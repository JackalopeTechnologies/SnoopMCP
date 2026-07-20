// UiaElementInfo.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Automation;

/// <summary>Facts about a discovered UIA element, plus a fresh cache reference for driving it.</summary>
public sealed record UiaElementInfo(
    UiaElementRef Reference,
    string AutomationId,
    string Name,
    string ControlType,
    string HelpText,
    double X,
    double Y,
    double Width,
    double Height,
    IReadOnlyList<string> Patterns);
