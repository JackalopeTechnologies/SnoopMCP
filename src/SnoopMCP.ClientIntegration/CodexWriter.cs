// CodexWriter.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#region Usings

using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;

#endregion

namespace SnoopMCP.ClientIntegration;

/// <summary>
///     Registers SnoopMCP in OpenAI Codex's <c>~/.codex/config.toml</c> under the
///     <c>[mcp_servers.snoopmcp]</c> table as a Streamable-HTTP server (a <c>url</c> entry). The entry is
///     added, updated, or removed in place via the TOML model, so every other table and key in the file
///     survives. Writes are atomic (temp file then move).
/// </summary>
public sealed class CodexWriter : IClientWriter
{
    /// <summary>Initialises the writer against explicit paths (used by tests).</summary>
    /// <param name="configPath">Absolute path to Codex's <c>config.toml</c>.</param>
    /// <param name="detectionPath">Directory whose existence means Codex is installed.</param>
    public CodexWriter(string configPath, string detectionPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(configPath);
        ArgumentException.ThrowIfNullOrEmpty(detectionPath);
        mConfigPath = configPath;
        mDetectionPath = detectionPath;
    }

    private readonly string mConfigPath;
    private readonly string mDetectionPath;

    /// <inheritdoc />
    public string ClientName => CodexClientName;

    /// <inheritdoc />
    public bool IsDetected()
    {
        return Directory.Exists(mDetectionPath) || File.Exists(mConfigPath);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Only <see cref="McpEndpoint.Url" /> is written under the server table: Codex infers HTTP
    ///     transport from the presence of <c>url</c>, so <see cref="McpEndpoint.Type" /> is intentionally not
    ///     persisted. A top-level <c>[features] experimental_use_rmcp_client = true</c> flag is also written:
    ///     Codex gates streamable-HTTP MCP servers behind it.
    /// </remarks>
    public RegisterResult Register(McpEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        RegisterResult result;
        if (TryLoadModel(out TomlTable root, out var error))
        {
            TomlTable servers = GetOrAddTable(root, ServersTableKey);
            servers[endpoint.Name] = new TomlTable { { UrlKey, endpoint.Url } };
            TomlTable features = GetOrAddTable(root, FeaturesTableKey);
            features[RmcpFlagKey] = true;
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
    /// <remarks>
    ///     Only the server-table entry is removed; the <c>[features]</c> flag is left in place — it
    ///     is harmless and may be shared by other Codex MCP servers.
    /// </remarks>
    public UnregisterResult Unregister()
    {
        UnregisterResult result;
        if (!File.Exists(mConfigPath))
        {
            result = new UnregisterResult(true, $"{ClientName}: no config file; nothing to remove.");
        }
        else
        {
            if (TryLoadModel(out TomlTable root, out var error))
            {
                var removed = root.TryGetValue(ServersTableKey, out var value)
                              && value is TomlTable servers
                              && servers.Remove(McpEndpoint.Default.Name);
                if (removed) WriteAtomic(root);
                var detail = removed
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
        var present = File.Exists(mConfigPath)
                      && TryLoadModel(out TomlTable root, out _)
                      && root.TryGetValue(ServersTableKey, out var serversValue)
                      && serversValue is TomlTable servers
                      && servers.TryGetValue(McpEndpoint.Default.Name, out var entryValue)
                      && entryValue is TomlTable entry
                      && entry.TryGetValue(UrlKey, out var url)
                      && string.Equals(url as string, McpEndpoint.Default.Url, StringComparison.Ordinal);
        return new StatusResult(present, present
            ? $"{ClientName}: SnoopMCP is registered."
            : $"{ClientName}: SnoopMCP is not registered.");
    }

    /// <summary>Creates a writer targeting the current user's <c>~/.codex/config.toml</c>.</summary>
    public static CodexWriter ForCurrentUser()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dir = Path.Combine(profile, CodexDirName);
        return new CodexWriter(Path.Combine(dir, ConfigFileName), dir);
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
        if (parent.TryGetValue(key, out var existing) && existing is TomlTable table)
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
        var dir = Path.GetDirectoryName(mConfigPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = mConfigPath + TempSuffix;
        File.WriteAllText(tmp, Toml.FromModel(root));
        File.Move(tmp, mConfigPath, true);
    }

    private const string ServersTableKey = "mcp_servers";
    private const string FeaturesTableKey = "features";
    private const string RmcpFlagKey = "experimental_use_rmcp_client";
    private const string UrlKey = "url";
    private const string CodexDirName = ".codex";
    private const string ConfigFileName = "config.toml";
    private const string CodexClientName = "Codex";
    private const string TempSuffix = ".tmp";
    private const string ParseErrorFallback = "parse error";
}
