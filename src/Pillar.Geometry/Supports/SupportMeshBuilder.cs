// SupportMeshBuilder.cs
// Generates procedural triangle geometry for support entities without introducing rendering dependencies.
using Pillar.Core.Entities;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Geometry.Supports;

/// <summary>
/// Builds triangle mesh data for one support entity.
/// </summary>
public static class SupportMeshBuilder
{
    private const int DefaultRadialSegments = 16;
    private const int MinimumRadialSegments = 6;
    private const int MaximumRadialSegments = 96;

    /// <summary>
    /// Generates the current procedural mesh for one support entity.
    /// </summary>
    public static SupportMeshData Build(SupportEntity support)
    {
        return Build(support, DefaultRadialSegments);
    }

    /// <summary>
    /// Generates the current procedural mesh for one support entity using the requested cylindrical side count.
    /// </summary>
    public static SupportMeshData Build(SupportEntity support, int radialSegments)
    {
        if (support == null)
        {
            throw new ArgumentNullException(nameof(support));
        }

        int validatedRadialSegments = Math.Clamp(radialSegments, MinimumRadialSegments, MaximumRadialSegments);
        Vector3 axisVector = support.TipPosition - support.BasePosition;
        float totalLength = axisVector.Length();
        Vector3 axisDirection = Vector3.Normalize(axisVector);

        float baseHeight = MathF.Min(support.Profile.BaseHeight, totalLength);
        float distanceAboveBase = MathF.Max(0.0f, totalLength - baseHeight);
        float headHeight = MathF.Min(support.Profile.HeadHeight, distanceAboveBase);
        float stemHeight = MathF.Max(0.0f, distanceAboveBase - headHeight);
        bool hasStem = stemHeight > 0.0001f;

        (Vector3 U, Vector3 V) frame = CreatePerpendicularFrame(axisDirection);
        List<Vector3> positions = new List<Vector3>();
        List<int> triangleIndices = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        Vector3 baseBottom = support.BasePosition;
        Vector3 baseTop = baseBottom + (axisDirection * baseHeight);
        Vector3 headBottom = support.TipPosition - (axisDirection * headHeight);
        Vector3 penetrationTop = support.TipPosition + (axisDirection * support.Profile.HeadPenetrationDepth);
        float baseBottomRadius = support.Profile.BaseBottomRadius;
        float baseTopRadius = (hasStem ? support.Profile.StemBottomDiameter : support.Profile.HeadBottomDiameter) * 0.5f;
        float stemBottomRadius = support.Profile.StemBottomDiameter * 0.5f;
        float stemTopRadius = support.Profile.StemTopDiameter * 0.5f;
        float headBottomRadius = support.Profile.HeadBottomDiameter * 0.5f;
        float headTopRadius = support.Profile.HeadTopDiameter * 0.5f;

        AddFrustum(positions, triangleIndices, normals, baseBottom, baseTop, baseBottomRadius, baseTopRadius, frame.U, frame.V, validatedRadialSegments);

        if (hasStem)
        {
            AddFrustum(positions, triangleIndices, normals, baseTop, headBottom, stemBottomRadius, stemTopRadius, frame.U, frame.V, validatedRadialSegments);
        }

        if (headHeight > 0.0001f)
        {
            AddFrustum(positions, triangleIndices, normals, headBottom, support.TipPosition, headBottomRadius, headTopRadius, frame.U, frame.V, validatedRadialSegments);
        }

        AddCylinder(positions, triangleIndices, normals, support.TipPosition, penetrationTop, headTopRadius, frame.U, frame.V, validatedRadialSegments);
        AddCap(positions, triangleIndices, normals, baseBottom, baseBottomRadius, -axisDirection, frame.U, frame.V, validatedRadialSegments);
        AddCap(positions, triangleIndices, normals, penetrationTop, headTopRadius, axisDirection, frame.U, frame.V, validatedRadialSegments);

        return new SupportMeshData(positions, triangleIndices, normals);
    }

    /// <summary>
    /// Creates a stable perpendicular basis around the support axis.
    /// </summary>
    private static (Vector3 U, Vector3 V) CreatePerpendicularFrame(Vector3 axisDirection)
    {
        Vector3 reference = MathF.Abs(Vector3.Dot(axisDirection, Vector3.UnitZ)) > 0.95f
            ? Vector3.UnitX
            : Vector3.UnitZ;

        Vector3 u = Vector3.Normalize(Vector3.Cross(axisDirection, reference));
        Vector3 v = Vector3.Normalize(Vector3.Cross(axisDirection, u));
        return (u, v);
    }

    /// <summary>
    /// Adds one cylindrical segment by delegating to the general frustum builder.
    /// </summary>
    private static void AddCylinder(
        List<Vector3> positions,
        List<int> triangleIndices,
        List<Vector3> normals,
        Vector3 startCenter,
        Vector3 endCenter,
        float radius,
        Vector3 frameU,
        Vector3 frameV,
        int radialSegments)
    {
        AddFrustum(positions, triangleIndices, normals, startCenter, endCenter, radius, radius, frameU, frameV, radialSegments);
    }

    /// <summary>
    /// Adds one frustum segment as a triangle list with flat-shaded normals.
    /// </summary>
    private static void AddFrustum(
        List<Vector3> positions,
        List<int> triangleIndices,
        List<Vector3> normals,
        Vector3 startCenter,
        Vector3 endCenter,
        float startRadius,
        float endRadius,
        Vector3 frameU,
        Vector3 frameV,
        int radialSegments)
    {
        for (int segmentIndex = 0; segmentIndex < radialSegments; segmentIndex++)
        {
            float startAngle = (float)(segmentIndex * Math.PI * 2.0 / radialSegments);
            float endAngle = (float)((segmentIndex + 1) * Math.PI * 2.0 / radialSegments);

            Vector3 startOffsetA = CreateRingOffset(startAngle, frameU, frameV);
            Vector3 startOffsetB = CreateRingOffset(endAngle, frameU, frameV);

            Vector3 startA = startCenter + (startOffsetA * startRadius);
            Vector3 startB = startCenter + (startOffsetB * startRadius);
            Vector3 endA = endCenter + (startOffsetA * endRadius);
            Vector3 endB = endCenter + (startOffsetB * endRadius);

            // Wind the wall quads so the generated triangle normals point away from the support axis.
            // Helix uses back-face culling for support meshes, so reversed winding makes the body render inside out.
            AddTriangle(positions, triangleIndices, normals, startA, endB, endA);
            AddTriangle(positions, triangleIndices, normals, startA, startB, endB);
        }
    }

    /// <summary>
    /// Adds one circular end cap to close the support mesh.
    /// </summary>
    private static void AddCap(
        List<Vector3> positions,
        List<int> triangleIndices,
        List<Vector3> normals,
        Vector3 center,
        float radius,
        Vector3 capNormal,
        Vector3 frameU,
        Vector3 frameV,
        int radialSegments)
    {
        for (int segmentIndex = 0; segmentIndex < radialSegments; segmentIndex++)
        {
            float startAngle = (float)(segmentIndex * Math.PI * 2.0 / radialSegments);
            float endAngle = (float)((segmentIndex + 1) * Math.PI * 2.0 / radialSegments);

            Vector3 offsetA = CreateRingOffset(startAngle, frameU, frameV) * radius;
            Vector3 offsetB = CreateRingOffset(endAngle, frameU, frameV) * radius;
            Vector3 cross = Vector3.Cross(offsetA, offsetB);

            if (Vector3.Dot(capNormal, cross) >= 0.0f)
            {
                AddTriangle(positions, triangleIndices, normals, center, center + offsetA, center + offsetB);
            }
            else
            {
                AddTriangle(positions, triangleIndices, normals, center, center + offsetB, center + offsetA);
            }
        }
    }

    /// <summary>
    /// Converts one polar angle into the local ring direction.
    /// </summary>
    private static Vector3 CreateRingOffset(float angle, Vector3 frameU, Vector3 frameV)
    {
        return (frameU * MathF.Cos(angle)) + (frameV * MathF.Sin(angle));
    }

    /// <summary>
    /// Adds one flat-shaded triangle to the output mesh buffers.
    /// </summary>
    private static void AddTriangle(
        List<Vector3> positions,
        List<int> triangleIndices,
        List<Vector3> normals,
        Vector3 a,
        Vector3 b,
        Vector3 c)
    {
        int firstIndex = positions.Count;
        Vector3 triangleNormal = Vector3.Normalize(Vector3.Cross(b - a, c - a));

        positions.Add(a);
        positions.Add(b);
        positions.Add(c);

        normals.Add(triangleNormal);
        normals.Add(triangleNormal);
        normals.Add(triangleNormal);

        triangleIndices.Add(firstIndex);
        triangleIndices.Add(firstIndex + 1);
        triangleIndices.Add(firstIndex + 2);
    }
}
