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
        SupportMeshAccumulator mesh = new SupportMeshAccumulator();
        SupportPartDimensions dimensions = SupportDimensionResolver.Resolve(support.Profile, support.Style);

        if (support.Style.Kind == SupportStyleKind.BraceMember)
        {
            AddClosedBraceMember(
                mesh,
                support.BasePosition,
                support.TipPosition,
                dimensions.BranchDiameter,
                validatedRadialSegments);
            return mesh.CreateMeshData();
        }

        bool isButtress = support.Style.Kind == SupportStyleKind.Buttress;
        Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(support.HeadDirection, support.Profile);
        float headLength = isButtress
            ? 0.0f
            : CalculateUsableHeadLength(support.TipPosition, support.BasePosition.Z, headDirection, support.Profile.HeadHeight);
        Vector3 headJointPosition = isButtress
            ? support.TipPosition
            : support.TipPosition - (headDirection * headLength);
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
            AddClosedStem(
                mesh,
                stemBasePosition,
                stemStations,
                verticalFrame.U,
                verticalFrame.V,
                validatedRadialSegments);
        }

        if (!isButtress)
        {
            AddClosedHead(
                mesh,
                headJointPosition,
                support.TipPosition,
                headDirection,
                support.Profile,
                dimensions,
                validatedRadialSegments);
        }

        if (hasBranch)
        {
            AddClosedBranch(
                mesh,
                stemJointPosition,
                headJointPosition,
                branchDirection,
                dimensions,
                validatedRadialSegments);

            AddJointBall(
                mesh,
                stemJointPosition,
                MathF.Max(dimensions.StemTopDiameter, dimensions.BranchDiameter) * 0.5f,
                validatedRadialSegments);

            AddJointBall(
                mesh,
                headJointPosition,
                dimensions.BranchDiameter * 0.5f,
                validatedRadialSegments);
        }
        else
        {
            AddJointBall(
                mesh,
                headJointPosition,
                MathF.Max(dimensions.StemTopDiameter, dimensions.HeadBottomDiameter) * 0.5f,
                validatedRadialSegments);
        }

        return mesh.CreateMeshData();
    }

    /// <summary>
    /// Adds a simple closed cylindrical reinforcement member between two saved endpoints.
    /// </summary>
    private static void AddClosedBraceMember(
        SupportMeshAccumulator mesh,
        Vector3 startPosition,
        Vector3 endPosition,
        float diameter,
        int radialSegments)
    {
        Vector3 axis = endPosition - startPosition;

        if (axis.LengthSquared() <= AxialTolerance * AxialTolerance)
        {
            return;
        }

        Vector3 axisDirection = Vector3.Normalize(axis);
        float radius = diameter * 0.5f;
        (Vector3 U, Vector3 V) frame = CreatePerpendicularFrame(axisDirection);
        AddClosedFrustum(
            mesh,
            startPosition,
            endPosition,
            radius,
            radius,
            axisDirection,
            frame.U,
            frame.V,
            radialSegments);
    }

    /// <summary>
    /// Adds the connected vertical profile while sharing every station ring between adjacent frustums and end caps.
    /// </summary>
    private static void AddClosedStem(
        SupportMeshAccumulator mesh,
        Vector3 stemBasePosition,
        IReadOnlyList<SectionStation> stations,
        Vector3 frameU,
        Vector3 frameV,
        int radialSegments)
    {
        int[][] stationRings = new int[stations.Count][];

        for (int stationIndex = 0; stationIndex < stations.Count; stationIndex++)
        {
            SectionStation station = stations[stationIndex];
            Vector3 stationCenter = stemBasePosition + (Vector3.UnitZ * station.DistanceFromBase);
            stationRings[stationIndex] = AddRing(mesh, stationCenter, station.Radius, frameU, frameV, radialSegments);
        }

        for (int stationIndex = 0; stationIndex < stationRings.Length - 1; stationIndex++)
        {
            AddFrustum(mesh, stationRings[stationIndex], stationRings[stationIndex + 1]);
        }

        SectionStation topStation = stations[stations.Count - 1];
        AddCap(mesh, stemBasePosition, stationRings[0], -Vector3.UnitZ);
        AddCap(
            mesh,
            stemBasePosition + (Vector3.UnitZ * topStation.DistanceFromBase),
            stationRings[stationRings.Length - 1],
            Vector3.UnitZ);
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
        SupportMeshAccumulator mesh,
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
        Vector3[] stationCenters = new Vector3[3];
        float[] stationRadii = new float[3];
        int stationCount = 0;

        if (Vector3.Distance(headBottomPosition, tipPosition) > AxialTolerance)
        {
            stationCenters[stationCount] = headBottomPosition;
            stationRadii[stationCount] = headBottomRadius;
            stationCount++;
        }

        stationCenters[stationCount] = tipPosition;
        stationRadii[stationCount] = headTopRadius;
        stationCount++;

        if (Vector3.Distance(tipPosition, penetrationTip) > AxialTolerance)
        {
            stationCenters[stationCount] = penetrationTip;
            stationRadii[stationCount] = headTopRadius;
            stationCount++;
        }

        AddClosedAxialChain(
            mesh,
            stationCenters,
            stationRadii,
            stationCount,
            headDirection,
            headFrame.U,
            headFrame.V,
            radialSegments);
    }

    /// <summary>
    /// Adds a connected axial chain with shared station rings and shared cap boundaries.
    /// </summary>
    private static void AddClosedAxialChain(
        SupportMeshAccumulator mesh,
        IReadOnlyList<Vector3> stationCenters,
        IReadOnlyList<float> stationRadii,
        int stationCount,
        Vector3 axisDirection,
        Vector3 frameU,
        Vector3 frameV,
        int radialSegments)
    {
        if (stationCount < 2)
        {
            return;
        }

        int[][] stationRings = new int[stationCount][];

        for (int stationIndex = 0; stationIndex < stationCount; stationIndex++)
        {
            stationRings[stationIndex] = AddRing(
                mesh,
                stationCenters[stationIndex],
                stationRadii[stationIndex],
                frameU,
                frameV,
                radialSegments);
        }

        for (int stationIndex = 0; stationIndex < stationCount - 1; stationIndex++)
        {
            AddFrustum(mesh, stationRings[stationIndex], stationRings[stationIndex + 1]);
        }

        AddCap(mesh, stationCenters[0], stationRings[0], -axisDirection);
        AddCap(
            mesh,
            stationCenters[stationCount - 1],
            stationRings[stationCount - 1],
            axisDirection);
    }

    /// <summary>
    /// Adds the optional branch cylinder as a separately closed component.
    /// </summary>
    private static void AddClosedBranch(
        SupportMeshAccumulator mesh,
        Vector3 stemJointPosition,
        Vector3 headJointPosition,
        Vector3 branchDirection,
        SupportPartDimensions dimensions,
        int radialSegments)
    {
        float branchRadius = dimensions.BranchDiameter * 0.5f;
        (Vector3 U, Vector3 V) branchFrame = CreatePerpendicularFrame(branchDirection);
        AddClosedFrustum(
            mesh,
            stemJointPosition,
            headJointPosition,
            branchRadius,
            branchRadius,
            branchDirection,
            branchFrame.U,
            branchFrame.V,
            radialSegments);
    }

    /// <summary>
    /// Adds the smooth ball joint that visually bridges the shifted stem and angled head.
    /// </summary>
    private static void AddJointBall(
        SupportMeshAccumulator mesh,
        Vector3 center,
        float radius,
        int radialSegments)
    {
        AddSphere(mesh, center, radius, radialSegments);
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
    private static void AddClosedFrustum(
        SupportMeshAccumulator mesh,
        Vector3 startCenter,
        Vector3 endCenter,
        float startRadius,
        float endRadius,
        Vector3 axisDirection,
        Vector3 frameU,
        Vector3 frameV,
        int radialSegments)
    {
        int[] startRing = AddRing(mesh, startCenter, startRadius, frameU, frameV, radialSegments);
        int[] endRing = AddRing(mesh, endCenter, endRadius, frameU, frameV, radialSegments);
        AddFrustum(mesh, startRing, endRing);
        AddCap(mesh, startCenter, startRing, -axisDirection);
        AddCap(mesh, endCenter, endRing, axisDirection);
    }

    /// <summary>
    /// Adds one frustum wall between two existing station rings.
    /// </summary>
    private static void AddFrustum(
        SupportMeshAccumulator mesh,
        IReadOnlyList<int> startRing,
        IReadOnlyList<int> endRing)
    {
        if (startRing.Count != endRing.Count)
        {
            throw new ArgumentException("Frustum rings must contain the same number of positions.");
        }

        for (int segmentIndex = 0; segmentIndex < startRing.Count; segmentIndex++)
        {
            int nextSegmentIndex = (segmentIndex + 1) % startRing.Count;
            int startA = startRing[segmentIndex];
            int startB = startRing[nextSegmentIndex];
            int endA = endRing[segmentIndex];
            int endB = endRing[nextSegmentIndex];

            // Preserve outward wall winding while render vertices remain independently expanded.
            mesh.AddTriangle(startA, endB, endA);
            mesh.AddTriangle(startA, startB, endB);
        }
    }

    /// <summary>
    /// Adds one indexed center and closes it against an existing wall ring.
    /// </summary>
    private static void AddCap(
        SupportMeshAccumulator mesh,
        Vector3 center,
        IReadOnlyList<int> ring,
        Vector3 capNormal)
    {
        int centerIndex = mesh.AddPosition(center);

        for (int segmentIndex = 0; segmentIndex < ring.Count; segmentIndex++)
        {
            int nextSegmentIndex = (segmentIndex + 1) % ring.Count;
            int ringAIndex = ring[segmentIndex];
            int ringBIndex = ring[nextSegmentIndex];
            Vector3 ringA = mesh.Positions[ringAIndex];
            Vector3 ringB = mesh.Positions[ringBIndex];
            Vector3 cross = Vector3.Cross(ringA - center, ringB - center);

            if (Vector3.Dot(capNormal, cross) >= 0.0f)
            {
                mesh.AddTriangle(centerIndex, ringAIndex, ringBIndex);
            }
            else
            {
                mesh.AddTriangle(centerIndex, ringBIndex, ringAIndex);
            }
        }
    }

    /// <summary>
    /// Adds one circular position ring without duplicating its endpoint at 2 PI.
    /// </summary>
    private static int[] AddRing(
        SupportMeshAccumulator mesh,
        Vector3 center,
        float radius,
        Vector3 frameU,
        Vector3 frameV,
        int radialSegments)
    {
        int[] ringIndices = new int[radialSegments];

        for (int segmentIndex = 0; segmentIndex < radialSegments; segmentIndex++)
        {
            float angle = (float)(segmentIndex * Math.PI * 2.0 / radialSegments);
            Vector3 position = center + (CreateRingOffset(angle, frameU, frameV) * radius);
            ringIndices[segmentIndex] = mesh.AddPosition(position);
        }

        return ringIndices;
    }

    /// <summary>
    /// Adds a closed indexed UV sphere using one pole and one shared position per latitude-ring segment.
    /// </summary>
    private static void AddSphere(
        SupportMeshAccumulator mesh,
        Vector3 center,
        float radius,
        int radialSegments)
    {
        int verticalSegments = Math.Max(4, radialSegments / 2);
        (Vector3 U, Vector3 V) frame = CreatePerpendicularFrame(Vector3.UnitZ);
        int topIndex = mesh.AddPosition(center + (Vector3.UnitZ * radius));
        int bottomIndex = mesh.AddPosition(center - (Vector3.UnitZ * radius));
        int[][] rings = new int[verticalSegments - 1][];

        for (int stackIndex = 1; stackIndex < verticalSegments; stackIndex++)
        {
            float phi = (float)(Math.PI * stackIndex / verticalSegments);
            float ringRadius = MathF.Sin(phi) * radius;
            float zOffset = MathF.Cos(phi) * radius;
            rings[stackIndex - 1] = AddRing(
                mesh,
                center + (Vector3.UnitZ * zOffset),
                ringRadius,
                frame.U,
                frame.V,
                radialSegments);
        }

        int[] firstRing = rings[0];

        for (int segmentIndex = 0; segmentIndex < radialSegments; segmentIndex++)
        {
            int nextSegmentIndex = (segmentIndex + 1) % radialSegments;
            mesh.AddTriangle(topIndex, firstRing[segmentIndex], firstRing[nextSegmentIndex]);
        }

        for (int stackIndex = 0; stackIndex < rings.Length - 1; stackIndex++)
        {
            int[] upperRing = rings[stackIndex];
            int[] lowerRing = rings[stackIndex + 1];

            for (int segmentIndex = 0; segmentIndex < radialSegments; segmentIndex++)
            {
                int nextSegmentIndex = (segmentIndex + 1) % radialSegments;
                mesh.AddTriangle(upperRing[segmentIndex], lowerRing[segmentIndex], lowerRing[nextSegmentIndex]);
                mesh.AddTriangle(upperRing[segmentIndex], lowerRing[nextSegmentIndex], upperRing[nextSegmentIndex]);
            }
        }

        int[] lastRing = rings[rings.Length - 1];

        for (int segmentIndex = 0; segmentIndex < radialSegments; segmentIndex++)
        {
            int nextSegmentIndex = (segmentIndex + 1) % radialSegments;
            mesh.AddTriangle(bottomIndex, lastRing[nextSegmentIndex], lastRing[segmentIndex]);
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
    /// Collects mutable indexed buffers while one procedural support is generated.
    /// </summary>
    private sealed class SupportMeshAccumulator
    {
        public List<Vector3> Positions { get; } = new List<Vector3>();

        public List<int> TriangleIndices { get; } = new List<int>();

        /// <summary>
        /// Adds one authoritative position and returns its index.
        /// </summary>
        public int AddPosition(Vector3 position)
        {
            int positionIndex = Positions.Count;
            Positions.Add(position);
            return positionIndex;
        }

        /// <summary>
        /// Adds one triangle from existing authoritative position indices.
        /// </summary>
        public void AddTriangle(int firstPositionIndex, int secondPositionIndex, int thirdPositionIndex)
        {
            TriangleIndices.Add(firstPositionIndex);
            TriangleIndices.Add(secondPositionIndex);
            TriangleIndices.Add(thirdPositionIndex);
        }

        /// <summary>
        /// Freezes and validates the completed support mesh.
        /// </summary>
        public SupportMeshData CreateMeshData()
        {
            return new SupportMeshData(Positions, TriangleIndices);
        }
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
