// HostRunRecord.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

using System.Globalization;
using System.IO;

/// <summary>Distinguishes a clean host exit from termination that could not run shutdown logging.</summary>
public sealed class HostRunRecord
{
    private const string AppFolderName = "SnoopMCP";
    private const string LogsFolderName = "logs";
    private const string RunRecordName = "host-running.txt";
    private readonly string mPath;

    /// <summary>The lifecycle record shared by the tray host and explicit CLI stop.</summary>
    public static HostRunRecord ForCurrentUser() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName, LogsFolderName, RunRecordName));

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
