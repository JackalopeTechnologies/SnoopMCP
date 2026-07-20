// ValueWaiter.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Interaction;

using System.ComponentModel;
using System.Windows;
using Protocol.Errors;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Polls a dependency property or a dotted DataContext path until it matches an expected value or
/// the timeout elapses. This is ground-truth verification — it reads view-model / DP reality, not
/// pixels.
/// </summary>
/// <remarks>
/// The poll loop is asynchronous: each read is marshalled onto the UI thread via
/// <see cref="DispatcherMarshal.Invoke{T}"/>, and the wait between polls is a non-blocking
/// <see cref="Task.Delay(int, CancellationToken)"/> rather than <see cref="Thread.Sleep(int)"/>. A
/// blocking sleep would stall whatever thread is driving the loop; when that thread also owns the
/// dispatcher being polled (as in a single-threaded test host), it would deadlock the wait against
/// itself. The async form yields instead, letting the dispatcher keep pumping between reads.
/// </remarks>
public sealed class ValueWaiter
{
    private const int PollIntervalMs = 100;

    /// <summary>
    /// Waits for the target value. Reads are marshalled onto the UI thread each poll; the wait
    /// between polls is a non-blocking delay.
    /// </summary>
    /// <param name="element">The element whose DP, or whose DataContext, is polled.</param>
    /// <param name="dependencyProperty">
    /// The DP registered name to poll. Exactly one of this and <paramref name="dataContextPath"/> must be set.
    /// </param>
    /// <param name="dataContextPath">
    /// The dotted DataContext path to poll. Exactly one of this and <paramref name="dependencyProperty"/> must be set.
    /// </param>
    /// <param name="expected">The expected value, compared via ordinal string equality.</param>
    /// <param name="timeoutMs">Maximum time to poll, in milliseconds.</param>
    /// <param name="marshal">Dispatcher marshal used to read the value on the UI thread each poll.</param>
    /// <param name="cancellationToken">A token observed at the start of each poll and during the inter-poll delay.</param>
    /// <returns>Whether the value matched before the timeout, and the last observed value.</returns>
    /// <remarks>
    /// CA1822 disabled: instance method by design so callers (e.g. <c>WaitForValueToolHandler</c>)
    /// hold and inject a <see cref="ValueWaiter"/> like the other driving-layer collaborators,
    /// consistent with <c>CommandInvoker</c>/<c>AutomationPeerDriver</c>. No instance state today;
    /// may gain some in a follow-up phase without an API-shape change.
    /// </remarks>
#pragma warning disable CA1822
    public async Task<WaitForValueResponse> WaitAsync(
        DependencyObject element,
        string? dependencyProperty,
        string? dataContextPath,
        string expected,
        int timeoutMs,
        DispatcherMarshal marshal,
        CancellationToken cancellationToken)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(marshal);
        ArgumentNullException.ThrowIfNull(expected);
        bool oneSource = string.IsNullOrEmpty(dependencyProperty) ^ string.IsNullOrEmpty(dataContextPath);
        if (!oneSource)
        {
            throw new SnoopMcpException(ErrorCode.InvalidArgument, "Set exactly one of dependencyProperty or dataContextPath.");
        }

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        string? actual = null;
        bool matched = false;
        while (!matched && DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            actual = marshal.Invoke(() => ReadValue(element, dependencyProperty, dataContextPath), cancellationToken);
            matched = string.Equals(actual, expected, StringComparison.Ordinal);
            if (!matched)
            {
                await Task.Delay(PollIntervalMs, cancellationToken).ConfigureAwait(false);
            }
        }
        return new WaitForValueResponse(matched, actual);
    }

    private static string? ReadValue(DependencyObject element, string? dp, string? path)
    {
        object? value;
        if (!string.IsNullOrEmpty(dp))
        {
            DependencyProperty property = FindDependencyProperty(element, dp)
                ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, $"DP '{dp}' not found on {element.GetType().Name}.");
            value = element.GetValue(property);
        }
        else
        {
            object? context = (element as FrameworkElement)?.DataContext;
            value = context is not null && path is not null && DataContextPath.TryWalk(context, path, out object? resolved)
                ? resolved
                : null;
        }
        return value?.ToString();
    }

    private static DependencyProperty? FindDependencyProperty(DependencyObject element, string name)
    {
        PropertyDescriptor? descriptor = TypeDescriptor.GetProperties(element)[name];
        DependencyPropertyDescriptor? dpd = descriptor is null ? null : DependencyPropertyDescriptor.FromProperty(descriptor);
        return dpd?.DependencyProperty;
    }
}
