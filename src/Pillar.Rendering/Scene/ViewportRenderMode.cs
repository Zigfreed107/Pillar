// ViewportRenderMode.cs
// Defines the transient mesh presentation modes supported by the main Helix viewport.
namespace Pillar.Rendering.Scene;

/// <summary>
/// Identifies how document meshes are rasterized in the viewport.
/// </summary>
public enum ViewportRenderMode
{
    Shaded,
    WireframeShaded,
    Wireframe
}