// SnoopSkill.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Installs canonical skills, retaining local edits and other clients' registrations.</summary>
public static class SnoopSkill
{
    /// <summary>The inspection skill's directory name.</summary>
    public const string FirstSkillName = "snoopmcp-first";
    /// <summary>The interaction and screenshot skill's directory name.</summary>
    public const string UiaSkillName = "snoopmcp-uia";
    /// <summary>Directory used by clients for skill discovery.</summary>
    public const string SkillsDirectoryName = "skills";
    private const string AgentsDirectoryName = ".agents";
    private const string SkillFileName = "SKILL.md";
    private const string ReceiptFileName = ".snoopmcp-install.json";
    private const string DefaultOwner = "Claude Code";
    // Fingerprints of unmarked bodies shipped before ownership records were introduced.
    private const string LegacyFirstHash = "3ED324B20219F98933F1B8991B4116D52433AB5E6749120B186374D2709F0B58";
    private const string LegacyUiaHash = "5D330C30220E7E7399485C77029BFDA02AF55FCC63247867DE3CCD1CC6679C67";

    /// <summary>Canonical repository files embedded at build time without duplicated instructions.</summary>
    public static IReadOnlyList<(string Name, string Body)> Definitions { get; } =
        [(FirstSkillName, ReadResource(FirstSkillName)), (UiaSkillName, ReadResource(UiaSkillName))];

    /// <summary>The documented shared user skill location for Codex and compatible local clients.</summary>
    public static string SharedUserDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AgentsDirectoryName, SkillsDirectoryName);

    /// <summary>Installs or upgrades owned, unedited skills. Conflicts are preserved and return false.</summary>
    public static bool Install(string skillsDir, string owner = DefaultOwner)
    {
        ArgumentException.ThrowIfNullOrEmpty(skillsDir);
        ArgumentException.ThrowIfNullOrEmpty(owner);
        return Apply(skillsDir, (dir, name, body) => InstallOne(dir, name, body, owner));
    }

    /// <summary>Removes ownership; only unedited owned files are deleted for the last client.</summary>
    public static bool Remove(string skillsDir, string owner = DefaultOwner)
    {
        ArgumentException.ThrowIfNullOrEmpty(skillsDir);
        ArgumentException.ThrowIfNullOrEmpty(owner);
        return Apply(skillsDir, (dir, _, _) => RemoveOne(dir, owner));
    }

    private static bool Apply(string skillsDir, Func<string, string, string, bool> action)
    {
        bool ok = true;
        foreach ((string name, string body) in Definitions)
        {
            try
            {
                string dir = Path.Combine(skillsDir, name);
                EnsureNotLinked(dir);
                ok = action(dir, name, body) && ok;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                ok = false;
            }
        }
        return ok;
    }

    private static bool InstallOne(string dir, string name, string body, string owner)
    {
        string path = Path.Combine(dir, SkillFileName);
        string receiptPath = Path.Combine(dir, ReceiptFileName);
        EnsureNotLinked(path);
        EnsureNotLinked(receiptPath);
        SkillReceipt? receipt = ReadReceipt(receiptPath);
        bool exists = File.Exists(path);
        string? currentHash = exists ? Hash(File.ReadAllText(path)) : null;
        bool legacy = exists && receipt is null && owner == DefaultOwner && IsLegacy(name, path);
        bool ok = !exists || (receipt is not null && receipt.Hashes.Contains(currentHash ?? string.Empty)) || legacy;
        if (ok)
        {
            Directory.CreateDirectory(dir);
            receipt ??= new SkillReceipt();
            receipt.Owners.Add(owner);
            // Record both versions before replacing the body so an interrupted upgrade can be retried.
            receipt.Hashes = currentHash is null ? [Hash(body)] : [currentHash, Hash(body)];
            AtomicFile.Write(receiptPath, JsonSerializer.Serialize(receipt));
            AtomicFile.Write(path, body);
            receipt.Hashes = [Hash(body)];
            AtomicFile.Write(receiptPath, JsonSerializer.Serialize(receipt));
        }
        return ok;
    }

    private static bool RemoveOne(string dir, string owner)
    {
        string path = Path.Combine(dir, SkillFileName);
        string receiptPath = Path.Combine(dir, ReceiptFileName);
        EnsureNotLinked(path);
        EnsureNotLinked(receiptPath);
        SkillReceipt? receipt = ReadReceipt(receiptPath);
        if (receipt is not null && receipt.Owners.Remove(owner))
        {
            if (receipt.Owners.Count > 0)
            {
                AtomicFile.Write(receiptPath, JsonSerializer.Serialize(receipt));
            }
            else
            {
                if (File.Exists(path) && receipt.Hashes.Contains(Hash(File.ReadAllText(path))))
                {
                    File.Delete(path);
                }
                File.Delete(receiptPath);
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                }
            }
        }
        return true;
    }

    private static SkillReceipt? ReadReceipt(string path)
    {
        SkillReceipt? result = null;
        if (File.Exists(path))
        {
            result = JsonSerializer.Deserialize<SkillReceipt>(File.ReadAllText(path));
            if (result is null || result.Version != 1 || result.Owners is null || result.Hashes is null)
            {
                throw new JsonException("Unrecognized SnoopMCP skill ownership record; preserved.");
            }
        }
        return result;
    }

    private static bool IsLegacy(string name, string path)
    {
        string normalized = File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd();
        return Hash(normalized) == (name == FirstSkillName ? LegacyFirstHash : LegacyUiaHash);
    }

    private static string Hash(string body) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));

    private static string ReadResource(string name)
    {
        using Stream stream = typeof(SnoopSkill).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing canonical skill resource: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void EnsureNotLinked(string path)
    {
        string? current = Path.GetFullPath(path);
        while (current is not null)
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Linked skill path is not installer-owned: {current}");
            }
            current = Path.GetDirectoryName(current);
        }
    }
}
