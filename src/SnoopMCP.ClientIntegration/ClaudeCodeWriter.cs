// ClaudeCodeWriter.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Registers SnoopMCP in Claude Code. Writes three things: the <c>mcpServers</c> entry in
/// <c>~/.claude.json</c> (with a millisecond <c>timeout</c>), a whole-server <c>permissions.allow</c>
/// entry (<c>mcp__snoopmcp</c>) in <c>~/.claude/settings.json</c> so the read-only tools are
/// pre-approved, and the SnoopMCP skills under <c>~/.claude/skills</c>.
/// </summary>
public sealed class ClaudeCodeWriter : JsonMcpServerWriter
{
    private const string ServersKey = "mcpServers";
    private const string ConfigFileName = ".claude.json";
    private const string ClaudeDirName = ".claude";
    private const string SettingsFileName = "settings.json";
    private const string SkillsDirName = "skills";
    private const string ClaudeCodeClientName = "Claude Code";
    private const string TimeoutKey = "timeout";
    private const string PermissionsKey = "permissions";
    private const string AllowKey = "allow";
    private const string WholeServerPermission = "mcp__snoopmcp";
    private const string TempSuffix = ".tmp";
    private const string MsgPermsAndSkill = " Pre-approved tools and installed the SnoopMCP skills.";
    private const string MsgPermsOnly = " Pre-approved tools; skill install failed.";
    private const string MsgSkillOnly = " Installed the SnoopMCP skills; permission write failed.";
    private const string MsgNeither = " Permission and skill writes failed.";
    private const int DefaultTimeoutSeconds = 120;
    private const int MinTimeoutSeconds = 10;
    private const int MillisPerSecond = 1000;

    private static readonly JsonSerializerOptions smWriteOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding smNoBomUtf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string mSettingsPath;
    private readonly string mSkillsDir;

    /// <summary>Initialises the writer against explicit paths (used by tests).</summary>
    /// <param name="configPath">Absolute path to <c>.claude.json</c>.</param>
    /// <param name="settingsPath">Absolute path to <c>~/.claude/settings.json</c>.</param>
    /// <param name="skillsDir">Absolute path to <c>~/.claude/skills</c>.</param>
    public ClaudeCodeWriter(string configPath, string settingsPath, string skillsDir)
        : base(configPath, ServersKey, configPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(settingsPath);
        ArgumentException.ThrowIfNullOrEmpty(skillsDir);
        mSettingsPath = settingsPath;
        mSkillsDir = skillsDir;
    }

    /// <inheritdoc />
    public override string ClientName => ClaudeCodeClientName;

    /// <summary>Creates a writer targeting the current user's Claude Code config.</summary>
    public static ClaudeCodeWriter ForCurrentUser()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string config = Path.Combine(profile, ConfigFileName);
        string claudeDir = Path.Combine(profile, ClaudeDirName);
        string settings = Path.Combine(claudeDir, SettingsFileName);
        string skills = Path.Combine(claudeDir, SkillsDirName);
        return new ClaudeCodeWriter(config, settings, skills);
    }

    /// <inheritdoc />
    /// <remarks>Adds a millisecond <c>timeout</c> to the base entry.</remarks>
    protected override JsonObject BuildEntry(McpEndpoint endpoint)
    {
        JsonObject entry = base.BuildEntry(endpoint);
        entry[TimeoutKey] = Math.Max(DefaultTimeoutSeconds, MinTimeoutSeconds) * MillisPerSecond;
        return entry;
    }

    /// <inheritdoc />
    /// <remarks>Also pre-approves the SnoopMCP tools and installs the SnoopMCP skills.</remarks>
    public override RegisterResult Register(McpEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        RegisterResult baseResult = base.Register(endpoint);
        RegisterResult result = baseResult;
        if (baseResult.Success)
        {
            bool perms = TryAddPermission();
            bool skill = SnoopSkill.Install(mSkillsDir);
            string extra = (perms, skill) switch
            {
                (true, true) => MsgPermsAndSkill,
                (true, false) => MsgPermsOnly,
                (false, true) => MsgSkillOnly,
                _ => MsgNeither
            };
            result = new RegisterResult(true, baseResult.Message + extra);
        }
        return result;
    }

    /// <inheritdoc />
    /// <remarks>Also removes the pre-approval permission and the SnoopMCP skills.</remarks>
    public override UnregisterResult Unregister()
    {
        UnregisterResult baseResult = base.Unregister();
        TryRemovePermission();
        SnoopSkill.Remove(mSkillsDir);
        return baseResult;
    }

    private bool TryAddPermission()
    {
        bool ok;
        try
        {
            JsonObject root = LoadJsonObjectOrEmpty(mSettingsPath);
            JsonObject permissions = GetOrAddObject(root, PermissionsKey);
            JsonArray allow = permissions[AllowKey] as JsonArray ?? [];
            permissions[AllowKey] = allow;
            bool present = allow.Any(n => string.Equals((string?) n, WholeServerPermission, StringComparison.Ordinal));
            if (!present)
            {
                allow.Add(WholeServerPermission);
            }
            WriteAtomic(mSettingsPath, root);
            ok = true;
        }
        catch (JsonException)
        {
            ok = false;
        }
        catch (IOException)
        {
            ok = false;
        }
        return ok;
    }

    private void TryRemovePermission()
    {
        if (File.Exists(mSettingsPath))
        {
            TryRemovePermissionCore();
        }
    }

    private void TryRemovePermissionCore()
    {
        try
        {
            JsonObject root = LoadJsonObjectOrEmpty(mSettingsPath);
            if (RemoveAllowEntry(root))
            {
                WriteAtomic(mSettingsPath, root);
            }
        }
        catch (JsonException)
        {
            // Leave a malformed settings file untouched.
        }
        catch (IOException)
        {
            // Best effort.
        }
    }

    private static bool RemoveAllowEntry(JsonObject root)
    {
        bool removed = false;
        if (root[PermissionsKey] is JsonObject permissions && permissions[AllowKey] is JsonArray allow)
        {
            JsonNode? match = allow.FirstOrDefault(n =>
                string.Equals((string?) n, WholeServerPermission, StringComparison.Ordinal));
            if (match is not null)
            {
                allow.Remove(match);
                removed = true;
            }
        }
        return removed;
    }

    private static JsonObject LoadJsonObjectOrEmpty(string path)
    {
        JsonObject root = new();
        if (File.Exists(path))
        {
            string text = File.ReadAllText(path);
            if (!string.IsNullOrWhiteSpace(text))
            {
                root = JsonNode.Parse(text) as JsonObject
                    ?? throw new JsonException("Root JSON value is not an object.");
            }
        }
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

    private static void WriteAtomic(string path, JsonObject root)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        string tmp = path + TempSuffix;
        File.WriteAllText(tmp, root.ToJsonString(smWriteOptions), smNoBomUtf8);
        File.Move(tmp, path, overwrite: true);
    }
}
