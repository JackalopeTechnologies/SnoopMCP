// ElementHandleCache.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Automation;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Automation;

/// <summary>
/// Short-TTL cache mapping opaque handles ("&lt;pid&gt;:&lt;seq&gt;") to live <see cref="AutomationElement"/>s
/// plus the durable locator each was found by. Handles are monotonic and never reused. Expired or
/// unknown handles miss, letting the caller re-resolve by locator. <see cref="TryGet"/> also validates
/// that the handle's embedded pid matches the caller's expected pid, so a handle from one process can
/// never resolve an element in another.
/// </summary>
public sealed class ElementHandleCache
{
    private static readonly TimeSpan smDefaultTtl = TimeSpan.FromSeconds(60);

    private readonly TimeSpan mTtl;
    private readonly Func<DateTimeOffset> mClock;
    private readonly ConcurrentDictionary<string, Entry> mEntries = new(StringComparer.Ordinal);
    private long mSeq;

    /// <summary>Creates a cache with the default 60-second TTL and system clock.</summary>
    public ElementHandleCache() : this(smDefaultTtl, () => DateTimeOffset.UtcNow) { }

    /// <summary>Creates a cache with an explicit TTL and clock (tests inject both).</summary>
    public ElementHandleCache(TimeSpan ttl, Func<DateTimeOffset> clock)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be positive.");
        }
        ArgumentNullException.ThrowIfNull(clock);
        mTtl = ttl;
        mClock = clock;
    }

    /// <summary>Caches an element and returns a fresh handle of the form "&lt;pid&gt;:&lt;seq&gt;".</summary>
    public string Add(int pid, AutomationElement element, string? by, string? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        long seq = Interlocked.Increment(ref mSeq);
        string handle = $"{pid}:{seq}";
        mEntries[handle] = new Entry(element, by, value, mClock());
        return handle;
    }

    /// <summary>
    /// Resolves a handle to its element and locator; false when unknown, expired, or the handle's
    /// embedded pid does not match <paramref name="expectedPid"/>.
    /// </summary>
    // STR0010 suppression: 'element' is an out parameter — the analyzer treats it like an
    // input that must be validated, but validating an `out` is incoherent (it carries no
    // caller input). There is no alternative expression of this out-parameter that would
    // satisfy the analyzer while preserving the standard .NET `Try*` shape.
#pragma warning disable STR0010
    public bool TryGet(
        string handle,
        int expectedPid,
        [MaybeNullWhen(false)] out AutomationElement element,
        out string? by,
        out string? value)
#pragma warning restore STR0010
    {
        element = null;
        by = null;
        value = null;
        bool ok = false;
        if (handle != null && HandlePidMatches(handle, expectedPid) && mEntries.TryGetValue(handle, out Entry? entry))
        {
            bool fresh = mClock() - entry.Stamp <= mTtl;
            if (fresh)
            {
                element = entry.Element;
                by = entry.By;
                value = entry.Value;
                ok = true;
            }
            else
            {
                mEntries.TryRemove(handle, out _);
            }
        }
        return ok;
    }

    /// <summary>Parses the "&lt;pid&gt;:&lt;seq&gt;" prefix and compares it to the expected pid.</summary>
    private static bool HandlePidMatches(string handle, int expectedPid)
    {
        bool matches = false;
        if (!string.IsNullOrEmpty(handle))
        {
            int colon = handle.IndexOf(':');
            if (colon > 0
                && int.TryParse(handle.AsSpan(0, colon), out int pid)
                && pid == expectedPid)
            {
                matches = true;
            }
        }
        return matches;
    }

    private sealed record Entry(AutomationElement Element, string? By, string? Value, DateTimeOffset Stamp);
}
