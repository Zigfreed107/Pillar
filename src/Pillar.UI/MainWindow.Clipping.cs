// MainWindow.Clipping.cs
// Wires the viewport Z clipping overlay to render-layer clipping planes while keeping drag UI logic out of rendering code.
using Pillar.UI.Controls;

namespace Pillar.UI;

public partial class MainWindow
{
    /// <summary>
    /// Initializes the Z clipping overlay from the printable volume configured for the viewport grid.
    /// </summary>
    private void InitializeModelClippingControls()
    {
        ClipRangeSliderOverlay.ClipRangeChanged += ClipRangeSliderOverlay_ClipRangeChanged;
        ClipRangeSliderOverlay.ConfigureRange(
            0.0,
            _printableVolumeDefinition.ZDistance,
            0.0,
            _printableVolumeDefinition.ZDistance);
    }

    /// <summary>
    /// Applies the selected visible Z range to all renderable mesh visuals.
    /// </summary>
    private void ClipRangeSliderOverlay_ClipRangeChanged(object? sender, ClipRangeChangedEventArgs e)
    {
        _ = sender;
        _scene.ConfigureModelClipRange((float)e.LowerZ, (float)e.UpperZ);
        _viewModel.SetStatusText($"Visible Z range: {e.LowerZ:F1} mm to {e.UpperZ:F1} mm");
    }
}
