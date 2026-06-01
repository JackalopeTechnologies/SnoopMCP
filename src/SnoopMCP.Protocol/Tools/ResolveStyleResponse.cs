// ResolveStyleResponse.cs
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
