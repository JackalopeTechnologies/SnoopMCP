// JsonMcpServerWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Shared logic for registering the SnoopMCP server in a JSON-config MCP client. Subclasses supply
/// the config file path and the name of the object that holds server entries (Claude Code uses
/// <c>mcpServers</c>; VS Code uses <c>servers</c>). The SnoopMCP entry is added/updated/removed in
/// place via a mutable JSON DOM, so every other key in the file survives unchanged. Writes are
/// atomic (temp file then move).
/// </summary>
public abstract class JsonMcpServerWriter : IClientWriter
{
    private const string TypeKey = "type";
    private const string UrlKey = "url";
    private const string TempSuffix = ".tmp";

    private static readonly JsonSerializerOptions smWriteOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding smNoBomUtf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string mConfigPath;
    private readonly string mServersKey;

    /// <summary>Initialises the writer.</summary>
    /// <param name="configPath">Absolute path to the client's JSON config file.</param>
    /// <param name="serversKey">Name of the object holding server entries (e.g. <c>mcpServers</c>).</param>
    protected JsonMcpServerWriter(string configPath, string serversKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(configPath);
        ArgumentException.ThrowIfNullOrEmpty(serversKey);
        mConfigPath = configPath;
        mServersKey = serversKey;
    }

    /// <inheritdoc />
    public abstract string ClientName { get; }

    /// <inheritdoc />
    public RegisterResult Register(McpEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        RegisterResult result;
        try
        {
            JsonObject root = LoadOrCreateRoot();
            JsonObject servers = GetOrAddObject(root, mServersKey);
            servers[endpoint.Name] = new JsonObject
            {
                [TypeKey] = endpoint.Type,
                [UrlKey] = endpoint.Url
            };
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
    public UnregisterResult Unregister()
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
    public StatusResult GetStatus()
    {
        StatusResult result;
        bool present = false;
        if (File.Exists(mConfigPath))
        {
            try
            {
                JsonObject root = LoadRoot();
                present = root[mServersKey] is JsonObject servers
                    && servers[McpEndpoint.Default.Name] is JsonObject entry
                    && string.Equals((string?)entry[UrlKey], McpEndpoint.Default.Url, StringComparison.Ordinal);
            }
            catch (JsonException)
            {
                // Malformed config is treated as "not registered".
            }
        }
        result = new StatusResult(present, present
            ? $"{ClientName}: SnoopMCP is registered."
            : $"{ClientName}: SnoopMCP is not registered.");
        return result;
    }

    private JsonObject LoadOrCreateRoot()
    {
        JsonObject root = File.Exists(mConfigPath) ? LoadRoot() : new JsonObject();
        return root;
    }

    private JsonObject LoadRoot()
    {
        string text = File.ReadAllText(mConfigPath);
        JsonNode? parsed = string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text);
        JsonObject root = parsed as JsonObject
            ?? throw new JsonException("Root JSON value is not an object.");
        return root;
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
        string json = root.ToJsonString(smWriteOptions);
        File.WriteAllText(tmp, json, smNoBomUtf8);
        File.Move(tmp, mConfigPath, overwrite: true);
    }
}
