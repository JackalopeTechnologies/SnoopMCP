// TrayIconRenderer.cs
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

namespace SnoopMCP.Host;

using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>
/// Supplies the tray icon for a given <see cref="ServerState"/>: the SnoopMCP logo with a colored
/// status dot in the corner (green = running, orange = starting, gray = stopped, red = faulted),
/// pre-rendered as four <c>.ico</c> assets shipped as WPF resources. The sources are loaded from
/// <c>pack://</c> URIs and frozen, so the same image can be handed to the UI thread from a background
/// state-change notification without a cross-thread violation.
/// </summary>
internal static class TrayIconRenderer
{
    private const string PackUriPrefix = "pack://application:,,,/Images/tray-";
    private const string PackUriSuffix = ".ico";
    private const string RunningName = "running";
    private const string StartingName = "starting";
    private const string StoppedName = "stopped";
    private const string FaultedName = "faulted";

    private static readonly Dictionary<ServerState, ImageSource> smCache = [];
    private static readonly Lock smLock = new();

    /// <summary>Returns the (cached, frozen) tray icon image for the given server state.</summary>
    public static ImageSource ForState(ServerState state)
    {
        ImageSource res;
        lock (smLock)
        {
            if (!smCache.TryGetValue(state, out ImageSource? cached))
            {
                cached = LoadIcon(StateName(state));
                smCache[state] = cached;
            }
            res = cached;
        }
        return res;
    }

    private static BitmapImage LoadIcon(string name)
    {
        var uri = new Uri(PackUriPrefix + name + PackUriSuffix, UriKind.Absolute);
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = uri;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static string StateName(ServerState state) => state switch
    {
        ServerState.Running => RunningName,
        ServerState.Starting => StartingName,
        ServerState.Faulted => FaultedName,
        _ => StoppedName
    };
}
