// DispatcherMarshal.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload;

using System.Windows.Threading;
using Protocol.Errors;

/// <summary>
/// Marshals work onto a WPF <see cref="Dispatcher"/> with a per-call timeout.
/// A stuck UI thread surfaces as a structured <see cref="ErrorCode.DispatcherTimeout"/> per call,
/// never as an indefinite hang of the payload.
/// </summary>
public sealed class DispatcherMarshal
{
    private static readonly TimeSpan smDefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Gets the default per-call timeout when none is supplied.</summary>
    public static TimeSpan DefaultTimeout => smDefaultTimeout;

    private readonly Dispatcher mDispatcher;
    private readonly TimeSpan mTimeout;

    /// <summary>
    /// Initialises a new <see cref="DispatcherMarshal"/> with the <see cref="DefaultTimeout"/>.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to marshal work onto.</param>
    public DispatcherMarshal(Dispatcher dispatcher) : this(dispatcher, smDefaultTimeout)
    {
    }

    /// <summary>
    /// Initialises a new <see cref="DispatcherMarshal"/> with an explicit per-call timeout.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to marshal work onto.</param>
    /// <param name="timeout">The per-call timeout; must be positive.</param>
    public DispatcherMarshal(Dispatcher dispatcher, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
        }
        mDispatcher = dispatcher;
        mTimeout = timeout;
    }

    /// <summary>
    /// Runs <paramref name="work"/> on the marshalled dispatcher and returns the result.
    /// When the call already runs on the dispatcher thread, <paramref name="work"/> executes inline.
    /// Otherwise the call blocks until either the dispatcher finishes the work, the timeout elapses
    /// (raising <see cref="ErrorCode.DispatcherTimeout"/>), or <paramref name="cancellationToken"/> is signalled.
    /// </summary>
    /// <typeparam name="T">The return type of <paramref name="work"/>.</typeparam>
    /// <param name="work">The function to execute on the dispatcher thread.</param>
    /// <param name="cancellationToken">A token to observe while waiting.</param>
    /// <returns>The value returned by <paramref name="work"/>.</returns>
    public T Invoke<T>(Func<T> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        cancellationToken.ThrowIfCancellationRequested();

        if (mDispatcher.HasShutdownStarted || mDispatcher.HasShutdownFinished)
        {
            throw new InvalidOperationException("Dispatcher has been shut down.");
        }

        T result;
        bool onDispatcherThread = mDispatcher.CheckAccess();
        if (onDispatcherThread)
        {
            result = work();
        }
        else
        {
            result = InvokeFromForeignThread(work, cancellationToken);
        }
        return result;
    }

    private T InvokeFromForeignThread<T>(Func<T> work, CancellationToken cancellationToken)
    {
        var operation = mDispatcher.InvokeAsync(work, DispatcherPriority.Normal, cancellationToken);
        bool completed = operation.Task.Wait(mTimeout, cancellationToken);
        T result;
        if (completed)
        {
            result = operation.Task.GetAwaiter().GetResult();
        }
        else
        {
            operation.Abort();
            throw new SnoopMcpException(
                ErrorCode.DispatcherTimeout,
                $"Dispatcher invoke exceeded {mTimeout.TotalMilliseconds:F0}ms.");
        }
        return result;
    }

    /// <summary>
    /// Runs a MUTATING <paramref name="work"/> on the marshalled dispatcher and returns the result.
    /// Identical to <see cref="Invoke{T}"/> except for what happens on timeout: because <paramref name="work"/>
    /// mutates target state, it may already have started (or finished) executing on the dispatcher thread by
    /// the time the wait expires. Aborting the queued operation at that point would not undo an in-flight
    /// mutation, so unlike <see cref="Invoke{T}"/> this method does NOT call <c>Abort()</c> on timeout — doing
    /// so would only stop the caller from awaiting a result that might still land, without changing whether the
    /// mutation actually applies. Instead the caller receives <see cref="ErrorCode.ActionPending"/>, a signal
    /// that the outcome is unknown rather than a (false) guarantee that nothing happened, so the caller can
    /// verify with a follow-up read (e.g. <c>waitForValue</c> or <c>captureWindow</c>).
    /// </summary>
    /// <typeparam name="T">The return type of <paramref name="work"/>.</typeparam>
    /// <param name="work">The mutating function to execute on the dispatcher thread.</param>
    /// <param name="cancellationToken">A token to observe while waiting.</param>
    /// <returns>The value returned by <paramref name="work"/>.</returns>
    public T InvokeMutating<T>(Func<T> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        cancellationToken.ThrowIfCancellationRequested();

        if (mDispatcher.HasShutdownStarted || mDispatcher.HasShutdownFinished)
        {
            throw new InvalidOperationException("Dispatcher has been shut down.");
        }

        T result;
        bool onDispatcherThread = mDispatcher.CheckAccess();
        if (onDispatcherThread)
        {
            result = work();
        }
        else
        {
            result = InvokeMutatingFromForeignThread(work, cancellationToken);
        }
        return result;
    }

    private T InvokeMutatingFromForeignThread<T>(Func<T> work, CancellationToken cancellationToken)
    {
        var operation = mDispatcher.InvokeAsync(work, DispatcherPriority.Normal, cancellationToken);
        bool completed = operation.Task.Wait(mTimeout, cancellationToken);
        T result;
        if (completed)
        {
            result = operation.Task.GetAwaiter().GetResult();
        }
        else
        {
            // Deliberately do NOT Abort(): the mutation may already be mid-flight or applied on the
            // dispatcher thread, and aborting would not reverse it. Signal ActionPending so the caller
            // verifies the actual outcome instead of trusting a timeout that says nothing about state.
            throw new SnoopMcpException(
                ErrorCode.ActionPending,
                $"Mutating action did not confirm within {mTimeout.TotalMilliseconds:F0}ms; it may have applied. Verify with waitForValue or captureWindow.");
        }
        return result;
    }

    /// <summary>
    /// Posts <paramref name="work"/> to the marshalled dispatcher and returns immediately, without waiting
    /// for it to run. Intended for actions that spin a nested message loop on the dispatcher thread — such as
    /// opening a modal dialog — which would never return to a caller that awaited them, stalling the serial
    /// request pipe for as long as the loop runs. Because this method does not wait, it reports no result and
    /// no failure; the caller must observe the effect separately (e.g. by polling for the new window).
    /// </summary>
    /// <param name="work">The action to post to the dispatcher thread.</param>
    public void Post(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (mDispatcher.HasShutdownStarted || mDispatcher.HasShutdownFinished)
        {
            throw new InvalidOperationException("Dispatcher has been shut down.");
        }

        _ = mDispatcher.InvokeAsync(work, DispatcherPriority.Normal);
    }
}
