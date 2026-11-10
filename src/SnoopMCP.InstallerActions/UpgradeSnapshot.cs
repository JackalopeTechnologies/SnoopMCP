// UpgradeSnapshot.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.InstallerActions;

using System.Globalization;
using System.IO;
using System.Xml.Linq;

/// <summary>Preserves user files around legacy installer cleanup, including rollback.</summary>
public sealed class UpgradeSnapshot
{
    private const string ManifestName = "manifest.xml";
    private const string RootName = "snapshot";
    private const string DirectoryName = "directory";
    private const string FileName = "file";
    private const string TargetName = "target";
    private const string IndexName = "index";
    private const string BlobSuffix = ".data";
    private const string TemporarySuffix = ".snoop-upgrade-";
    private readonly string mDirectory;

    /// <summary>Uses a unique installer-owned directory outside the application's install folder.</summary>
    public UpgradeSnapshot(string directory) => mDirectory = Path.GetFullPath(directory);

    /// <summary>Copies selected files and directory contents, excluding files owned by the old package.</summary>
    public void Capture(IEnumerable<string> files, IEnumerable<string> directories, ISet<string> packageFiles)
    {
        files = files ?? throw new ArgumentNullException(nameof(files));
        directories = directories ?? throw new ArgumentNullException(nameof(directories));
        packageFiles = packageFiles ?? throw new ArgumentNullException(nameof(packageFiles));
        EnsureNotLinked(mDirectory);
        if (Directory.Exists(mDirectory))
        {
            throw new IOException("Upgrade snapshot already exists; refusing to overwrite it.");
        }
        Directory.CreateDirectory(mDirectory);
        var manifest = new XElement(RootName);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in files)
        {
            CaptureFile(path, packageFiles, seen, manifest);
        }
        foreach (string path in directories)
        {
            CaptureDirectory(path, packageFiles, seen, manifest);
        }
        manifest.Save(Path.Combine(mDirectory, ManifestName));
    }

    /// <summary>Restores the snapshot byte for byte. Safe to repeat during rollback.</summary>
    public void Restore()
    {
        XElement manifest = ReadManifest();
        foreach (XElement entry in manifest.Elements())
        {
            string target = RequiredAttribute(entry, TargetName);
            EnsureNotLinked(target);
            if (entry.Name == DirectoryName)
            {
                Directory.CreateDirectory(target);
            }
            else
            {
                RestoreFile(entry, target);
            }
        }
    }

    /// <summary>Deletes only snapshot files listed in its manifest; unexpected files are retained.</summary>
    public void Delete()
    {
        XElement manifest = ReadManifest();
        foreach (XElement entry in manifest.Elements(FileName))
        {
            File.Delete(BlobPath(entry));
        }
        File.Delete(Path.Combine(mDirectory, ManifestName));
        if (!Directory.EnumerateFileSystemEntries(mDirectory).Any())
        {
            Directory.Delete(mDirectory);
        }
    }

    private void CaptureDirectory(string path, ISet<string> packageFiles, ISet<string> seen, XElement manifest)
    {
        EnsureNotLinked(path);
        if (Directory.Exists(path))
        {
            manifest.Add(new XElement(DirectoryName, new XAttribute(TargetName, Path.GetFullPath(path))));
            foreach (string file in Directory.EnumerateFiles(path))
            {
                CaptureFile(file, packageFiles, seen, manifest);
            }
            foreach (string child in Directory.EnumerateDirectories(path))
            {
                CaptureDirectory(child, packageFiles, seen, manifest);
            }
        }
    }

    private void CaptureFile(string path, ISet<string> packageFiles, ISet<string> seen, XElement manifest)
    {
        EnsureNotLinked(path);
        string fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath) && !packageFiles.Contains(fullPath) && seen.Add(fullPath))
        {
            var entry = new XElement(FileName, new XAttribute(TargetName, fullPath),
                new XAttribute(IndexName, seen.Count));
            File.Copy(fullPath, BlobPath(entry));
            manifest.Add(entry);
        }
    }

    private void RestoreFile(XElement entry, string target)
    {
        if (entry.Name != FileName)
        {
            throw new IOException("Unrecognized upgrade snapshot entry.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(target) ?? throw new IOException("No target directory."));
        string temporary = target + TemporarySuffix + Guid.NewGuid().ToString("N");
        try
        {
            File.Copy(BlobPath(entry), temporary);
            if (File.Exists(target))
            {
                File.Replace(temporary, target, null);
            }
            else
            {
                File.Move(temporary, target);
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private XElement ReadManifest()
    {
        string path = Path.Combine(mDirectory, ManifestName);
        EnsureNotLinked(path);
        XElement result = XElement.Load(path);
        if (result.Name != RootName)
        {
            throw new IOException("Unrecognized upgrade snapshot manifest.");
        }
        return result;
    }

    private string BlobPath(XElement entry)
    {
        int index = int.Parse(RequiredAttribute(entry, IndexName), CultureInfo.InvariantCulture);
        string result = Path.Combine(mDirectory, index.ToString(CultureInfo.InvariantCulture) + BlobSuffix);
        EnsureNotLinked(result);
        return result;
    }

    private static string RequiredAttribute(XElement entry, string name)
        => (string?) entry.Attribute(name) ?? throw new IOException("Incomplete upgrade snapshot manifest.");

    private static void EnsureNotLinked(string path)
    {
        string? current = Path.GetFullPath(path);
        while (current is not null)
        {
            // GetAttributes also identifies directory junctions; never follow a link during migration.
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException($"Cannot safely migrate a linked path: {current}");
                }
            }
            catch (FileNotFoundException)
            {
                // A missing leaf will be created during restore; ancestors must still be checked.
            }
            catch (DirectoryNotFoundException)
            {
                // A missing ancestor is safe to create after checking its existing parents.
            }
            current = Path.GetDirectoryName(current);
        }
    }
}
