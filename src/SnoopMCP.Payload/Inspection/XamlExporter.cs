// XamlExporter.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Inspection;

using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Markup;
using System.Xml;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Serializes an element to XAML via <see cref="XamlWriter.Save(object, XmlWriter)"/> — the snapshot
/// the LLM uses to see an object's current declarative state. Output reflects live values (evaluated
/// binding values, realized children, attached property values). Large subtrees are truncated at a
/// soft cap and flagged with a warning so the response stays bounded.
/// </summary>
public sealed class XamlExporter
{
    /// <summary>The default soft cap on serialized XAML, in bytes (256 KiB).</summary>
    public const int DefaultSoftCapBytes = 256 * 1024;

    private const int Utf8ContinuationMask = 0b1100_0000;
    private const int Utf8ContinuationMarker = 0b1000_0000;

    private readonly int mSoftCapBytes;

    /// <summary>
    /// Initialises a new <see cref="XamlExporter"/> with the <see cref="DefaultSoftCapBytes"/> cap.
    /// </summary>
    public XamlExporter()
        : this(DefaultSoftCapBytes)
    {
    }

    /// <summary>
    /// Initialises a new <see cref="XamlExporter"/> with an explicit soft cap.
    /// </summary>
    /// <param name="softCapBytes">The soft cap on serialized XAML, in bytes. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="softCapBytes"/> is not positive.</exception>
    public XamlExporter(int softCapBytes)
    {
        if (softCapBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(softCapBytes), "Soft cap must be positive.");
        }

        mSoftCapBytes = softCapBytes;
    }

    /// <summary>
    /// Serializes <paramref name="element"/> to XAML, truncating to the soft cap when oversized.
    /// </summary>
    /// <param name="element">The element to serialize.</param>
    /// <returns>The XAML, its full byte count, a truncation flag, and a warning when truncated.</returns>
    public ExportXamlResponse Export(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        string xaml = SerializeToXaml(element);
        int byteCount = Encoding.UTF8.GetByteCount(xaml);
        bool truncated = byteCount > mSoftCapBytes;
        string emitted = truncated ? TruncateUtf8(xaml, mSoftCapBytes) : xaml;
        string? warning = truncated ? BuildWarning(byteCount) : null;

        return new ExportXamlResponse(
            Xaml: emitted,
            ByteCount: byteCount,
            Truncated: truncated,
            Warning: warning);
    }

    private string BuildWarning(int byteCount)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"XAML payload of {byteCount} bytes exceeded soft cap of {mSoftCapBytes}; truncated.");
    }

    private static string SerializeToXaml(DependencyObject element)
    {
        var builder = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = true
        };
        using (XmlWriter writer = XmlWriter.Create(builder, settings))
        {
            XamlWriter.Save(element, writer);
        }

        return builder.ToString();
    }

    private static string TruncateUtf8(string source, int byteCap)
    {
        byte[] all = Encoding.UTF8.GetBytes(source);
        int safeLen = Math.Min(byteCap, all.Length);

        // Walk back to a valid UTF-8 boundary so we don't split a multi-byte code point.
        while (safeLen > 0 && (all[safeLen - 1] & Utf8ContinuationMask) == Utf8ContinuationMarker)
        {
            safeLen--;
        }

        return Encoding.UTF8.GetString(all, 0, safeLen);
    }
}
