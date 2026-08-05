// RaftTextPreviewRenderer.cs
// Reuses one render object for transparent moving and opaque locked raft text previews.
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Layers;
using Pillar.Geometry.RaftTexts;
using Pillar.Rendering.EntityRenderers;
using System;
using System.Windows;
using System.Windows.Media.Media3D;

namespace Pillar.Rendering.Preview;

/// <summary>
/// Owns the disposable raft text preview beneath SceneManager's preview root.
/// </summary>
public sealed class RaftTextPreviewRenderer
{
    private readonly MeshGeometryModel3D _model;
    private readonly TranslateTransform3D _translation;

    /// <summary>
    /// Creates and attaches one reusable raft text preview model.
    /// </summary>
    public RaftTextPreviewRenderer(GroupModel3D previewRoot)
    {
        if (previewRoot == null)
        {
            throw new ArgumentNullException(nameof(previewRoot));
        }

        _translation = new TranslateTransform3D();
        _model = new MeshGeometryModel3D
        {
            IsHitTestVisible = false,
            IsTransparent = true,
            Transform = _translation,
            Visibility = Visibility.Collapsed
        };
        previewRoot.Children.Add(_model);
    }

    /// <summary>
    /// Prepares local geometry once before pointer movement begins.
    /// </summary>
    public void PrepareMoving(RaftTextMeshData localMesh, SupportLayerColor color, float opacity)
    {
        SetMeshAndMaterial(localMesh, color, opacity);
        SetTranslation(System.Numerics.Vector3.Zero);
        _model.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Moves prepared local geometry without rebuilding buffers or allocating scene objects.
    /// </summary>
    public void MovePrepared(System.Numerics.Vector3 placement)
    {
        SetTranslation(placement);
        _model.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Replaces geometry and material while retaining the scene object.
    /// </summary>
    public void Show(RaftTextMeshData mesh, SupportLayerColor color, float opacity)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        SetMeshAndMaterial(mesh, color, opacity);
        SetTranslation(System.Numerics.Vector3.Zero);
        _model.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Hides prepared moving geometry while retaining buffers for the next valid pointer position.
    /// </summary>
    public void HidePrepared()
    {
        _model.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Hides the transient model without changing durable document state.
    /// </summary>
    public void Hide()
    {
        _model.Visibility = Visibility.Collapsed;
        _model.Geometry = null;
    }

    /// <summary>
    /// Replaces geometry and material outside the pointer-move hot path.
    /// </summary>
    private void SetMeshAndMaterial(RaftTextMeshData mesh, SupportLayerColor color, float opacity)
    {
        _model.Geometry = RaftTextRenderer.CreateFlatShadedGeometry(mesh.Positions, mesh.TriangleIndices);
        _model.Material = RaftTextRenderer.CreateMaterial(color, opacity);
        _model.IsTransparent = opacity < 1.0f;
    }

    /// <summary>
    /// Updates the reusable translation transform in place.
    /// </summary>
    private void SetTranslation(System.Numerics.Vector3 placement)
    {
        _translation.OffsetX = placement.X;
        _translation.OffsetY = placement.Y;
        _translation.OffsetZ = placement.Z;
    }
}
