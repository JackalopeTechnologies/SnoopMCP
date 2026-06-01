// DataContextInfo.cs
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
///     CLR type-shape snapshot of an element's DataContext: type name, namespace,
///     base-type chain, implemented interfaces, and declared CLR properties.
/// </summary>
/// <param name="TypeName">The simple type name of the DataContext.</param>
/// <param name="Namespace">The namespace of the DataContext type, or <see cref="string.Empty" /> when global.</param>
/// <param name="BaseTypes">Full names of every base type from the immediate parent up to <see cref="object" />.</param>
/// <param name="Interfaces">Full names of every interface implemented by the type.</param>
/// <param name="DeclaredProperties">Declared-only public instance CLR properties on the type.</param>
public sealed record DataContextInfo(
    string TypeName,
    string Namespace,
    IReadOnlyList<string> BaseTypes,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<DeclaredPropertyDto> DeclaredProperties);
