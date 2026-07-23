// MainWindow.Wireframe.cs
// Routes the grouped main-toolbar viewport mode buttons into rendering-owned scene state.
using Pillar.Rendering.Scene;
using System.Windows;

namespace Pillar.UI;

public partial class MainWindow
{
    /// <summary>
    /// Selects one viewport rendering mode and keeps its three toolbar buttons mutually exclusive.
    /// </summary>
    private void RenderModeButton_Click(object sender, RoutedEventArgs e)
    {
        _ = e;

        ViewportRenderMode renderMode = sender == WireframeShadedRenderModeButton
            ? ViewportRenderMode.WireframeShaded
            : sender == WireframeRenderModeButton
                ? ViewportRenderMode.Wireframe
                : ViewportRenderMode.Shaded;

        ShadedRenderModeButton.IsChecked = renderMode == ViewportRenderMode.Shaded;
        WireframeShadedRenderModeButton.IsChecked = renderMode == ViewportRenderMode.WireframeShaded;
        WireframeRenderModeButton.IsChecked = renderMode == ViewportRenderMode.Wireframe;
        _scene.SetViewportRenderMode(renderMode);
    }
}