// JsonMcpServerWriter.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Shared logic for registering the SnoopMCP server in a JSON-config MCP client. Subclasses supply the
/// config path, the servers-container key, the URL field name, and whether a <c>type</c> field is
/// emitted. The SnoopMCP entry is added/updated/removed in place via a mutable JSON DOM, so every other
/// key survives. Writes are atomic (temp file then move). Entry shape and status matching are
/// overridable for clients with a non-standard entry.
/// </summary>
public abstract class JsonMcpServerWriter : IClientWriter
{
    /// <summary>Conventional <c>type</c> field name.</summary>
    protected const string TypeKey = "type";

    private const string DefaultUrlKey = "url";
    private const string TempSuffix = ".tmp";

    private static readonly JsonSerializerOptions smWriteOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding smNoBomUtf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string mConfigPath;
    private readonly string mServersKey;
    private readonly string mUrlKey;
    private readonly bool mEmitType;
    private readonly string? mServerType;
    private readonly string mDetectionPath;

    /// <summary>Initialises the writer.</summary>
    /// <param name="configPath">Absolute path to the client's JSON config file.</param>
    /// <param name="serversKey">Name of the object holding server entries (e.g. <c>mcpServers</c>).</param>
    /// <param name="detectionPath">Directory/file whose existence means the agent is installed.</param>
    /// <param name="urlKey">Field name the URL is written under (default <c>url</c>).</param>
    /// <param name="emitType">When true, a <c>type</c> field is written.</param>
    /// <param name="serverType">
    /// Value for <c>type</c> when <paramref name="emitType"/> is true and this is non-null; otherwise
    /// the endpoint's own <see cref="McpEndpoint.Type"/> is used.
    /// </param>
    protected JsonMcpServerWriter(
        string configPath,
        string serversKey,
        string detectionPath,
        string urlKey = DefaultUrlKey,
        bool emitType = true,
        string? serverType = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(configPath);
        ArgumentException.ThrowIfNullOrEmpty(serversKey);
        ArgumentException.ThrowIfNullOrEmpty(detectionPath);
        ArgumentException.ThrowIfNullOrEmpty(urlKey);
        mConfigPath = configPath;
        mServersKey = serversKey;
        mDetectionPath = detectionPath;
        mUrlKey = urlKey;
        mEmitType = emitType;
        mServerType = serverType;
    }

    /// <inheritdoc />
    public abstract string ClientName { get; }

    /// <summary>The URL field name this client uses (e.g. <c>url</c>, <c>httpUrl</c>, <c>serverUrl</c>).</summary>
    protected string UrlKey => mUrlKey;

    /// <inheritdoc />
    public virtual bool IsDetected()
    {
        return Directory.Exists(mDetectionPath) || File.Exists(mDetectionPath) || File.Exists(mConfigPath);
    }

    /// <inheritdoc />
    public virtual RegisterResult Register(McpEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        RegisterResult result;
        try
        {
            JsonObject root = LoadOrCreateRoot();
            JsonObject servers = GetOrAddObject(root, mServersKey);
            servers[endpoint.Name] = BuildEntry(endpoint);
            WriteAtomic(root);
            result = new RegisterResult(true, $"Registered '{endpoint.Name}' in {ClientName}.");
        }
        catch (JsonException ex)
        {
            result = new RegisterResult(false, $"{ClientName} config is not valid JSON: {ex.Message}");
        }
        return result;
    }

    /// <inheritdoc />
    public virtual UnregisterResult Unregister()
    {
        UnregisterResult result;
        if (!File.Exists(mConfigPath))
        {
            result = new UnregisterResult(true, $"{ClientName}: no config file; nothing to remove.");
        }
        else
        {
            try
            {
                JsonObject root = LoadRoot();
                bool removed = root[mServersKey] is JsonObject servers
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
            catch (JsonException ex)
            {
                result = new UnregisterResult(false, $"{ClientName} config is not valid JSON: {ex.Message}");
            }
        }
        return result;
    }

    /// <inheritdoc />
    public virtual StatusResult GetStatus()
    {
        bool present = false;
        if (File.Exists(mConfigPath))
        {
            try
            {
                JsonObject root = LoadRoot();
                present = root[mServersKey] is JsonObject servers
                    && servers[McpEndpoint.Default.Name] is JsonObject entry
                    && EntryMatches(entry);
            }
            catch (JsonException)
            {
                // Malformed config is treated as "not registered".
            }
        }
        return new StatusResult(present, present
            ? $"{ClientName}: SnoopMCP is registered."
            : $"{ClientName}: SnoopMCP is not registered.");
    }

    /// <summary>Builds the JSON entry written under the server name. Override for a non-standard shape.</summary>
    protected virtual JsonObject BuildEntry(McpEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var entry = new JsonObject();
        if (mEmitType)
        {
            entry[TypeKey] = mServerType ?? endpoint.Type;
        }
        entry[mUrlKey] = endpoint.Url;
        return entry;
    }

    /// <summary>Returns true when an existing entry is recognised as SnoopMCP's. Override if needed.</summary>
    protected virtual bool EntryMatches(JsonObject entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return string.Equals((string?) entry[mUrlKey], McpEndpoint.Default.Url, StringComparison.Ordinal);
    }

    private JsonObject LoadOrCreateRoot()
    {
        return File.Exists(mConfigPath) ? LoadRoot() : new JsonObject();
    }

    private JsonObject LoadRoot()
    {
        string text = File.ReadAllText(mConfigPath);
        JsonNode? parsed = string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text);
        return parsed as JsonObject ?? throw new JsonException("Root JSON value is not an object.");
    }

    private static JsonObject GetOrAddObject(JsonObject parent, string key)
    {
        JsonObject child;
        if (parent[key] is JsonObject existing)
        {
            child = existing;
        }
        else
        {
            child = new JsonObject();
            parent[key] = child;
        }
        return child;
    }

    private void WriteAtomic(JsonObject root)
    {
        string? dir = Path.GetDirectoryName(mConfigPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        string tmp = mConfigPath + TempSuffix;
        File.WriteAllText(tmp, root.ToJsonString(smWriteOptions), smNoBomUtf8);
        File.Move(tmp, mConfigPath, overwrite: true);
    }
}
