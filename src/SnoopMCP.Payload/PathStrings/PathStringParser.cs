// PathStringParser.cs
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

using SnoopMCP.Protocol.Errors;

#endregion

namespace SnoopMCP.Payload.PathStrings;

/// <summary>
///     Parses canonical path strings of the form <c>/TypeName[Name='X', AutomationId='Y'][n]/...</c>
///     into an ordered sequence of <see cref="PathStep" /> records.
/// </summary>
public sealed class PathStringParser
{
    /// <summary>
    ///     Parses <paramref name="path" /> into its constituent steps.
    /// </summary>
    /// <param name="path">A canonical path string. Must start with <c>/</c> and have at least one step.</param>
    /// <returns>The parsed steps, in document order.</returns>
    // CA1822 suppression: instance method by design — host-side DI replaces this with an
    // alternate parser when path semantics evolve. Forcing static would lock that out.
#pragma warning disable CA1822
    public IReadOnlyList<PathStep> Parse(string path)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path)) throw new SnoopMcpException(ErrorCode.PathParseError, "Path is empty.");
        if (path[0] != '/') throw new SnoopMcpException(ErrorCode.PathParseError, "Path must start with '/'.");

        var remaining = path[1..];
        var rawSteps = remaining.Split('/');
        var steps = new List<PathStep>(rawSteps.Length);
        foreach (var raw in rawSteps.Where(s => s.Length > 0)) steps.Add(ParseStep(raw));
        if (steps.Count == 0) throw new SnoopMcpException(ErrorCode.PathParseError, "Path has no steps.");
        return steps;
    }

    private static PathStep ParseStep(string raw)
    {
        var firstBracket = raw.IndexOf('[');
        string typeName;
        string remainder;
        if (firstBracket < 0)
        {
            typeName = raw;
            remainder = string.Empty;
        }
        else
        {
            typeName = raw[..firstBracket];
            remainder = raw[firstBracket..];
        }

        if (string.IsNullOrWhiteSpace(typeName))
            throw new SnoopMcpException(ErrorCode.PathParseError, $"Step '{raw}' has no type name.");

        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        int? index = null;

        while (remainder.Length > 0)
        {
            var close = remainder.IndexOf(']');
            if (close < 0) throw new SnoopMcpException(ErrorCode.PathParseError, $"Unclosed '[' in step '{raw}'.");
            var inside = remainder[1..close];
            remainder = remainder[(close + 1)..];

            var isIndex = int.TryParse(inside, out var parsedIndex);
            if (isIndex)
                index = parsedIndex;
            else
                ParseAttributes(inside, attributes, raw);
        }

        return new PathStep(typeName, attributes, index);
    }

    private static void ParseAttributes(string inside, Dictionary<string, string> attributes, string fullStep)
    {
        var pairs = inside.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var equals = pair.IndexOf('=');
            if (equals < 0)
                throw new SnoopMcpException(
                    ErrorCode.PathParseError,
                    $"Attribute '{pair}' missing '=' in step '{fullStep}'.");
            var key = pair[..equals].Trim();
            var valuePart = pair[(equals + 1)..].Trim();
            var isQuoted = valuePart.Length >= 2 && valuePart[0] == '\'' && valuePart[^1] == '\'';
            if (!isQuoted)
                throw new SnoopMcpException(
                    ErrorCode.PathParseError,
                    $"Attribute value '{valuePart}' must be single-quoted in step '{fullStep}'.");
            attributes[key] = valuePart[1..^1];
        }
    }
}
