// PathStep.cs
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

namespace SnoopMCP.Payload.PathStrings;

/// <summary>
/// A single segment in a canonical path string of the form
/// <c>/TypeName[Name='X', AutomationId='Y'][n]/...</c>.
/// </summary>
/// <param name="TypeName">The CLR type's short name (no namespace).</param>
/// <param name="Attributes">Zero or more attribute predicates (e.g. <c>Name</c>, <c>AutomationId</c>).</param>
/// <param name="Index">Optional 0-based index disambiguating same-typed siblings.</param>
public sealed record PathStep(
    string TypeName,
    IReadOnlyDictionary<string, string> Attributes,
    int? Index);
