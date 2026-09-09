// DispatchOutcome.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload;

using System.Runtime.ExceptionServices;

/// <summary>
/// The outcome of running a marshalled delegate on the dispatcher thread: either the value it returned
/// or the exception it threw, captured so that the delegate handed to <c>Dispatcher.InvokeAsync</c>
/// itself never throws.
/// </summary>
/// <remarks>
/// A delegate that throws inside a <c>DispatcherOperation</c> faults the operation's <see cref="Task"/>.
/// If nothing then reads that task (the fire-and-forget path never does; the timeout paths stop
/// waiting before the fault lands) the fault reaches the GC finalizer unobserved and is raised through
/// <see cref="TaskScheduler.UnobservedTaskException"/> in the HOST application, where a crash reporter
/// such as Raygun records a payload tool error as a host crash. Capturing here keeps every payload
/// exception on the payload's side of that boundary: the operation always completes, the marshal
/// rethrows the captured exception (original stack preserved) to the caller that is still waiting, and
/// an abandoned operation's error is traced instead of leaking.
/// <para>
/// The success value is held in a closure rather than a <c>T?</c> field so the error state never has to
/// fabricate a <c>default(T)</c> for an unconstrained <typeparamref name="T"/>, which nullable analysis
/// rejects without the null-forgiving operator the coding standard forbids.
/// </para>
/// </remarks>
/// <typeparam name="T">The delegate's return type.</typeparam>
internal sealed class DispatchOutcome<T>
{
    private readonly Func<T> mUnwrap;

    private DispatchOutcome(Func<T> unwrap, Exception? error)
    {
        mUnwrap = unwrap;
        Error = error;
    }

    /// <summary>Gets the exception the delegate threw, or <c>null</c> when it returned normally.</summary>
    public Exception? Error { get; }

    /// <summary>
    /// Runs <paramref name="work"/> and captures whatever it returns or throws. Never throws itself.
    /// </summary>
    /// <param name="work">The delegate to run.</param>
    /// <returns>The captured outcome.</returns>
    public static DispatchOutcome<T> Capture(Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        DispatchOutcome<T> outcome;
        try
        {
            T value = work();
            outcome = new DispatchOutcome<T>(() => value, null);
        }
        catch (Exception ex)
        {
            ExceptionDispatchInfo error = ExceptionDispatchInfo.Capture(ex);
            outcome = new DispatchOutcome<T>(() => Rethrow(error), ex);
        }
        return outcome;
    }

    /// <summary>
    /// Returns the captured value, or rethrows the captured exception with its original stack trace.
    /// </summary>
    /// <returns>The value the delegate returned.</returns>
    public T GetValueOrRethrow()
    {
        return mUnwrap();
    }

    private static T Rethrow(ExceptionDispatchInfo error)
    {
        error.Throw();
        // Throw() never returns; this second throw only satisfies definite-return analysis.
        throw error.SourceException;
    }
}
