// TagPreviewRenderer.cs
// Reuses one render object for transparent moving and opaque locked raft-tag previews.
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Layers;
using Pillar.Geometry.Tags;
using Pillar.Rendering.EntityRenderers;
using System;
using System.Windows;

namespace Pillar.Rendering.Preview;

/// <summary>
/// Owns the disposable tag preview visual beneath SceneManager's preview root.
/// </summary>
public sealed class TagPreviewRenderer
{
    private readonly MeshGeometryModel3D _model;

    /// <summary>
    /// Creates and attaches one reusable tag preview model.
    /// </summary>
    public TagPreviewRenderer(GroupModel3D previewRoot)
    {
        if (previewRoot == null)
        {
            throw new ArgumentNullException(nameof(previewRoot));
        }

        _model = new MeshGeometryModel3D
        {
            IsHitTestVisible = false,
            IsTransparent = true,
            Visibility = Visibility.Collapsed
        };
        previewRoot.Children.Add(_model);
    }

    /// <summary>
    /// Replaces only geometry and material while retaining the scene object.
    /// </summary>
    public void Show(TagMeshData mesh, SupportLayerColor color, float opacity)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        _model.Geometry = TagRenderer.CreateFlatShadedGeometry(mesh.Positions, mesh.TriangleIndices);
        _model.Material = TagRenderer.CreateMaterial(color, opacity);
        _model.IsTransparent = opacity < 1.0f;
        _model.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Hides the transient model without changing durable document state.
    /// </summary>
    public void Hide()
    {
        _model.Visibility = Visibility.Collapsed;
        _model.Geometry = null;
    }
}
