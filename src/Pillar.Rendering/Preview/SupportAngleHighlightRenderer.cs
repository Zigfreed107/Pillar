// SupportAngleHighlightRenderer.cs
// Draws render-only overlays on low-angle support heads and branches for the Direct Edit tool.
using HelixToolkit;
using HelixToolkit.Geometry;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Entities;
using Pillar.Core.Supports;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;

namespace Pillar.Rendering.Preview;

/// <summary>
/// Rebuilds lightweight colored overlays when Direct Edit angle diagnostics or document output changes.
/// </summary>
public sealed class SupportAngleHighlightRenderer
{
    private readonly GroupModel3D _root = new GroupModel3D();
    private readonly int _radialSegments;

    /// <summary>
    /// Adds the highlight overlay root to the scene preview collection.
    /// </summary>
    public SupportAngleHighlightRenderer(GroupModel3D sceneRoot, int radialSegments)
    {
        _radialSegments = global::System.Math.Max(6, radialSegments);
        sceneRoot.Children.Add(_root);
    }

    /// <summary>
    /// Rebuilds only the head and branch parts that fall below the configured XY-plane angle threshold.
    /// </summary>
    public void Refresh(
        IEnumerable<CadEntity> entities,
        double thresholdDegrees,
        Color4 color,
        Func<SupportEntity, bool> isVisible,
        Func<Guid, float> getLayerOpacity)
    {
        _root.Children.Clear();

        foreach (CadEntity entity in entities)
        {
            if (entity is not SupportEntity support
                || !isVisible(support)
                || support.Style.Kind == SupportStyleKind.BraceMember
                || support.Style.Kind == SupportStyleKind.Buttress)
            {
                continue;
            }

            bool highlightHead = CalculateAngleFromXyPlane(support.HeadDirection) < thresholdDegrees;
            bool highlightBranch = support.BranchLength > 0.0001f
                && CalculateAngleFromXyPlane(support.BranchDirection) < thresholdDegrees;

            if (!highlightHead && !highlightBranch)
            {
                continue;
            }

            MeshBuilder builder = new MeshBuilder();
            SupportPartDimensions dimensions = SupportDimensionResolver.Resolve(support.Profile, support.Style);
            Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(support.HeadDirection, support.Profile);
            Vector3 headJoint = support.TipPosition - (headDirection * support.Profile.HeadHeight);

            if (highlightHead)
            {
                float headLength = Vector3.Distance(headJoint, support.TipPosition);

                if (headLength > 0.0001f)
                {
                    builder.AddCone(
                        headJoint,
                        headDirection,
                        dimensions.HeadBottomDiameter * 0.5f,
                        dimensions.HeadTopDiameter * 0.5f,
                        headLength,
                        true,
                        true,
                        _radialSegments);
                }

                Vector3 penetrationTip = support.TipPosition + (headDirection * support.Profile.HeadPenetrationDepth);

                if (Vector3.DistanceSquared(support.TipPosition, penetrationTip) > 0.00000001f)
                {
                    builder.AddCylinder(support.TipPosition, penetrationTip, dimensions.HeadTopDiameter, _radialSegments);
                }
            }

            if (highlightBranch)
            {
                Vector3 stemJoint = headJoint - (Vector3.Normalize(support.BranchDirection) * support.BranchLength);
                builder.AddCylinder(stemJoint, headJoint, dimensions.BranchDiameter, _radialSegments);
            }

            float opacity = global::System.Math.Clamp(getLayerOpacity(support.SupportLayerGroupId), 0.0f, 1.0f);
            Color4 visibleColor = new Color4(color.Red, color.Green, color.Blue, color.Alpha * opacity);
            MeshGeometryModel3D model = new MeshGeometryModel3D
            {
                Geometry = builder.ToMeshGeometry3D(),
                Material = new PhongMaterial
                {
                    AmbientColor = visibleColor,
                    DiffuseColor = visibleColor,
                    SpecularColor = new Color4(0.1f, 0.1f, 0.1f, visibleColor.Alpha),
                    SpecularShininess = 12.0f
                },
                IsTransparent = visibleColor.Alpha < 1.0f,
                IsHitTestVisible = false,
                Visibility = Visibility.Visible
            };
            _root.Children.Add(model);
        }

        _root.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Clears all angle overlays when Direct Edit closes.
    /// </summary>
    public void Hide()
    {
        _root.Children.Clear();
        _root.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Calculates the acute angle between one direction and the XY plane.
    /// </summary>
    private static double CalculateAngleFromXyPlane(Vector3 direction)
    {
        Vector3 normalized = direction.LengthSquared() > 0.000001f ? Vector3.Normalize(direction) : Vector3.UnitZ;
        return global::System.Math.Asin(global::System.Math.Clamp(global::System.Math.Abs(normalized.Z), 0.0f, 1.0f)) * (180.0 / global::System.Math.PI);
    }
}
