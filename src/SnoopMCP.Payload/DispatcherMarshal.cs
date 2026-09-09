// DispatcherMarshal.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload;

using System.Diagnostics;
using System.Windows.Threading;
using Protocol.Errors;

/// <summary>
/// Marshals work onto a WPF <see cref="Dispatcher"/> with a per-call timeout.
/// A stuck UI thread surfaces as a structured <see cref="ErrorCode.DispatcherTimeout"/> per call,
/// never as an indefinite hang of the payload.
/// </summary>
/// <remarks>
/// Every delegate this class hands to the dispatcher is wrapped in <see cref="DispatchOutcome{T}"/>,
/// so a payload exception never faults a <c>DispatcherOperation</c>: a waiting caller gets the original
/// exception rethrown, and an operation the caller has stopped waiting for (timeout, fire-and-forget)
/// has its error traced rather than left as an unobserved task fault that the HOST application's
/// crash reporter would record as its own crash (Raygun handoff, 2026-09-09).
/// </remarks>
public sealed class DispatcherMarshal
{
    private const string AbandonedInvokeContext = "Dispatcher invoke abandoned after timeout";
    private const string AbandonedMutationContext = "Mutating action abandoned as ActionPending";

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
    /// An exception thrown by <paramref name="work"/> is rethrown to the caller as-is.
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
        DispatcherOperation<DispatchOutcome<T>> operation = Dispatch(work, cancellationToken);
        bool completed = operation.Task.Wait(mTimeout, cancellationToken);
        T result;
        if (completed)
        {
            result = operation.Task.GetAwaiter().GetResult().GetValueOrRethrow();
        }
        else
        {
            // Abort() only succeeds while the operation is still queued. Work that has already
            // started runs to completion after the caller has gone; keep its outcome observed.
            operation.Abort();
            ObserveAbandoned(operation, AbandonedInvokeContext);
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
    /// An exception thrown by <paramref name="work"/> before the timeout is rethrown to the caller as-is.
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
        DispatcherOperation<DispatchOutcome<T>> operation = Dispatch(work, cancellationToken);
        bool completed = operation.Task.Wait(mTimeout, cancellationToken);
        T result;
        if (completed)
        {
            result = operation.Task.GetAwaiter().GetResult().GetValueOrRethrow();
        }
        else
        {
            // Deliberately do NOT Abort(): the mutation may already be mid-flight or applied on the
            // dispatcher thread, and aborting would not reverse it. Signal ActionPending so the caller
            // verifies the actual outcome instead of trusting a timeout that says nothing about state.
            ObserveAbandoned(operation, AbandonedMutationContext);
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
    /// no failure to the caller; an exception thrown by <paramref name="work"/> is traced and otherwise
    /// swallowed, so it can neither fault the dispatcher operation nor reach the host's unhandled-exception
    /// paths. The caller must observe the effect separately (e.g. by polling for the new window).
    /// </summary>
    /// <param name="work">The action to post to the dispatcher thread.</param>
    public void Post(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (mDispatcher.HasShutdownStarted || mDispatcher.HasShutdownFinished)
        {
            throw new InvalidOperationException("Dispatcher has been shut down.");
        }

        _ = mDispatcher.InvokeAsync(() => RunPosted(work), DispatcherPriority.Normal);
    }

    private DispatcherOperation<DispatchOutcome<T>> Dispatch<T>(Func<T> work, CancellationToken cancellationToken)
    {
        return mDispatcher.InvokeAsync(
            () => DispatchOutcome<T>.Capture(work),
            DispatcherPriority.Normal,
            cancellationToken);
    }

    /// <summary>
    /// Keeps an operation the caller has stopped waiting for observed: when it eventually completes,
    /// a captured error is traced. The continuation cannot throw, so it can never itself become an
    /// unobserved fault. A cancelled operation (aborted while still queued) has nothing to report.
    /// </summary>
    private static void ObserveAbandoned<T>(DispatcherOperation<DispatchOutcome<T>> operation, string context)
    {
        _ = operation.Task.ContinueWith(
            task => TraceAbandonedError(task, context),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void TraceAbandonedError<T>(Task<DispatchOutcome<T>> task, string context)
    {
        if (task.Status == TaskStatus.RanToCompletion && task.Result.Error is { } error)
        {
            Trace.WriteLine($"SnoopMCP payload: {context}; the work later threw {error.GetType().FullName}: {error.Message}");
        }
    }

    private static void RunPosted(Action work)
    {
        try
        {
            work();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"SnoopMCP payload: posted action threw {ex.GetType().FullName}: {ex.Message}");
        }
    }
}
