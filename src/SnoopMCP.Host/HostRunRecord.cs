// HostRunRecord.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host;

using System.Globalization;
using System.IO;

/// <summary>Distinguishes a clean host exit from termination that could not run shutdown logging.</summary>
public sealed class HostRunRecord
{
    private readonly string mPath;

    /// <summary>Creates a lifecycle record at an explicit path.</summary>
    public HostRunRecord(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        mPath = path;
    }

    /// <summary>Marks startup and returns whether the previous run ended without a clean shutdown.</summary>
    public bool Begin()
    {
        bool interrupted = File.Exists(mPath);
        string? directory = Path.GetDirectoryName(mPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(mPath, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        return interrupted;
    }

    /// <summary>Marks an orderly shutdown; the next startup will not report an interrupted run.</summary>
    public void Complete() => File.Delete(mPath);
}
