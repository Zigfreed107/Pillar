// SupportMeshBuilder.cs
// Generates procedural triangle geometry for support entities without introducing rendering dependencies.
using Pillar.Core.Entities;
using Pillar.Core.Supports;
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
        List<Vector3> positions = new List<Vector3>();
        List<int> triangleIndices = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        SupportPartDimensions dimensions = SupportDimensionResolver.Resolve(support.Profile, support.Style);
        Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(support.HeadDirection, support.Profile);
        float headLength = CalculateUsableHeadLength(support.TipPosition, support.BasePosition.Z, headDirection, support.Profile.HeadHeight);
        Vector3 headJointPosition = support.TipPosition - (headDirection * headLength);
        bool hasBranch = support.BranchLength > AxialTolerance;
        Vector3 branchDirection = hasBranch
            ? Vector3.Normalize(support.BranchDirection)
            : Vector3.UnitZ;
        Vector3 stemJointPosition = hasBranch
            ? headJointPosition - (branchDirection * support.BranchLength)
            : headJointPosition;
        Vector3 stemBasePosition = new Vector3(stemJointPosition.X, stemJointPosition.Y, support.BasePosition.Z);
        float verticalLength = MathF.Max(0.0f, stemJointPosition.Z - stemBasePosition.Z);

        if (verticalLength > AxialTolerance)
        {
            (Vector3 U, Vector3 V) verticalFrame = CreatePerpendicularFrame(Vector3.UnitZ);
            List<SectionStation> stemStations = CreateVerticalSectionStations(support.Profile, dimensions, verticalLength);

            for (int stationIndex = 0; stationIndex < stemStations.Count - 1; stationIndex++)
            {
                SectionStation startStation = stemStations[stationIndex];
                SectionStation endStation = stemStations[stationIndex + 1];

                AddFrustum(
                    positions,
                    triangleIndices,
                    normals,
                    stemBasePosition + (Vector3.UnitZ * startStation.DistanceFromBase),
                    stemBasePosition + (Vector3.UnitZ * endStation.DistanceFromBase),
                    startStation.Radius,
                    endStation.Radius,
                    verticalFrame.U,
                    verticalFrame.V,
                    validatedRadialSegments);
            }

            SectionStation baseStation = stemStations[0];
            SectionStation topStation = stemStations[stemStations.Count - 1];
            AddCap(positions, triangleIndices, normals, stemBasePosition, baseStation.Radius, -Vector3.UnitZ, verticalFrame.U, verticalFrame.V, validatedRadialSegments);
            AddCap(positions, triangleIndices, normals, stemBasePosition + (Vector3.UnitZ * topStation.DistanceFromBase), topStation.Radius, Vector3.UnitZ, verticalFrame.U, verticalFrame.V, validatedRadialSegments);
        }

        AddClosedHead(
            positions,
            triangleIndices,
            normals,
            headJointPosition,
            support.TipPosition,
            headDirection,
            support.Profile,
            dimensions,
            validatedRadialSegments);

        if (hasBranch)
        {
            AddClosedBranch(
                positions,
                triangleIndices,
                normals,
                stemJointPosition,
                headJointPosition,
                branchDirection,
                dimensions,
                validatedRadialSegments);

            AddJointBall(
                positions,
                triangleIndices,
                normals,
                stemJointPosition,
                MathF.Max(dimensions.StemTopDiameter, dimensions.BranchDiameter) * 0.5f,
                validatedRadialSegments);

            AddJointBall(
                positions,
                triangleIndices,
                normals,
                headJointPosition,
                dimensions.BranchDiameter * 0.5f,
                validatedRadialSegments);
        }
        else
        {
            AddJointBall(
                positions,
                triangleIndices,
                normals,
                headJointPosition,
                MathF.Max(dimensions.StemTopDiameter, dimensions.HeadBottomDiameter) * 0.5f,
                validatedRadialSegments);
        }

        return new SupportMeshData(positions, triangleIndices, normals);
    }

    /// <summary>
    /// Calculates the head length that can fit above the build plate for the current head direction.
    /// </summary>
    private static float CalculateUsableHeadLength(Vector3 tipPosition, float baseZ, Vector3 headDirection, float requestedHeadLength)
    {
        if (headDirection.Z <= AxialTolerance)
        {
            return requestedHeadLength;
        }

        float maximumLengthByHeight = MathF.Max(0.0f, (tipPosition.Z - baseZ) / headDirection.Z);
        return MathF.Min(requestedHeadLength, maximumLengthByHeight);
    }

    /// <summary>
    /// Creates an ordered vertical base-and-stem profile chain from the build plate to the angled head joint.
    /// </summary>
    private static List<SectionStation> CreateVerticalSectionStations(SupportProfile profile, SupportPartDimensions dimensions, float totalLength)
    {
        float baseBottomRadius = profile.BaseBottomRadius;
        float stemBottomRadius = dimensions.StemBottomDiameter * 0.5f;
        float stemTopRadius = dimensions.StemTopDiameter * 0.5f;
        float headBottomRadius = dimensions.HeadBottomDiameter * 0.5f;
        float baseHeight = MathF.Min(profile.BaseHeight, totalLength);
        float distanceAboveBase = MathF.Max(0.0f, totalLength - baseHeight);
        float stemHeight = distanceAboveBase;
        bool hasStem = stemHeight > AxialTolerance;
        float baseTopRadius = hasStem
            ? stemBottomRadius
            : headBottomRadius;

        List<SectionStation> stations = new List<SectionStation>();
        AddStation(stations, 0.0f, baseBottomRadius);
        AddStation(stations, baseHeight, baseTopRadius);

        if (hasStem)
        {
            AddStation(stations, baseHeight + stemHeight, stemTopRadius);
        }

        return stations;
    }

    /// <summary>
    /// Adds the angled head as a closed mesh from the joint through the model contact and penetration tip.
    /// </summary>
    private static void AddClosedHead(
        List<Vector3> positions,
        List<int> triangleIndices,
        List<Vector3> normals,
        Vector3 headBottomPosition,
        Vector3 tipPosition,
        Vector3 headDirection,
        SupportProfile profile,
        SupportPartDimensions dimensions,
        int radialSegments)
    {
        float headBottomRadius = dimensions.HeadBottomDiameter * 0.5f;
        float headTopRadius = dimensions.HeadTopDiameter * 0.5f;
        Vector3 penetrationTip = tipPosition + (headDirection * profile.HeadPenetrationDepth);
        (Vector3 U, Vector3 V) headFrame = CreatePerpendicularFrame(headDirection);

        if (Vector3.Distance(headBottomPosition, tipPosition) > AxialTolerance)
        {
            AddFrustum(
                positions,
                triangleIndices,
                normals,
                headBottomPosition,
                tipPosition,
                headBottomRadius,
                headTopRadius,
                headFrame.U,
                headFrame.V,
                radialSegments);
        }

        if (Vector3.Distance(tipPosition, penetrationTip) > AxialTolerance)
        {
            AddFrustum(
                positions,
                triangleIndices,
                normals,
                tipPosition,
                penetrationTip,
                headTopRadius,
                headTopRadius,
                headFrame.U,
                headFrame.V,
                radialSegments);
        }

        AddCap(positions, triangleIndices, normals, headBottomPosition, headBottomRadius, -headDirection, headFrame.U, headFrame.V, radialSegments);
        AddCap(positions, triangleIndices, normals, penetrationTip, headTopRadius, headDirection, headFrame.U, headFrame.V, radialSegments);
    }

    /// <summary>
    /// Adds the optional branch cylinder as a closed mesh between the vertical stem and angled head.
    /// </summary>
    private static void AddClosedBranch(
        List<Vector3> positions,
        List<int> triangleIndices,
        List<Vector3> normals,
        Vector3 stemJointPosition,
        Vector3 headJointPosition,
        Vector3 branchDirection,
        SupportPartDimensions dimensions,
        int radialSegments)
    {
        float branchRadius = dimensions.BranchDiameter * 0.5f;
        (Vector3 U, Vector3 V) branchFrame = CreatePerpendicularFrame(branchDirection);

        AddFrustum(
            positions,
            triangleIndices,
            normals,
            stemJointPosition,
            headJointPosition,
            branchRadius,
            branchRadius,
            branchFrame.U,
            branchFrame.V,
            radialSegments);

        AddCap(positions, triangleIndices, normals, stemJointPosition, branchRadius, -branchDirection, branchFrame.U, branchFrame.V, radialSegments);
        AddCap(positions, triangleIndices, normals, headJointPosition, branchRadius, branchDirection, branchFrame.U, branchFrame.V, radialSegments);
    }

    /// <summary>
    /// Adds the smooth ball joint that visually bridges the shifted stem and angled head.
    /// </summary>
    private static void AddJointBall(
        List<Vector3> positions,
        List<int> triangleIndices,
        List<Vector3> normals,
        Vector3 center,
        float radius,
        int radialSegments)
    {
        AddSphere(positions, triangleIndices, normals, center, radius, radialSegments);
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
    /// Adds a closed UV sphere using the same radial side count as the support body.
    /// </summary>
    private static void AddSphere(
        List<Vector3> positions,
        List<int> triangleIndices,
        List<Vector3> normals,
        Vector3 center,
        float radius,
        int radialSegments)
    {
        int verticalSegments = Math.Max(4, radialSegments / 2);
        (Vector3 U, Vector3 V) frame = CreatePerpendicularFrame(Vector3.UnitZ);
        Vector3 top = center + (Vector3.UnitZ * radius);
        Vector3 bottom = center - (Vector3.UnitZ * radius);
        Vector3[][] rings = new Vector3[verticalSegments - 1][];

        for (int stackIndex = 1; stackIndex < verticalSegments; stackIndex++)
        {
            float phi = (float)(Math.PI * stackIndex / verticalSegments);
            float ringRadius = MathF.Sin(phi) * radius;
            float zOffset = MathF.Cos(phi) * radius;
            rings[stackIndex - 1] = CreateRingPositions(center + (Vector3.UnitZ * zOffset), ringRadius, frame.U, frame.V, radialSegments);
        }

        Vector3[] firstRing = rings[0];

        for (int segmentIndex = 0; segmentIndex < radialSegments; segmentIndex++)
        {
            int nextSegmentIndex = (segmentIndex + 1) % radialSegments;
            AddTriangle(positions, triangleIndices, normals, top, firstRing[segmentIndex], firstRing[nextSegmentIndex]);
        }

        for (int stackIndex = 0; stackIndex < rings.Length - 1; stackIndex++)
        {
            Vector3[] upperRing = rings[stackIndex];
            Vector3[] lowerRing = rings[stackIndex + 1];

            for (int segmentIndex = 0; segmentIndex < radialSegments; segmentIndex++)
            {
                int nextSegmentIndex = (segmentIndex + 1) % radialSegments;
                AddTriangle(positions, triangleIndices, normals, upperRing[segmentIndex], lowerRing[segmentIndex], lowerRing[nextSegmentIndex]);
                AddTriangle(positions, triangleIndices, normals, upperRing[segmentIndex], lowerRing[nextSegmentIndex], upperRing[nextSegmentIndex]);
            }
        }

        Vector3[] lastRing = rings[rings.Length - 1];

        for (int segmentIndex = 0; segmentIndex < radialSegments; segmentIndex++)
        {
            int nextSegmentIndex = (segmentIndex + 1) % radialSegments;
            AddTriangle(positions, triangleIndices, normals, bottom, lastRing[nextSegmentIndex], lastRing[segmentIndex]);
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




