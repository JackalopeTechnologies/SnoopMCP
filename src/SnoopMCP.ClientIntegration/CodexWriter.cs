// CodexWriter.cs
// Copyright (c) 2026 Jackalope Technologies

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
public sealed class CodexWriter : IClientWriter
{
    private const string ServersTableKey = "mcp_servers";
    private const string UrlKey = "url";
    private const string CodexDirName = ".codex";
    private const string ConfigFileName = "config.toml";
    private const string CodexClientName = "Codex";
    private const string TempSuffix = ".tmp";
    private const string ParseErrorFallback = "parse error";

    private readonly string mConfigPath;

    /// <summary>Initialises the writer against an explicit config path (used by tests).</summary>
    /// <param name="configPath">Absolute path to Codex's <c>config.toml</c>.</param>
    public CodexWriter(string configPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(configPath);
        mConfigPath = configPath;
    }

    /// <inheritdoc />
    public string ClientName => CodexClientName;

    /// <summary>Creates a writer targeting the current user's <c>~/.codex/config.toml</c>.</summary>
    public static CodexWriter ForCurrentUser()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new CodexWriter(Path.Combine(profile, CodexDirName, ConfigFileName));
    }

    /// <inheritdoc />
    public RegisterResult Register(McpEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        RegisterResult result;
        if (TryLoadModel(out TomlTable root, out string? error))
        {
            TomlTable servers = GetOrAddTable(root, ServersTableKey);
            servers[endpoint.Name] = new TomlTable { { UrlKey, endpoint.Url } };
            WriteAtomic(root);
            result = new RegisterResult(true, $"Registered '{endpoint.Name}' in {ClientName}.");
        }
        else
        {
            result = new RegisterResult(false, $"{ClientName} config is not valid TOML: {error}");
        }
        return result;
    }

    /// <inheritdoc />
    public UnregisterResult Unregister()
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
                bool removed = root.TryGetValue(ServersTableKey, out object? value)
                    && value is TomlTable servers
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
            && root.TryGetValue(ServersTableKey, out object? serversValue)
            && serversValue is TomlTable servers
            && servers.TryGetValue(McpEndpoint.Default.Name, out object? entryValue)
            && entryValue is TomlTable entry
            && entry.TryGetValue(UrlKey, out object? url)
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
                root = doc.ToModel();
                ok = true;
            }
        }
        return ok;
    }

    private static TomlTable GetOrAddTable(TomlTable parent, string key)
    {
        TomlTable child;
        if (parent.TryGetValue(key, out object? existing) && existing is TomlTable table)
        {
            child = table;
        }
        else
        {
            child = new TomlTable();
            parent[key] = child;
        }
        return child;
    }

    private void WriteAtomic(TomlTable root)
    {
        string? dir = Path.GetDirectoryName(mConfigPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        string tmp = mConfigPath + TempSuffix;
        File.WriteAllText(tmp, Toml.FromModel(root));
        File.Move(tmp, mConfigPath, overwrite: true);
    }
}
