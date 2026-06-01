// WalkthroughTranscript.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.IntegrationTests;

using System.Runtime.CompilerServices;
using System.Text.Json;
using Protocol.Wire;

/// <summary>
/// Buffers <see cref="WalkthroughRecord"/> entries during a capture run and writes them to
/// <c>walkthrough-transcript.json</c> beside this source file. The doc author cherry-picks excerpts
/// from the committed JSON into <c>docs/walkthrough.md</c>.
/// </summary>
public sealed class WalkthroughTranscript
{
    private const string TranscriptFileName = "walkthrough-transcript.json";

    private static readonly string smSourceDirectory = ResolveSourceDirectory();

    private readonly List<WalkthroughRecord> mRecords = [];

    public void Add(string scene, string tool, object request, JsonElement response)
    {
        ArgumentException.ThrowIfNullOrEmpty(scene);
        ArgumentException.ThrowIfNullOrEmpty(tool);
        ArgumentNullException.ThrowIfNull(request);
        mRecords.Add(new WalkthroughRecord(scene, tool, request, response.Clone()));
    }

    /// <summary>Writes the buffered records to <c>walkthrough-transcript.json</c> beside this source file.</summary>
    public async Task WriteAsync()
    {
        string target = Path.Combine(smSourceDirectory, TranscriptFileName);
        string json = JsonSerializer.Serialize(mRecords, WireSerializer.JsonOptions);
        await File.WriteAllTextAsync(target, json);
    }

    private static string ResolveSourceDirectory([CallerFilePath] string thisFilePath = "")
    {
        string dir = Path.GetDirectoryName(thisFilePath) ?? AppContext.BaseDirectory;
        return dir;
    }
}
