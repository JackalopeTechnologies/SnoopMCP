// WalkthroughTranscript.cs
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

using System.Runtime.CompilerServices;
using System.Text.Json;
using SnoopMCP.Protocol.Wire;

#endregion

namespace SnoopMCP.IntegrationTests;

/// <summary>
///     Buffers <see cref="WalkthroughRecord" /> entries during a capture run and writes them to
///     <c>walkthrough-transcript.json</c> beside this source file. The doc author cherry-picks excerpts
///     from the committed JSON into <c>docs/walkthrough.md</c>.
/// </summary>
public sealed class WalkthroughTranscript
{
    private readonly List<WalkthroughRecord> mRecords = new();

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
        var target = Path.Combine(smSourceDirectory, TranscriptFileName);
        var json = JsonSerializer.Serialize(mRecords, WireSerializer.JsonOptions);
        await File.WriteAllTextAsync(target, json);
    }

    private static string ResolveSourceDirectory([CallerFilePath] string thisFilePath = "")
    {
        var dir = Path.GetDirectoryName(thisFilePath) ?? AppContext.BaseDirectory;
        return dir;
    }

    private const string TranscriptFileName = "walkthrough-transcript.json";

    private static readonly string smSourceDirectory = ResolveSourceDirectory();
}
