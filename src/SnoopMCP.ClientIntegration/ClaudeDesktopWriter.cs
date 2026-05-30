// ClaudeDesktopWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Registers SnoopMCP in Claude Desktop's <c>%APPDATA%\Claude\claude_desktop_config.json</c>, under
/// the <c>mcpServers</c> object. Claude Desktop has no native HTTP MCP transport, so the entry runs an
/// <c>npx mcp-remote</c> stdio bridge that proxies to the SnoopMCP HTTP endpoint. The SnoopMCP entry is
/// added/updated/removed in place via a mutable JSON DOM, so every other key in the file survives.
/// Writes are atomic (temp file then move).
/// </summary>
public sealed class ClaudeDesktopWriter : IClientWriter
{
    private const string ServersKey = "mcpServers";
    private const string CommandKey = "command";
    private const string ArgsKey = "args";
    private const string NpxCommand = "npx";
    private const string NpxCmd = "npx.cmd";
    private const string NpxExe = "npx.exe";
    private const string YesFlag = "-y";
    private const string McpRemotePackage = "mcp-remote@latest";
    private const string AllowHttpFlag = "--allow-http";
    private const string ClaudeDirName = "Claude";
    private const string ConfigFileName = "claude_desktop_config.json";
    private const string ClaudeDesktopClientName = "Claude Desktop";
    private const string PathEnvVar = "PATH";
    private const string TempSuffix = ".tmp";
    private const string NpxMissingNote =
        " Note: 'npx' was not found on PATH; install Node.js so the mcp-remote bridge can run.";

    private static readonly JsonSerializerOptions smWriteOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding smNoBomUtf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string mConfigPath;

    /// <summary>Initialises the writer against an explicit config path (used by tests).</summary>
    /// <param name="configPath">Absolute path to Claude Desktop's <c>claude_desktop_config.json</c>.</param>
    public ClaudeDesktopWriter(string configPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(configPath);
        mConfigPath = configPath;
    }

    /// <inheritdoc />
    public string ClientName => ClaudeDesktopClientName;

    /// <summary>
    /// Creates a writer targeting the current user's
    /// <c>%APPDATA%\Claude\claude_desktop_config.json</c>.
    /// </summary>
    public static ClaudeDesktopWriter ForCurrentUser()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new ClaudeDesktopWriter(Path.Combine(appData, ClaudeDirName, ConfigFileName));
    }

    /// <inheritdoc />
    /// <remarks>The entry is always written even when <c>npx</c> is absent; the returned message notes
    /// the missing dependency so the user can install Node.js.</remarks>
    public RegisterResult Register(McpEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        RegisterResult result;
        try
        {
            JsonObject root = LoadOrCreateRoot();
            JsonObject servers = GetOrAddObject(root, ServersKey);
            servers[endpoint.Name] = BuildBridgeEntry(endpoint);
            WriteAtomic(root);
            string message = $"Registered '{endpoint.Name}' in {ClientName}.";
            if (!IsNpxOnPath())
            {
                message += NpxMissingNote;
            }
            result = new RegisterResult(true, message);
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
                bool removed = root[ServersKey] is JsonObject servers
                    && servers.Remove(McpEndpoint.Default.Name);
                if (removed)
                {
                    WriteAtomic(root);
                }
                string message = removed
                    ? $"Removed SnoopMCP from {ClientName}."
                    : $"{ClientName}: SnoopMCP entry was not present.";
                result = new UnregisterResult(true, message);
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
        bool present = false;
        if (File.Exists(mConfigPath))
        {
            try
            {
                JsonObject root = LoadRoot();
                present = root[ServersKey] is JsonObject servers
                    && servers[McpEndpoint.Default.Name] is JsonObject entry
                    && string.Equals((string?) entry[CommandKey], NpxCommand, StringComparison.Ordinal)
                    && entry[ArgsKey] is JsonArray args
                    && args.Any(a => string.Equals((string?) a, McpEndpoint.Default.Url, StringComparison.Ordinal));
            }
            catch (JsonException)
            {
                present = false;
            }
        }
        return new StatusResult(present, present
            ? $"{ClientName}: SnoopMCP is registered."
            : $"{ClientName}: SnoopMCP is not registered.");
    }

    private static JsonObject BuildBridgeEntry(McpEndpoint endpoint)
    {
        return new JsonObject
        {
            [CommandKey] = NpxCommand,
            [ArgsKey] = new JsonArray
            {
                YesFlag,
                McpRemotePackage,
                endpoint.Url,
                AllowHttpFlag
            }
        };
    }

    private static bool IsNpxOnPath()
    {
        string path = Environment.GetEnvironmentVariable(PathEnvVar) ?? string.Empty;
        string[] candidates = { NpxCmd, NpxExe, NpxCommand };
        return path.Split(Path.PathSeparator)
            .Where(dir => !string.IsNullOrWhiteSpace(dir))
            .SelectMany(dir => candidates.Select(name => Path.Combine(dir, name)))
            .Any(File.Exists);
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
