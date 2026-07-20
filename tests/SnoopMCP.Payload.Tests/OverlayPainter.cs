// OverlayPainter.cs
// Temporary diagnostic helper for issue #75 - not a permanent test type.

namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Media;

/// <summary>Draws far outside its own (zero) layout size, like a modal-overlay host.</summary>
internal sealed class OverlayPainter : FrameworkElement
{
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        drawingContext.DrawRectangle(Brushes.Blue, null, new Rect(0, 0, 800, 600));
    }
}
