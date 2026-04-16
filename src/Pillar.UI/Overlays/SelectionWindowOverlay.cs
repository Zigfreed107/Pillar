// SelectionWindowOverlay.cs
// Draws the 2D screen-space selection rectangle for the CAD viewport while selection rules remain in SelectTool.
using Pillar.Rendering.Tools;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Pillar.UI.Overlays;

/// <summary>
/// Updates the WPF rectangle used to display transient window-selection feedback over the viewport.
/// </summary>
public sealed class SelectionWindowOverlay
{
    private const string SolidDashArrayResourceKey = "SelectionWindowSolidDashArray";
    private const string DottedDashArrayResourceKey = "SelectionWindowDottedDashArray";

    private readonly Rectangle _rectangle;
    private readonly DoubleCollection _solidDashArray;
    private readonly DoubleCollection _dottedDashArray;

    /// <summary>
    /// Creates an overlay controller for an existing XAML rectangle.
    /// </summary>
    public SelectionWindowOverlay(FrameworkElement resourceOwner, Rectangle rectangle)
    {
        _rectangle = rectangle;
        _solidDashArray = FindDashArrayResource(resourceOwner, SolidDashArrayResourceKey, new DoubleCollection());
        _dottedDashArray = FindDashArrayResource(resourceOwner, DottedDashArrayResourceKey, new DoubleCollection { 8.0, 4.0 });
    }

    /// <summary>
    /// Draws or hides the screen-space selection rectangle.
    /// </summary>
    public void Update(SelectionWindowOverlayState state)
    {
        if (!state.IsVisible)
        {
            Hide();
            return;
        }

        Canvas.SetLeft(_rectangle, state.Left);
        Canvas.SetTop(_rectangle, state.Top);
        _rectangle.Width = state.Width;
        _rectangle.Height = state.Height;
        _rectangle.StrokeDashArray = state.SelectsCrossingEntities
            ? _solidDashArray
            : _dottedDashArray;
        _rectangle.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Hides the selection rectangle without changing selection state.
    /// </summary>
    public void Hide()
    {
        _rectangle.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Reads a dash pattern from XAML resources and falls back to a local pattern if the resource is missing.
    /// </summary>
    private static DoubleCollection FindDashArrayResource(
        FrameworkElement resourceOwner,
        string resourceKey,
        DoubleCollection fallback)
    {
        if (resourceOwner.TryFindResource(resourceKey) is DoubleCollection dashArray)
        {
            return dashArray;
        }

        return fallback;
    }
}
