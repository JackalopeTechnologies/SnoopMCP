// ResolveStyleResponse.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>resolveStyle</c> tool: the applied style's key and source, its full
/// <c>BasedOn</c> chain, every setter (across the chain), and a summary of its triggers.
/// </summary>
/// <param name="AppliedStyleKey">
/// The implicit style key (the target type's name) when determinable, otherwise <c>null</c>.
/// Recovering an explicit <c>x:Key</c> at runtime is out of scope for v1.
/// </param>
/// <param name="AppliedStyleSource">
/// <c>Explicit</c> when the style was set via the local <c>Style</c> property, <c>Implicit</c> when
/// matched by target type, or <c>null</c> when no style is applied.
/// </param>
/// <param name="BasedOnChain">The applied style and its ancestors via <c>BasedOn</c>, depth-ordered.</param>
/// <param name="Setters">Every setter across the <c>BasedOn</c> chain.</param>
/// <param name="Triggers">A best-effort summary of every trigger across the <c>BasedOn</c> chain.</param>
public sealed record ResolveStyleResponse(
    string? AppliedStyleKey,
    string? AppliedStyleSource,
    IReadOnlyList<BasedOnEntryDto> BasedOnChain,
    IReadOnlyList<StyleSetterDto> Setters,
    IReadOnlyList<TriggerSummaryDto> Triggers);
