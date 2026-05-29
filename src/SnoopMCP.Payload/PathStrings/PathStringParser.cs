// PathStringParser.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload.PathStrings;

using SnoopMCP.Protocol.Errors;

/// <summary>
/// Parses canonical path strings of the form <c>/TypeName[Name='X', AutomationId='Y'][n]/...</c>
/// into an ordered sequence of <see cref="PathStep"/> records.
/// </summary>
public sealed class PathStringParser
{
    /// <summary>
    /// Parses <paramref name="path"/> into its constituent steps.
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
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new SnoopMcpException(ErrorCode.PathParseError, "Path is empty.");
        }
        if (path[0] != '/')
        {
            throw new SnoopMcpException(ErrorCode.PathParseError, "Path must start with '/'.");
        }

        string remaining = path[1..];
        string[] rawSteps = remaining.Split('/', StringSplitOptions.None);
        var steps = new List<PathStep>(rawSteps.Length);
        foreach (string raw in rawSteps.Where(s => s.Length > 0))
        {
            steps.Add(ParseStep(raw));
        }
        if (steps.Count == 0)
        {
            throw new SnoopMcpException(ErrorCode.PathParseError, "Path has no steps.");
        }
        return steps;
    }

    private static PathStep ParseStep(string raw)
    {
        int firstBracket = raw.IndexOf('[');
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
        {
            throw new SnoopMcpException(ErrorCode.PathParseError, $"Step '{raw}' has no type name.");
        }

        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        int? index = null;

        while (remainder.Length > 0)
        {
            int close = remainder.IndexOf(']');
            if (close < 0)
            {
                throw new SnoopMcpException(ErrorCode.PathParseError, $"Unclosed '[' in step '{raw}'.");
            }
            string inside = remainder[1..close];
            remainder = remainder[(close + 1)..];

            bool isIndex = int.TryParse(inside, out int parsedIndex);
            if (isIndex)
            {
                index = parsedIndex;
            }
            else
            {
                ParseAttributes(inside, attributes, raw);
            }
        }

        return new PathStep(typeName, attributes, index);
    }

    private static void ParseAttributes(string inside, Dictionary<string, string> attributes, string fullStep)
    {
        string[] pairs = inside.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (string pair in pairs)
        {
            int equals = pair.IndexOf('=');
            if (equals < 0)
            {
                throw new SnoopMcpException(
                    ErrorCode.PathParseError,
                    $"Attribute '{pair}' missing '=' in step '{fullStep}'.");
            }
            string key = pair[..equals].Trim();
            string valuePart = pair[(equals + 1)..].Trim();
            bool isQuoted = valuePart.Length >= 2 && valuePart[0] == '\'' && valuePart[^1] == '\'';
            if (!isQuoted)
            {
                throw new SnoopMcpException(
                    ErrorCode.PathParseError,
                    $"Attribute value '{valuePart}' must be single-quoted in step '{fullStep}'.");
            }
            attributes[key] = valuePart[1..^1];
        }
    }
}
