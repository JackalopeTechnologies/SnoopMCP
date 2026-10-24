// TomlMcpServerWriter.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;

/// <summary>
/// Registers SnoopMCP in OpenAI Codex's <c>~/.codex/config.toml</c> under the
/// <c>[mcp_servers.snoopmcp]</c> table as a Streamable-HTTP server (a <c>url</c> entry). The entry is
/// added, updated, or removed in place via the TOML model, so every other table and key in the file
/// survives. Writes are atomic (temp file then move).
/// </summary>
public abstract class TomlMcpServerWriter : IClientWriter
{
    private const string ServersTableKey = "mcp_servers";
    private const string UrlKey = "url";
    private const string ParseErrorFallback = "parse error";
    private const string SkillsRemoved = " Released owned skills; local edits and other clients' skills are retained.";
    private const string SkillsRemovalFailed = " Skill cleanup failed; files were preserved.";

    private readonly string mConfigPath;
    private readonly string mDetectionPath;
    private readonly string mSkillsDir;

    /// <summary>Initialises the writer against explicit paths (used by tests).</summary>
    /// <param name="configPath">Absolute path to Codex's <c>config.toml</c>.</param>
    /// <param name="detectionPath">Directory whose existence means Codex is installed.</param>
    /// <param name="skillsDir">Skill discovery directory.</param>
    protected TomlMcpServerWriter(string configPath, string detectionPath, string skillsDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(configPath);
        ArgumentException.ThrowIfNullOrEmpty(detectionPath);
        ArgumentException.ThrowIfNullOrEmpty(skillsDir);
        mConfigPath = configPath;
        mDetectionPath = detectionPath;
        mSkillsDir = skillsDir;
    }

    /// <inheritdoc />
    public abstract string ClientName { get; }

    /// <inheritdoc />
    public bool IsDetected() => Directory.Exists(mDetectionPath) || File.Exists(mConfigPath);

    /// <inheritdoc />
    /// <remarks>No experimental flags, enablement, filters, or permission settings are written.</remarks>
    public RegisterResult Register(McpEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        RegisterResult result;
        try
        {
            if (TryLoadModel(out TomlTable root, out string? error))
            {
                TomlTable servers = GetOrAddTable(root, ServersTableKey);
                TomlTable entry = GetOrAddTable(servers, endpoint.Name);
                entry[UrlKey] = endpoint.Url;
                WriteAtomic(root);
                bool skills = SnoopSkill.Install(mSkillsDir, ClientName);
                result = new RegisterResult(skills, $"Registered '{endpoint.Name}' in {ClientName}. "
                    + (skills ? $"Installed SnoopMCP skills in {mSkillsDir}. Refresh the client connection; restart if needed."
                        : $"Skills incomplete in {mSkillsDir}: existing edits, linked paths, or write errors were preserved."));
            }
            else
            {
                result = new RegisterResult(false, $"{ClientName} config is not valid TOML: {error}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TomlException)
        {
            result = new RegisterResult(false, $"{ClientName}: {ex.Message}");
        }
        return result;
    }

    /// <inheritdoc />
    /// <remarks>Client configuration files and feature preferences are retained.</remarks>
    public UnregisterResult Unregister()
    {
        UnregisterResult result;
        try
        {
            result = RemoveConnection();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TomlException)
        {
            result = new UnregisterResult(false, $"{ClientName}: {ex.Message}");
        }
        bool skills = SnoopSkill.Remove(mSkillsDir, ClientName);
        return new UnregisterResult(result.Success && skills, result.Message
            + (skills ? SkillsRemoved : SkillsRemovalFailed));
    }

    private UnregisterResult RemoveConnection()
    {
        UnregisterResult result;
        if (!File.Exists(mConfigPath))
        {
            result = new UnregisterResult(true, $"{ClientName}: no config file; nothing to remove.");
        }
        else
        {
            if (TryLoadModel(out TomlTable root, out string? error))
            {
                bool removed = root.TryGetValue(ServersTableKey, out object value)
                    && value is TomlTable servers
                    && servers.TryGetValue(McpEndpoint.Default.Name, out object entryValue)
                    && entryValue is TomlTable entry
                    && entry.TryGetValue(UrlKey, out object url)
                    && string.Equals(url as string, McpEndpoint.Default.Url, StringComparison.Ordinal)
                    && servers.Remove(McpEndpoint.Default.Name);
                if (removed)
                {
                    WriteAtomic(root);
                }
                string detail = removed
                    ? $"Removed SnoopMCP from {ClientName}."
                    : $"{ClientName}: SnoopMCP entry was not present.";
                result = new UnregisterResult(true, detail);
            }
            else
            {
                result = new UnregisterResult(false, $"{ClientName} config is not valid TOML: {error}");
            }
        }
        return result;
    }

    /// <inheritdoc />
    public StatusResult GetStatus()
    {
        bool present = File.Exists(mConfigPath)
            && TryLoadModel(out TomlTable root, out _)
            && root.TryGetValue(ServersTableKey, out object serversValue)
            && serversValue is TomlTable servers
            && servers.TryGetValue(McpEndpoint.Default.Name, out object entryValue)
            && entryValue is TomlTable entry
            && entry.TryGetValue(UrlKey, out object url)
            && string.Equals(url as string, McpEndpoint.Default.Url, StringComparison.Ordinal);
        return new StatusResult(present, present
            ? $"{ClientName}: SnoopMCP is registered."
            : $"{ClientName}: SnoopMCP is not registered.");
    }

    private bool TryLoadModel(out TomlTable root, out string? error)
    {
        bool ok;
        error = null;
        if (!File.Exists(mConfigPath))
        {
            root = new TomlTable();
            ok = true;
        }
        else
        {
            DocumentSyntax doc = Toml.Parse(File.ReadAllText(mConfigPath), mConfigPath);
            if (doc.HasErrors)
            {
                root = new TomlTable();
                error = doc.Diagnostics.Count > 0 ? doc.Diagnostics[0].Message : ParseErrorFallback;
                ok = false;
            }
            else
            {
                // ToModel runs a second (model-build) pass that can throw even when parsing
                // succeeded - e.g. a key defined both as a value and a table - so treat that as a
                // clean failure rather than letting it escape the "never throws" contract.
                try
                {
                    root = doc.ToModel();
                    ok = true;
                }
                catch (TomlException ex)
                {
                    root = new TomlTable();
                    error = ex.Message;
                    ok = false;
                }
            }
        }
        return ok;
    }

    private static TomlTable GetOrAddTable(TomlTable parent, string key)
    {
        TomlTable child;
        if (parent.TryGetValue(key, out object existing) && existing is TomlTable table)
        {
            child = table;
        }
        else
        {
            child = new TomlTable();
            if (parent.ContainsKey(key))
            {
                throw new IOException($"Expected table '{key}'; existing value was preserved.");
            }
            parent[key] = child;
        }
        return child;
    }

    private void WriteAtomic(TomlTable root)
    {
        AtomicFile.Write(mConfigPath, Toml.FromModel(root));
    }
}
