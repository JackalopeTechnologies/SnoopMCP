// AtomicFile.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

/// <summary>Replaces a file without exposing a partial write or reusing another operation's temporary file.</summary>
internal static class AtomicFile
{
    private const string TempPrefix = ".snoopmcp-";
    private const string TempSuffix = ".tmp";
    internal static void Write(string path, string content)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        string temp = path + TempPrefix + Guid.NewGuid().ToString("N") + TempSuffix;
        try
        {
            File.WriteAllText(temp, content);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
