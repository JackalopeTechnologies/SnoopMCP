// StyleResolver.cs
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

namespace SnoopMCP.Payload.Inspection;

using System.Globalization;
using System.Windows;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Resolves the <see cref="Style"/> applied to an element into a wire response: the applied style's
/// key and source, its full <c>BasedOn</c> chain, every setter across that chain, and a best-effort
/// summary of every trigger. The LLM uses this to understand why a property has the value it has when
/// "the style won."
/// </summary>
public sealed class StyleResolver
{
    private const string ExplicitSource = "Explicit";
    private const string ImplicitSource = "Implicit";
    private const string UnknownTarget = "?";

    /// <summary>
    /// Resolves the style applied to <paramref name="element"/>. When no style is applied, the chain,
    /// setters, and triggers are empty and both key and source are <c>null</c>.
    /// </summary>
    /// <param name="element">The element to inspect.</param>
    /// <returns>The applied style's key, source, chain, setters, and trigger summary.</returns>
    /// <remarks>
    /// CA1822 disabled: scaffolded for DI in handler; will gain instance state in follow-up phases.
    /// </remarks>
#pragma warning disable CA1822
    public ResolveStyleResponse Resolve(DependencyObject element)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(element);

        Style? style = (element as FrameworkElement)?.Style;
        ResolveStyleResponse response;
        if (style is null)
        {
            response = new ResolveStyleResponse(
                AppliedStyleKey: null,
                AppliedStyleSource: null,
                BasedOnChain: Array.Empty<BasedOnEntryDto>(),
                Setters: Array.Empty<StyleSetterDto>(),
                Triggers: Array.Empty<TriggerSummaryDto>());
        }
        else
        {
            response = BuildResponse(element, style);
        }

        return response;
    }

    private static ResolveStyleResponse BuildResponse(DependencyObject element, Style style)
    {
        string appliedKey = style.TargetType?.Name ?? string.Empty;
        string appliedSource = ClassifySource(element);
        List<BasedOnEntryDto> chain = BuildChain(style);
        List<StyleSetterDto> setters = CollectSetters(style);
        List<TriggerSummaryDto> triggers = CollectTriggers(style);

        return new ResolveStyleResponse(
            AppliedStyleKey: string.IsNullOrEmpty(appliedKey) ? null : appliedKey,
            AppliedStyleSource: appliedSource,
            BasedOnChain: chain,
            Setters: setters,
            Triggers: triggers);
    }

    private static string ClassifySource(DependencyObject element)
    {
        object localStyle = element.ReadLocalValue(FrameworkElement.StyleProperty);
        bool isExplicit = localStyle != DependencyProperty.UnsetValue;
        return isExplicit ? ExplicitSource : ImplicitSource;
    }

    private static List<BasedOnEntryDto> BuildChain(Style style)
    {
        var chain = new List<BasedOnEntryDto>();
        Style? current = style;
        int depth = 0;
        while (current is not null)
        {
            string target = current.TargetType?.FullName ?? current.TargetType?.Name ?? UnknownTarget;
            chain.Add(new BasedOnEntryDto(target, depth));
            current = current.BasedOn;
            depth++;
        }

        return chain;
    }

    private static List<StyleSetterDto> CollectSetters(Style style)
    {
        var setters = new List<StyleSetterDto>();
        Style? current = style;
        while (current is not null)
        {
            AppendSetters(current.Setters, setters);
            current = current.BasedOn;
        }

        return setters;
    }

    private static List<TriggerSummaryDto> CollectTriggers(Style style)
    {
        var triggers = new List<TriggerSummaryDto>();
        Style? current = style;
        while (current is not null)
        {
            foreach (TriggerBase t in current.Triggers)
            {
                triggers.Add(SummarizeTrigger(t));
            }

            current = current.BasedOn;
        }

        return triggers;
    }

    private static TriggerSummaryDto SummarizeTrigger(TriggerBase trigger)
    {
        string kind = trigger.GetType().Name;
        string condition = trigger switch
        {
            Trigger t => FormatPropertyCondition(t),
            _ => kind
        };

        var setters = new List<StyleSetterDto>();
        if (trigger is Trigger tt)
        {
            AppendSetters(tt.Setters, setters);
        }

        return new TriggerSummaryDto(kind, condition, setters);
    }

    private static string FormatPropertyCondition(Trigger trigger)
    {
        string property = trigger.Property?.Name ?? UnknownTarget;
        string value = Convert.ToString(trigger.Value, CultureInfo.InvariantCulture) ?? string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"{property}={value}");
    }

    private static void AppendSetters(SetterBaseCollection source, List<StyleSetterDto> sink)
    {
        IEnumerable<Setter> typed = source.OfType<Setter>();
        foreach (Setter s in typed)
        {
            sink.Add(new StyleSetterDto(
                Property: s.Property.Name,
                Value: Convert.ToString(s.Value, CultureInfo.InvariantCulture)));
        }
    }
}
