// WalkthroughTranscript.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.IntegrationTests;

using System.Runtime.CompilerServices;
using System.Text.Json;
using SnoopMCP.Protocol.Wire;

/// <summary>
/// Buffers <see cref="WalkthroughRecord"/> entries during a capture run and writes them to
/// <c>walkthrough-transcript.json</c> beside this source file. The doc author cherry-picks excerpts
/// from the committed JSON into <c>docs/walkthrough.md</c>.
/// </summary>
public sealed class WalkthroughTranscript
{
    private const string TranscriptFileName = "walkthrough-transcript.json";

    private readonly List<WalkthroughRecord> mRecords = new();

    public void Add(string scene, string tool, object request, JsonElement response)
    {
        ArgumentException.ThrowIfNullOrEmpty(scene);
        ArgumentException.ThrowIfNullOrEmpty(tool);
        ArgumentNullException.ThrowIfNull(request);
        mRecords.Add(new WalkthroughRecord(scene, tool, request, response.Clone()));
    }

    /// <summary>Writes the buffered records to the JSON file beside this source file.</summary>
    public async Task WriteAsync([CallerFilePath] string callerPath = "")
    {
        string dir = Path.GetDirectoryName(callerPath) ?? AppContext.BaseDirectory;
        string target = Path.Combine(dir, TranscriptFileName);
        string json = JsonSerializer.Serialize(mRecords, WireSerializer.JsonOptions);
        await File.WriteAllTextAsync(target, json);
    }
}
