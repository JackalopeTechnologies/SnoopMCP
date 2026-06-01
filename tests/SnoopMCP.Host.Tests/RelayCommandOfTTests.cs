// RelayCommandOfTTests.cs
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

#region Usings

using SnoopMCP.ClientIntegration;
using Xunit;

#endregion

namespace SnoopMCP.Host.Tests;

/// <summary>
///     Tests for <see cref="RelayCommand{T}" />, which backs the per-agent tray install/uninstall menu
///     items: a specific menu item passes its <see cref="McpClient" />; the "All" item passes null.
/// </summary>
public sealed class RelayCommandOfTTests
{
    [Fact]
    public void Execute_WithEnumParameter_PassesValue()
    {
        McpClient? captured = null;
        var command = new RelayCommand<McpClient>(c => captured = c);

        command.Execute(McpClient.Cursor);

        Assert.Equal(McpClient.Cursor, captured);
    }

    [Fact]
    public void Execute_WithNullParameter_PassesNull()
    {
        var invoked = false;
        McpClient? captured = McpClient.ClaudeCode;
        var command = new RelayCommand<McpClient>(c =>
        {
            invoked = true;
            captured = c;
        });

        command.Execute(null);

        Assert.True(invoked);
        Assert.Null(captured);
    }

    [Fact]
    public void CanExecute_IsAlwaysTrue()
    {
        var command = new RelayCommand<McpClient>(_ => { });

        Assert.True(command.CanExecute(null));
        Assert.True(command.CanExecute(McpClient.VsCode));
    }
}
