// UiaDriver.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Automation;

using System.Windows;
using System.Windows.Automation;
using SnoopMCP.Protocol.Errors;
using Condition = System.Windows.Automation.Condition;

/// <summary>
/// UIA2 driver over <see cref="System.Windows.Automation"/>. Finds the target window by process id and
/// projects elements to <see cref="UiaElementInfo"/> (minting handles into the shared cache). Discovery
/// walks batch cross-process COM reads under an active <see cref="CacheRequest"/>, so a bounded tree
/// walk costs one round trip per element rather than one per property. All UIA calls run on the thread
/// pool under a per-call timeout so a hung target cannot wedge the server.
/// </summary>
public sealed class UiaDriver : IUiaDriver
{
    private const string LocatorByAutomationId = "automationId";
    private const string PatternInvoke = "Invoke";
    private const string PatternToggle = "Toggle";
    private const string PatternSelectionItem = "SelectionItem";
    private const string PatternExpandCollapse = "ExpandCollapse";
    private const string PatternValue = "Value";
    private const string MessageNoActionPattern =
        "Element exposes no Invoke/SelectionItem/Toggle/ExpandCollapse pattern.";
    private const int WaitPollMs = 200;

    private static readonly TimeSpan smCallTimeout = TimeSpan.FromSeconds(5);

    private readonly ElementHandleCache mCache;

    /// <summary>Creates a driver over the shared handle cache.</summary>
    public UiaDriver(ElementHandleCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        mCache = cache;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<UiaElementInfo>> GetTreeAsync(
        int pid, UiaElementRef? fromElement, int depth, CancellationToken ct)
    {
        if (depth < 1)
        {
            throw new SnoopMcpException(ErrorCode.InvalidArgument, "depth must be >= 1.");
        }
        if (fromElement is { } reference && reference.Pid != pid)
        {
            throw new SnoopMcpException(
                ErrorCode.InvalidArgument,
                $"fromElement.Pid ({reference.Pid}) does not match pid ({pid}).");
        }
        return RunUia<IReadOnlyList<UiaElementInfo>>(() => GetTreeCore(pid, fromElement, depth), ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<UiaElementInfo>> FindAsync(int pid, string by, string value, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(by);
        ArgumentNullException.ThrowIfNull(value);
        Condition condition = UiaLocator.ToCondition(by, value);
        return RunUia<IReadOnlyList<UiaElementInfo>>(() => FindCore(pid, by, value, condition), ct);
    }

    /// <inheritdoc />
    public async Task<UiaElementInfo> WaitForAsync(
        int pid, string by, string value, int timeoutMs, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(by);
        ArgumentNullException.ThrowIfNull(value);
        if (timeoutMs < 0)
        {
            throw new SnoopMcpException(ErrorCode.InvalidArgument, "timeoutMs must be >= 0.");
        }
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        UiaElementInfo? hit = null;
        while (hit is null)
        {
            IReadOnlyList<UiaElementInfo> hits = await FindAsync(pid, by, value, ct).ConfigureAwait(false);
            if (hits.Count > 0)
            {
                hit = hits[0];
            }
            if (hit is null && DateTimeOffset.UtcNow >= deadline)
            {
                throw new SnoopMcpException(
                    ErrorCode.TargetUnresponsive,
                    $"No element matched {by}='{value}' within {timeoutMs}ms.");
            }
            if (hit is null)
            {
                await Task.Delay(WaitPollMs, ct).ConfigureAwait(false);
            }
        }
        return hit;
    }

    /// <inheritdoc />
    public Task<AutomationElement> ResolveAsync(UiaElementRef reference, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return RunUia(() => ResolveCore(reference), ct);
    }

    /// <inheritdoc />
    public async Task InvokeAsync(UiaElementRef reference, string? pattern, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        AutomationElement element = await ResolveAsync(reference, ct).ConfigureAwait(false);
        await RunUia<object?>(() =>
        {
            Act(element, pattern);
            return null;
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetValueAsync(UiaElementRef reference, string value, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(value);
        AutomationElement element = await ResolveAsync(reference, ct).ConfigureAwait(false);
        await RunUia<object?>(() =>
        {
            if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out object raw))
            {
                throw new SnoopMcpException(ErrorCode.NotDrivable, "Element does not support ValuePattern.");
            }
            var valuePattern = (ValuePattern)raw;
            if (valuePattern.Current.IsReadOnly)
            {
                throw new SnoopMcpException(ErrorCode.ValueReadOnly, "Element value is read-only.");
            }
            valuePattern.SetValue(value);
            return null;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Drives <paramref name="element"/> via the named <paramref name="pattern"/>, or — when null —
    /// auto-selects the first available pattern in priority order (Invoke, SelectionItem, Toggle,
    /// ExpandCollapse). Throws <see cref="ErrorCode.NotDrivable"/> when no pattern applies.
    /// </summary>
    private static void Act(AutomationElement element, string? pattern)
    {
        bool tryInvoke = pattern is null || string.Equals(pattern, PatternInvoke, StringComparison.OrdinalIgnoreCase);
        bool trySelectionItem =
            pattern is null || string.Equals(pattern, PatternSelectionItem, StringComparison.OrdinalIgnoreCase);
        bool tryToggle = pattern is null || string.Equals(pattern, PatternToggle, StringComparison.OrdinalIgnoreCase);
        bool tryExpandCollapse =
            pattern is null || string.Equals(pattern, PatternExpandCollapse, StringComparison.OrdinalIgnoreCase);

        bool acted = false;
        if (tryInvoke && element.TryGetCurrentPattern(InvokePattern.Pattern, out object invoke))
        {
            ((InvokePattern)invoke).Invoke();
            acted = true;
        }
        if (!acted && trySelectionItem && element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object select))
        {
            ((SelectionItemPattern)select).Select();
            acted = true;
        }
        if (!acted && tryToggle && element.TryGetCurrentPattern(TogglePattern.Pattern, out object toggle))
        {
            ((TogglePattern)toggle).Toggle();
            acted = true;
        }
        if (!acted && tryExpandCollapse
            && element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out object expand))
        {
            ((ExpandCollapsePattern)expand).Expand();
            acted = true;
        }
        if (!acted)
        {
            throw new SnoopMcpException(
                ErrorCode.NotDrivable,
                pattern is null
                    ? MessageNoActionPattern
                    : $"Element does not support the '{pattern}' pattern.");
        }
    }

    /// <summary>The <see cref="GetTreeAsync"/> work body, run under <see cref="RunUia{T}"/> off the caller's thread.</summary>
    private List<UiaElementInfo> GetTreeCore(int pid, UiaElementRef? fromElement, int depth)
    {
        var results = new List<UiaElementInfo>();
        using (BuildDiscoveryCacheRequest().Activate())
        {
            AutomationElement root = ResolveRoot(pid, fromElement);
            Walk(pid, root, depth, results);
        }
        return results;
    }

    /// <summary>The <see cref="FindAsync"/> work body, run under <see cref="RunUia{T}"/> off the caller's thread.</summary>
    private List<UiaElementInfo> FindCore(int pid, string by, string value, Condition condition)
    {
        var results = new List<UiaElementInfo>();
        using (BuildDiscoveryCacheRequest().Activate())
        {
            AutomationElement window = FindWindow(pid);
            AutomationElementCollection found = window.FindAll(TreeScope.Descendants | TreeScope.Element, condition);
            foreach (AutomationElement element in found)
            {
                results.Add(Project(pid, element, by, value));
            }
        }
        return results;
    }

    /// <summary>
    /// Resolves <paramref name="fromElement"/> when supplied (handle → locator → <see cref="ErrorCode.UiaElementStale"/>,
    /// never re-rooting to the window); otherwise resolves the target window itself.
    /// </summary>
    private AutomationElement ResolveRoot(int pid, UiaElementRef? fromElement)
    {
        AutomationElement root;
        if (fromElement is { } reference)
        {
            root = ResolveCore(reference);
        }
        else
        {
            root = FindWindow(pid);
        }
        return root;
    }

    /// <summary>Shared handle→locator resolution used by both <see cref="ResolveAsync"/> and <see cref="ResolveRoot"/>.</summary>
    private AutomationElement ResolveCore(UiaElementRef reference)
    {
        AutomationElement? resolved = null;
        if (mCache.TryGet(reference.Handle, reference.Pid, out AutomationElement? cached, out _, out _))
        {
            resolved = cached;
        }
        if (resolved is null && reference.By is { } by && reference.Value is { } value)
        {
            AutomationElement window = FindWindow(reference.Pid);
            AutomationElementCollection matches = window.FindAll(
                TreeScope.Descendants | TreeScope.Element, UiaLocator.ToCondition(by, value));
            if (matches.Count > 1)
            {
                throw new SnoopMcpException(
                    ErrorCode.UiaAmbiguousLocator,
                    $"Locator {by}='{value}' matched {matches.Count} elements.");
            }
            if (matches.Count == 1)
            {
                resolved = matches[0];
            }
        }
        return resolved
            ?? throw new SnoopMcpException(
                ErrorCode.UiaElementStale, "Element handle expired and could not be re-resolved.");
    }

    private static AutomationElement FindWindow(int pid)
    {
        AutomationElement? window = AutomationElement.RootElement.FindFirst(
            TreeScope.Children,
            new PropertyCondition(AutomationElement.ProcessIdProperty, pid));
        return window
            ?? throw new SnoopMcpException(ErrorCode.AttachFailed, $"No top-level window for process {pid}.");
    }

    private void Walk(int pid, AutomationElement element, int remainingDepth, List<UiaElementInfo> sink)
    {
        AutomationElementCollection children = element.FindAll(TreeScope.Children, Condition.TrueCondition);
        foreach (AutomationElement child in children)
        {
            sink.Add(Project(pid, child, by: null, value: null));
            if (remainingDepth > 1)
            {
                Walk(pid, child, remainingDepth - 1, sink);
            }
        }
    }

    /// <summary>
    /// Projects a discovered element to <see cref="UiaElementInfo"/> and mints a fresh cache handle.
    /// The minted <see cref="UiaElementRef"/> carries a durable locator: the element's own AutomationId
    /// when it has one, otherwise the <paramref name="by"/>/<paramref name="value"/> the find was
    /// performed with (which may be null for plain tree-walk nodes). Reads go through the cached
    /// property surface — an active <see cref="CacheRequest"/> must already cover every property and
    /// pattern-availability flag read here.
    /// </summary>
    private UiaElementInfo Project(int pid, AutomationElement element, string? by, string? value)
    {
        string automationId = element.Cached.AutomationId ?? string.Empty;
        string? effectiveBy = by;
        string? effectiveValue = value;
        if (!string.IsNullOrEmpty(automationId))
        {
            effectiveBy = LocatorByAutomationId;
            effectiveValue = automationId;
        }
        string handle = mCache.Add(pid, element, effectiveBy, effectiveValue);
        var reference = new UiaElementRef(pid, handle, effectiveBy, effectiveValue);
        // Rect.Empty (±Infinity) for an offscreen element; Create maps that to null bounds so one
        // offscreen match cannot abort serialization of the whole response. See issue #73.
        Rect bounds = element.Cached.BoundingRectangle;
        return UiaElementInfo.Create(
            reference,
            automationId,
            element.Cached.Name ?? string.Empty,
            element.Cached.ControlType?.ProgrammaticName ?? string.Empty,
            element.Cached.HelpText ?? string.Empty,
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            BuildPatterns(element));
    }

    /// <summary>
    /// Reads the cached action/value pattern-availability flags and maps each true flag to its
    /// agent-facing short name. Reads via <see cref="AutomationElement.GetCachedPropertyValue(AutomationProperty)"/>
    /// rather than <see cref="AutomationElement.GetSupportedPatterns"/> so discovery never makes an
    /// un-cached COM call.
    /// </summary>
    private static List<string> BuildPatterns(AutomationElement element)
    {
        var patterns = new List<string>();
        if ((bool)element.GetCachedPropertyValue(AutomationElement.IsInvokePatternAvailableProperty))
        {
            patterns.Add(PatternInvoke);
        }
        if ((bool)element.GetCachedPropertyValue(AutomationElement.IsTogglePatternAvailableProperty))
        {
            patterns.Add(PatternToggle);
        }
        if ((bool)element.GetCachedPropertyValue(AutomationElement.IsSelectionItemPatternAvailableProperty))
        {
            patterns.Add(PatternSelectionItem);
        }
        if ((bool)element.GetCachedPropertyValue(AutomationElement.IsExpandCollapsePatternAvailableProperty))
        {
            patterns.Add(PatternExpandCollapse);
        }
        if ((bool)element.GetCachedPropertyValue(AutomationElement.IsValuePatternAvailableProperty))
        {
            patterns.Add(PatternValue);
        }
        return patterns;
    }

    /// <summary>The property/pattern-availability set discovery caches in one cross-process batch (C10).</summary>
    private static CacheRequest BuildDiscoveryCacheRequest()
    {
        var request = new CacheRequest();
        request.Add(AutomationElement.AutomationIdProperty);
        request.Add(AutomationElement.NameProperty);
        request.Add(AutomationElement.ControlTypeProperty);
        request.Add(AutomationElement.HelpTextProperty);
        request.Add(AutomationElement.BoundingRectangleProperty);
        request.Add(AutomationElement.IsInvokePatternAvailableProperty);
        request.Add(AutomationElement.IsTogglePatternAvailableProperty);
        request.Add(AutomationElement.IsSelectionItemPatternAvailableProperty);
        request.Add(AutomationElement.IsExpandCollapsePatternAvailableProperty);
        request.Add(AutomationElement.IsValuePatternAvailableProperty);
        return request;
    }

    private static async Task<T> RunUia<T>(Func<T> work, CancellationToken ct)
    {
        T result;
        try
        {
            result = await Task.Run(work, ct).WaitAsync(smCallTimeout, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new SnoopMcpException(ErrorCode.TargetUnresponsive, "UI Automation call timed out.");
        }
        catch (ElementNotAvailableException ex)
        {
            // The element vanished between find and use (window closed, tree changed).
            throw new SnoopMcpException(ErrorCode.UiaElementStale, "UIA element is no longer available.", ex);
        }
        catch (ElementNotEnabledException ex)
        {
            throw new SnoopMcpException(ErrorCode.NotDrivable, "UIA element is not enabled.", ex);
        }
        return result;
    }
}
