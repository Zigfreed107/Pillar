// IslandDetectionPreviewRenderer.cs
// Draws reusable non-selectable birth markers and selected birth-triangle overlays for island analysis results.
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Entities;
using Pillar.Geometry.Analysis;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;

namespace Pillar.Rendering.Preview;

/// <summary>
/// Presents transient island results without recomputing topology or participating in hit testing.
/// </summary>
public sealed class IslandDetectionPreviewRenderer
{
    private const float MinimumMarkerRadius = 0.05f;
    private readonly MeshGeometryModel3D _inactiveMarkerModel;
    private readonly MeshGeometryModel3D _activeMarkerModel;
    private readonly MeshGeometryModel3D _birthHighlightModel;

    /// <summary>
    /// Creates three reusable scene models for dim markers, the active marker, and its birth faces.
    /// </summary>
    public IslandDetectionPreviewRenderer(GroupModel3D sceneRoot)
    {
        if (sceneRoot == null)
        {
            throw new ArgumentNullException(nameof(sceneRoot));
        }

        _inactiveMarkerModel = CreateModel(new Color4(1.0f, 0.45f, 0.05f, 0.55f));
        _activeMarkerModel = CreateModel(new Color4(1.0f, 0.9f, 0.05f, 1.0f));
        _birthHighlightModel = CreateModel(new Color4(1.0f, 0.15f, 0.05f, 0.55f));
        sceneRoot.Children.Add(_inactiveMarkerModel);
        sceneRoot.Children.Add(_activeMarkerModel);
        sceneRoot.Children.Add(_birthHighlightModel);
    }

    /// <summary>
    /// Rebuilds marker buffers only when results, filtering, or active navigation changes.
    /// </summary>
    public void Show(
        MeshEntity mesh,
        IReadOnlyList<IslandCandidate> visibleCandidates,
        int selectedIndex,
        float markerRadius)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (visibleCandidates == null)
        {
            throw new ArgumentNullException(nameof(visibleCandidates));
        }

        if (visibleCandidates.Count == 0)
        {
            Hide();
            return;
        }

        int safeSelectedIndex = global::System.Math.Clamp(selectedIndex, 0, visibleCandidates.Count - 1);
        float safeMarkerRadius = float.IsFinite(markerRadius)
            ? MathF.Max(MinimumMarkerRadius, markerRadius)
            : 0.5f;
        List<Vector3> inactivePositions = new List<Vector3>(global::System.Math.Max(0, visibleCandidates.Count - 1));

        for (int candidateIndex = 0; candidateIndex < visibleCandidates.Count; candidateIndex++)
        {
            if (candidateIndex != safeSelectedIndex)
            {
                inactivePositions.Add(visibleCandidates[candidateIndex].BirthPosition);
            }
        }

        _inactiveMarkerModel.Geometry = CreateMarkerGeometry(inactivePositions, safeMarkerRadius);
        _activeMarkerModel.Geometry = CreateMarkerGeometry(
            new[] { visibleCandidates[safeSelectedIndex].BirthPosition },
            safeMarkerRadius * 1.35f);
        _birthHighlightModel.Geometry = CreateTriangleOverlayGeometry(
            mesh,
            visibleCandidates[safeSelectedIndex].BirthTriangleIndices);
        _inactiveMarkerModel.Visibility = inactivePositions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _activeMarkerModel.Visibility = Visibility.Visible;
        _birthHighlightModel.Visibility = visibleCandidates[safeSelectedIndex].BirthTriangleIndices.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Clears transient geometry and hides every island overlay model.
    /// </summary>
    public void Hide()
    {
        _inactiveMarkerModel.Geometry = null;
        _activeMarkerModel.Geometry = null;
        _birthHighlightModel.Geometry = null;
        _inactiveMarkerModel.Visibility = Visibility.Collapsed;
        _activeMarkerModel.Visibility = Visibility.Collapsed;
        _birthHighlightModel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Creates one non-selectable transparent overlay model.
    /// </summary>
    private static MeshGeometryModel3D CreateModel(Color4 color)
    {
        return new MeshGeometryModel3D
        {
            Material = new PhongMaterial
            {
                AmbientColor = color,
                DiffuseColor = color,
                SpecularColor = new Color4(0.1f, 0.1f, 0.1f, color.Alpha),
                SpecularShininess = 8.0f
            },
            CullMode = SharpDX.Direct3D11.CullMode.None,
            IsHitTestVisible = false,
            IsTransparent = color.Alpha < 1.0f,
            Visibility = Visibility.Collapsed
        };
    }

    /// <summary>
    /// Builds one combined octahedral marker buffer instead of one scene model per candidate.
    /// </summary>
    private static MeshGeometry3D CreateMarkerGeometry(IReadOnlyList<Vector3> centers, float radius)
    {
        Vector3Collection positions = new Vector3Collection(centers.Count * 6);
        Vector3Collection normals = new Vector3Collection(centers.Count * 6);
        IntCollection indices = new IntCollection(centers.Count * 24);
        Vector3[] offsets =
        {
            Vector3.UnitX,
            -Vector3.UnitX,
            Vector3.UnitY,
            -Vector3.UnitY,
            Vector3.UnitZ,
            -Vector3.UnitZ
        };
        int[] localIndices =
        {
            4, 0, 2,
            4, 2, 1,
            4, 1, 3,
            4, 3, 0,
            5, 2, 0,
            5, 1, 2,
            5, 3, 1,
            5, 0, 3
        };

        for (int centerIndex = 0; centerIndex < centers.Count; centerIndex++)
        {
            int baseVertex = positions.Count;

            for (int offsetIndex = 0; offsetIndex < offsets.Length; offsetIndex++)
            {
                positions.Add(centers[centerIndex] + (offsets[offsetIndex] * radius));
                normals.Add(offsets[offsetIndex]);
            }

            for (int localIndex = 0; localIndex < localIndices.Length; localIndex++)
            {
                indices.Add(baseVertex + localIndices[localIndex]);
            }
        }

        return new MeshGeometry3D
        {
            Positions = positions,
            Normals = normals,
            Indices = indices
        };
    }

    /// <summary>
    /// Maps original triangle ordinals to transformed display geometry without topology inference.
    /// </summary>
    private static MeshGeometry3D CreateTriangleOverlayGeometry(
        MeshEntity mesh,
        IReadOnlyList<int> triangleOrdinals)
    {
        Vector3Collection positions = new Vector3Collection(triangleOrdinals.Count * 3);
        Vector3Collection normals = new Vector3Collection(triangleOrdinals.Count * 3);
        IntCollection indices = new IntCollection(triangleOrdinals.Count * 3);
        Matrix4x4 transform = mesh.WorldTransform;
        int triangleCount = mesh.TriangleIndices.Count / 3;

        for (int ordinalIndex = 0; ordinalIndex < triangleOrdinals.Count; ordinalIndex++)
        {
            int triangleIndex = triangleOrdinals[ordinalIndex];

            if (triangleIndex < 0 || triangleIndex >= triangleCount)
            {
                continue;
            }

            int sourceBaseIndex = triangleIndex * 3;
            Vector3 first = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[sourceBaseIndex]], transform);
            Vector3 second = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[sourceBaseIndex + 1]], transform);
            Vector3 third = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[sourceBaseIndex + 2]], transform);
            Vector3 normal = Vector3.Cross(second - first, third - first);

            if (normal.LengthSquared() <= 0.000000000001f)
            {
                continue;
            }

            normal = Vector3.Normalize(normal);
            int destinationBaseIndex = positions.Count;
            positions.Add(first);
            positions.Add(second);
            positions.Add(third);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            indices.Add(destinationBaseIndex);
            indices.Add(destinationBaseIndex + 1);
            indices.Add(destinationBaseIndex + 2);
        }

        return new MeshGeometry3D
        {
            Positions = positions,
            Normals = normals,
            Indices = indices
        };
    }
}
