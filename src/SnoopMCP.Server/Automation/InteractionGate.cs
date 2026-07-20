// InteractionGate.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Automation;

using System.Text.Json;

/// <summary>
/// The host-side safety switch for mutating driving tools. Default OFF. State is a tiny JSON file
/// under %LOCALAPPDATA%\SnoopMCP so the tray (which toggles it) and the running server (which reads
/// it per call) coordinate without a shared instance. Read-only tools ignore this gate.
/// </summary>
public sealed class InteractionGate
{
    private const string AppDirName = "SnoopMCP";
    private const string StateFileName = "interaction-gate.json";
    private const string TempSuffix = ".tmp";

    private readonly string mStatePath;

    /// <summary>Creates a gate backed by an explicit state-file path (tests pass a temp path).</summary>
    public InteractionGate(string statePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(statePath);
        mStatePath = statePath;
    }

    /// <summary>Creates a gate backed by the per-user state file.</summary>
    public static InteractionGate ForCurrentUser() => new(DefaultStatePath());

    /// <summary>The per-user state-file path under %LOCALAPPDATA%\SnoopMCP.</summary>
    public static string DefaultStatePath()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, AppDirName, StateFileName);
    }

    /// <summary>True when mutating driving tools are permitted. Reads the file each call; default false.</summary>
    public bool IsEnabled
    {
        get
        {
            bool enabled = false;
            try
            {
                if (File.Exists(mStatePath))
                {
                    string text = File.ReadAllText(mStatePath);
                    GateState? state = JsonSerializer.Deserialize<GateState>(text);
                    enabled = state?.Enabled ?? false;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                enabled = false;
            }
            return enabled;
        }
    }

    /// <summary>Enables or disables the gate, persisting atomically.</summary>
    public void SetEnabled(bool enabled)
    {
        string? dir = Path.GetDirectoryName(mStatePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        string tmp = mStatePath + TempSuffix;
        File.WriteAllText(tmp, JsonSerializer.Serialize(new GateState(enabled)));
        File.Move(tmp, mStatePath, overwrite: true);
    }

    private sealed record GateState(bool Enabled);
}
