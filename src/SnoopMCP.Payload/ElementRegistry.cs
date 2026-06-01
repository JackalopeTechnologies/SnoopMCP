// ElementRegistry.cs
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

namespace SnoopMCP.Payload;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Windows;

/// <summary>
/// Maps <see cref="DependencyObject"/> instances to stable integer ids and back.
/// </summary>
/// <remarks>
/// Forward lookup (id → element) goes through <see cref="WeakReference{T}"/> so the registry
/// never extends element lifetime; expired ids report <c>false</c> from <see cref="TryResolve"/>.
/// Reverse lookup (element → id) uses <see cref="ConditionalWeakTable{TKey, TValue}"/> so element
/// ↔ id holders aren't pinned either way.
/// </remarks>
public sealed class ElementRegistry
{
    private readonly ConcurrentDictionary<int, WeakReference<DependencyObject>> mById = new();
    private readonly ConditionalWeakTable<DependencyObject, IdHolder> mByElement = new();
    private int mNextId;

    /// <summary>
    /// Returns the existing id for <paramref name="element"/>, or assigns and returns a new one.
    /// </summary>
    /// <param name="element">The element to look up or register.</param>
    /// <returns>A positive integer id stable for the lifetime of <paramref name="element"/>.</returns>
    public int GetOrAssign(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        IdHolder holder = mByElement.GetValue(element, AllocateId);
        return holder.Id;
    }

    /// <summary>
    /// Tries to resolve a previously-assigned id to its live <see cref="DependencyObject"/>.
    /// </summary>
    /// <param name="id">The id returned from a prior <see cref="GetOrAssign"/> call.</param>
    /// <param name="element">When the method returns <c>true</c>, the live element; otherwise <c>null</c>.</param>
    /// <returns>
    /// <c>true</c> if the id is known and the element is still alive; <c>false</c> when the id is
    /// unknown or the element has been garbage-collected.
    /// </returns>
    // STR0010 suppression: the analyzer treats `out` parameters identically to `in` parameters,
    // but validating an `out` is incoherent — it carries no caller input. There is no alternative
    // expression of TryResolve that preserves the standard .NET `Try*` semantics.
#pragma warning disable STR0010
    public bool TryResolve(int id, [MaybeNullWhen(false)] out DependencyObject element)
#pragma warning restore STR0010
    {
        DependencyObject? resolved = null;
        bool found = false;
        bool hasEntry = mById.TryGetValue(id, out WeakReference<DependencyObject>? weakRef);
        if (hasEntry)
        {
            bool hasLiveTarget = weakRef is not null && weakRef.TryGetTarget(out resolved);
            if (hasLiveTarget)
            {
                found = true;
            }
            else
            {
                mById.TryRemove(id, out _);
            }
        }
        element = resolved;
        return found;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="id"/> resolves to a live element.
    /// </summary>
    /// <param name="id">The id to check.</param>
    /// <returns><c>true</c> if the id is known and the element is still alive.</returns>
    public bool IsAlive(int id)
    {
        bool alive = TryResolve(id, out _);
        return alive;
    }

    private IdHolder AllocateId(DependencyObject element)
    {
        int newId = Interlocked.Increment(ref mNextId);
        mById[newId] = new WeakReference<DependencyObject>(element);
        return new IdHolder(newId);
    }

    private sealed class IdHolder
    {
        public IdHolder(int id)
        {
            Id = id;
        }

        public int Id { get; }
    }
}
