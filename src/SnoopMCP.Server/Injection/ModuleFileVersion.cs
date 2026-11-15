// ModuleFileVersion.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Injection;

using System.Diagnostics;
using System.IO;

/// <summary>Reads optional version metadata without requiring a loaded module's file to remain on disk.</summary>
internal static class ModuleFileVersion
{
    internal const string Unknown = "Unknown";

    internal static string Read(string fileName)
    {
        string version = Unknown;
        try
        {
            version = FileVersionInfo.GetVersionInfo(fileName).FileVersion ?? Unknown;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // A runtime update can remove an old file while a process still has its module loaded.
            // Losing optional version metadata must not prevent discovery or attachment to that process.
        }
        return version;
    }
}
