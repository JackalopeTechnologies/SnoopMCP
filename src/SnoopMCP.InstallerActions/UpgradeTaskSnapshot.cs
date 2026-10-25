// UpgradeTaskSnapshot.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.InstallerActions;

using System.IO;
using System.Xml.Linq;

/// <summary>Restores the prior autostart definition after legacy cleanup or failed new registration.</summary>
public sealed class UpgradeTaskSnapshot
{
    private const string FileName = "autostart.xml";
    private const string RootName = "autostart";
    private const string HostAttribute = "host";
    private const string TaskNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";
    private const string ActionsElement = "Actions";
    private const string ExecElement = "Exec";
    private const string CommandElement = "Command";
    private readonly string mPath;

    /// <summary>Uses the same unique directory as the installer's file snapshot.</summary>
    public UpgradeTaskSnapshot(string directory)
    {
        directory = directory ?? throw new ArgumentNullException(nameof(directory));
        mPath = Path.Combine(directory, FileName);
    }

    /// <summary>Captures an owned task, or its absence, before any old-product removal.</summary>
    public void Capture(string hostPath, Func<string?> read)
    {
        hostPath = hostPath ?? throw new ArgumentNullException(nameof(hostPath));
        read = read ?? throw new ArgumentNullException(nameof(read));
        string? xml = read();
        ValidateOwnership(xml, hostPath);
        new XElement(RootName, new XAttribute(HostAttribute, hostPath), xml ?? string.Empty).Save(mPath);
    }

    /// <summary>Restores settings without starting the task or replacing a different application's task.</summary>
    public void Restore(Func<string?> read, Action<string> write, Action remove)
    {
        read = read ?? throw new ArgumentNullException(nameof(read));
        write = write ?? throw new ArgumentNullException(nameof(write));
        remove = remove ?? throw new ArgumentNullException(nameof(remove));
        // Older scratch probes contain only a file snapshot.
        if (File.Exists(mPath))
        {
            XElement saved = XElement.Load(mPath);
            if (saved.Name != RootName)
            {
                throw new IOException("Unrecognized autostart snapshot.");
            }
            string host = (string?) saved.Attribute(HostAttribute) ?? throw new IOException("Incomplete autostart snapshot.");
            string? current = read();
            ValidateOwnership(current, host);
            if (saved.Value.Length > 0)
            {
                ValidateOwnership(saved.Value, host);
                write(saved.Value);
            }
            else
            {
                if (current is not null)
                {
                    remove();
                }
            }
        }
    }

    /// <summary>Removes only this snapshot's task definition after commit.</summary>
    public void Delete() => File.Delete(mPath);

    private static void ValidateOwnership(string? xml, string host)
    {
        if (xml is not null)
        {
            XNamespace ns = TaskNamespace;
            XElement task = XElement.Parse(xml);
            XElement[] actions = task.Elements(ns + ActionsElement).Elements().ToArray();
            string? command = actions.Length == 1 && actions[0].Name == ns + ExecElement
                ? (string?) actions[0].Element(ns + CommandElement) : null;
            if (command is null || !string.Equals(Path.GetFullPath(command.Trim('"')), Path.GetFullPath(host), StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("The SnoopMCP autostart name belongs to another task; it has not been changed.");
            }
        }
    }
}
