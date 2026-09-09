// UnobservedFaultProbe.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

/// <summary>
/// Detects a faulted <see cref="Task"/> that nothing ever observed. Subscribes to
/// <see cref="TaskScheduler.UnobservedTaskException"/> for its lifetime and, on
/// <see cref="CollectAndCheckFired"/>, forces the finalizer pass that raises the event — the exact
/// mechanism by which a payload fault inside a <c>DispatcherOperation</c> reached the host
/// application's crash reporter (Raygun handoff, 2026-09-09). The event is process-wide, so each
/// probe filters on a predicate (a unique message marker or an exception type) to stay independent
/// of whatever other tests are doing in parallel.
/// </summary>
internal sealed class UnobservedFaultProbe : IDisposable
{
    private readonly Func<Exception, bool> mMatches;
    private int mFired;

    public UnobservedFaultProbe(Func<Exception, bool> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);
        mMatches = matches;
        TaskScheduler.UnobservedTaskException += OnUnobserved;
    }

    /// <summary>Creates a probe that matches any unobserved fault whose message contains <paramref name="marker"/>.</summary>
    public static UnobservedFaultProbe ForMessage(string marker)
    {
        ArgumentException.ThrowIfNullOrEmpty(marker);
        return new UnobservedFaultProbe(ex => ex.Message.Contains(marker, StringComparison.Ordinal));
    }

    /// <summary>
    /// Runs a full collection plus finalizer pass (twice, so a holder freed by the first finalizer
    /// pass is itself collected) and reports whether a matching unobserved fault was raised.
    /// </summary>
    public bool CollectAndCheckFired()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return Volatile.Read(ref mFired) != 0;
    }

    public void Dispose()
    {
        TaskScheduler.UnobservedTaskException -= OnUnobserved;
    }

    private void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (e.Exception.Flatten().InnerExceptions.Any(mMatches))
        {
            Interlocked.Exchange(ref mFired, 1);
            e.SetObserved();
        }
    }
}
