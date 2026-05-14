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
    private const float AxialTolerance = 0.0001f;

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

        (Vector3 U, Vector3 V) frame = CreatePerpendicularFrame(axisDirection);
        List<Vector3> positions = new List<Vector3>();
        List<int> triangleIndices = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        List<SectionStation> stations = CreateSectionStations(support, totalLength);

        for (int stationIndex = 0; stationIndex < stations.Count - 1; stationIndex++)
        {
            SectionStation startStation = stations[stationIndex];
            SectionStation endStation = stations[stationIndex + 1];

            AddFrustum(
                positions,
                triangleIndices,
                normals,
                support.BasePosition + (axisDirection * startStation.DistanceFromBase),
                support.BasePosition + (axisDirection * endStation.DistanceFromBase),
                startStation.Radius,
                endStation.Radius,
                frame.U,
                frame.V,
                validatedRadialSegments);
        }

        SectionStation baseStation = stations[0];
        SectionStation topStation = stations[stations.Count - 1];
        AddCap(positions, triangleIndices, normals, support.BasePosition, baseStation.Radius, -axisDirection, frame.U, frame.V, validatedRadialSegments);
        AddCap(positions, triangleIndices, normals, support.BasePosition + (axisDirection * topStation.DistanceFromBase), topStation.Radius, axisDirection, frame.U, frame.V, validatedRadialSegments);

        return new SupportMeshData(positions, triangleIndices, normals);
    }

    /// <summary>
    /// Creates an ordered support profile chain from the build plate to the penetration tip.
    /// </summary>
    private static List<SectionStation> CreateSectionStations(SupportEntity support, float totalLength)
    {
        float baseBottomRadius = support.Profile.BaseBottomRadius;
        float stemBottomRadius = support.Profile.StemBottomDiameter * 0.5f;
        float stemTopRadius = support.Profile.StemTopDiameter * 0.5f;
        float headBottomRadius = support.Profile.HeadBottomDiameter * 0.5f;
        float headTopRadius = support.Profile.HeadTopDiameter * 0.5f;
        float baseHeight = MathF.Min(support.Profile.BaseHeight, totalLength);
        float distanceAboveBase = MathF.Max(0.0f, totalLength - baseHeight);
        float headHeight = MathF.Min(support.Profile.HeadHeight, distanceAboveBase);
        float stemHeight = MathF.Max(0.0f, distanceAboveBase - headHeight);
        bool hasStem = stemHeight > AxialTolerance;
        bool hasHead = headHeight > AxialTolerance;
        float baseTopRadius = hasStem
            ? stemBottomRadius
            : hasHead
                ? headBottomRadius
                : headTopRadius;

        List<SectionStation> stations = new List<SectionStation>();
        AddStation(stations, 0.0f, baseBottomRadius);
        AddStation(stations, baseHeight, baseTopRadius);

        if (hasStem)
        {
            AddStation(stations, baseHeight + stemHeight, stemTopRadius);
        }

        if (hasHead)
        {
            AddStation(stations, totalLength, headTopRadius);
        }

        AddStation(stations, totalLength + support.Profile.HeadPenetrationDepth, headTopRadius);
        return stations;
    }

    /// <summary>
    /// Adds one axial station while avoiding zero-length section duplicates.
    /// </summary>
    private static void AddStation(List<SectionStation> stations, float distanceFromBase, float radius)
    {
        if (stations.Count > 0)
        {
            SectionStation lastStation = stations[stations.Count - 1];

            if (MathF.Abs(distanceFromBase - lastStation.DistanceFromBase) <= AxialTolerance)
            {
                stations[stations.Count - 1] = new SectionStation(distanceFromBase, radius);
                return;
            }
        }

        stations.Add(new SectionStation(distanceFromBase, radius));
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
    /// Adds one frustum segment as a triangle list using modulo ring closure.
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
        Vector3[] startRing = CreateRingPositions(startCenter, startRadius, frameU, frameV, radialSegments);
        Vector3[] endRing = CreateRingPositions(endCenter, endRadius, frameU, frameV, radialSegments);

        for (int segmentIndex = 0; segmentIndex < radialSegments; segmentIndex++)
        {
            int nextSegmentIndex = (segmentIndex + 1) % radialSegments;
            Vector3 startA = startRing[segmentIndex];
            Vector3 startB = startRing[nextSegmentIndex];
            Vector3 endA = endRing[segmentIndex];
            Vector3 endB = endRing[nextSegmentIndex];

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
        Vector3[] capRing = CreateRingPositions(center, radius, frameU, frameV, radialSegments);

        for (int segmentIndex = 0; segmentIndex < radialSegments; segmentIndex++)
        {
            int nextSegmentIndex = (segmentIndex + 1) % radialSegments;
            Vector3 ringA = capRing[segmentIndex];
            Vector3 ringB = capRing[nextSegmentIndex];
            Vector3 cross = Vector3.Cross(ringA - center, ringB - center);

            if (Vector3.Dot(capNormal, cross) >= 0.0f)
            {
                AddTriangle(positions, triangleIndices, normals, center, ringA, ringB);
            }
            else
            {
                AddTriangle(positions, triangleIndices, normals, center, ringB, ringA);
            }
        }
    }

    /// <summary>
    /// Creates one circular ring without adding a duplicate endpoint at 2 PI.
    /// </summary>
    private static Vector3[] CreateRingPositions(
        Vector3 center,
        float radius,
        Vector3 frameU,
        Vector3 frameV,
        int radialSegments)
    {
        Vector3[] ringPositions = new Vector3[radialSegments];

        for (int segmentIndex = 0; segmentIndex < radialSegments; segmentIndex++)
        {
            float angle = (float)(segmentIndex * Math.PI * 2.0 / radialSegments);
            ringPositions[segmentIndex] = center + (CreateRingOffset(angle, frameU, frameV) * radius);
        }

        return ringPositions;
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

    /// <summary>
    /// Stores one radius at one distance along the support axis.
    /// </summary>
    private readonly struct SectionStation
    {
        /// <summary>
        /// Creates one support section station.
        /// </summary>
        public SectionStation(float distanceFromBase, float radius)
        {
            DistanceFromBase = distanceFromBase;
            Radius = radius;
        }

        /// <summary>
        /// Gets the distance from the support base along the support axis.
        /// </summary>
        public float DistanceFromBase { get; }

        /// <summary>
        /// Gets the support radius at this station.
        /// </summary>
        public float Radius { get; }
    }
}
