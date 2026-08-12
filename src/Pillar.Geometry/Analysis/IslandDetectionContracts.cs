// IslandDetectionContracts.cs
// Defines renderer-independent settings, progress, diagnostics, candidate, and result data for mesh island analysis.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Pillar.Geometry.Analysis;

/// <summary>
/// Identifies the coarse stages reported by the island analyzer.
/// </summary>
public enum IslandDetectionStage
{
    TransformingVertices,
    BuildingTopology,
    GroupingPlateaus,
    SweepingComponents,
    FinalizingResults
}

/// <summary>
/// Describes coarse analysis progress without coupling callers to implementation details.
/// </summary>
public readonly struct IslandDetectionProgress
{
    /// <summary>
    /// Creates one progress snapshot.
    /// </summary>
    public IslandDetectionProgress(IslandDetectionStage stage, double fraction, string message)
    {
        Stage = stage;
        Fraction = Math.Clamp(fraction, 0.0, 1.0);
        Message = message ?? string.Empty;
    }

    public IslandDetectionStage Stage { get; }

    public double Fraction { get; }

    public string Message { get; }
}

/// <summary>
/// Stores topology settings whose changes require a new analysis.
/// </summary>
public sealed class IslandDetectionSettings : IEquatable<IslandDetectionSettings>
{
    public const float DefaultHeightGroupingTolerance = 0.001f;
    public const float DefaultBuildPlateZ = 0.0f;
    public const float DefaultBuildPlateContactTolerance = 0.01f;

    /// <summary>
    /// Creates validated topology settings in world-space millimetres.
    /// </summary>
    public IslandDetectionSettings(
        float heightGroupingTolerance = DefaultHeightGroupingTolerance,
        float buildPlateZ = DefaultBuildPlateZ,
        float buildPlateContactTolerance = DefaultBuildPlateContactTolerance)
    {
        if (!float.IsFinite(heightGroupingTolerance) || heightGroupingTolerance < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(heightGroupingTolerance));
        }

        if (!float.IsFinite(buildPlateZ))
        {
            throw new ArgumentOutOfRangeException(nameof(buildPlateZ));
        }

        if (!float.IsFinite(buildPlateContactTolerance) || buildPlateContactTolerance < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(buildPlateContactTolerance));
        }

        HeightGroupingTolerance = heightGroupingTolerance;
        BuildPlateZ = buildPlateZ;
        BuildPlateContactTolerance = buildPlateContactTolerance;
    }

    public float HeightGroupingTolerance { get; }

    public float BuildPlateZ { get; }

    public float BuildPlateContactTolerance { get; }

    /// <summary>
    /// Checks whether two settings snapshots require the same topology analysis.
    /// </summary>
    public bool Equals(IslandDetectionSettings? other)
    {
        return other != null
            && HeightGroupingTolerance.Equals(other.HeightGroupingTolerance)
            && BuildPlateZ.Equals(other.BuildPlateZ)
            && BuildPlateContactTolerance.Equals(other.BuildPlateContactTolerance);
    }

    /// <summary>
    /// Checks whether an object is an equivalent settings snapshot.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is IslandDetectionSettings other && Equals(other);
    }

    /// <summary>
    /// Creates a stable hash for cache validation.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(HeightGroupingTolerance, BuildPlateZ, BuildPlateContactTolerance);
    }
}

/// <summary>
/// Flags mesh-quality conditions that can reduce confidence in one candidate.
/// </summary>
[Flags]
public enum IslandCandidateDiagnosticFlags
{
    None = 0,
    OpenMeshBoundary = 1,
    NonManifoldEdge = 2,
    DegenerateTriangle = 4,
    CoincidentDistinctPositions = 8,
    ZeroAreaBirthPlateau = 16
}

/// <summary>
/// Provides a compact, presentation-neutral candidate ranking.
/// </summary>
public enum IslandSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Communicates how strongly mesh diagnostics affect a candidate.
/// </summary>
public enum IslandConfidence
{
    High,
    Medium,
    Low
}

/// <summary>
/// Records mesh and timing diagnostics for one analysis run.
/// </summary>
public sealed class IslandDetectionDiagnostics
{
    /// <summary>
    /// Creates one immutable diagnostics snapshot.
    /// </summary>
    public IslandDetectionDiagnostics(
        int positionCount,
        int triangleCount,
        int validTriangleCount,
        int edgeCount,
        int openEdgeCount,
        int nonManifoldEdgeCount,
        int degenerateTriangleCount,
        int coincidentDistinctPositionCount,
        int plateauCount,
        TimeSpan topologyBuildTime,
        TimeSpan sweepTime,
        TimeSpan totalTime)
    {
        PositionCount = positionCount;
        TriangleCount = triangleCount;
        ValidTriangleCount = validTriangleCount;
        EdgeCount = edgeCount;
        OpenEdgeCount = openEdgeCount;
        NonManifoldEdgeCount = nonManifoldEdgeCount;
        DegenerateTriangleCount = degenerateTriangleCount;
        CoincidentDistinctPositionCount = coincidentDistinctPositionCount;
        PlateauCount = plateauCount;
        TopologyBuildTime = topologyBuildTime;
        SweepTime = sweepTime;
        TotalTime = totalTime;
    }

    public int PositionCount { get; }

    public int TriangleCount { get; }

    public int ValidTriangleCount { get; }

    public int EdgeCount { get; }

    public int OpenEdgeCount { get; }

    public int NonManifoldEdgeCount { get; }

    public int DegenerateTriangleCount { get; }

    public int CoincidentDistinctPositionCount { get; }

    public int PlateauCount { get; }

    public TimeSpan TopologyBuildTime { get; }

    public TimeSpan SweepTime { get; }

    public TimeSpan TotalTime { get; }

    public bool HasMeshQualityWarnings
    {
        get
        {
            return OpenEdgeCount > 0
                || NonManifoldEdgeCount > 0
                || DegenerateTriangleCount > 0
                || CoincidentDistinctPositionCount > 0;
        }
    }
}

/// <summary>
/// Describes one above-plate component from its birth until its merge or the end of the mesh.
/// </summary>
public sealed class IslandCandidate
{
    /// <summary>
    /// Creates one immutable island candidate.
    /// </summary>
    internal IslandCandidate(
        int candidateId,
        float birthHeight,
        float? mergeHeight,
        Vector3 birthPosition,
        IReadOnlyList<Vector3> birthPositions,
        IReadOnlyList<int> birthVertexIndices,
        IReadOnlyList<int> birthTriangleIndices,
        IReadOnlyList<int> branchTriangleIndices,
        Vector3 worldBoundsMin,
        Vector3 worldBoundsMax,
        float totalBranchArea,
        float downwardFacingArea,
        IslandSeverity severity,
        IslandConfidence confidence,
        IslandCandidateDiagnosticFlags diagnosticFlags)
    {
        CandidateId = candidateId;
        BirthHeight = birthHeight;
        MergeHeight = mergeHeight;
        BirthPosition = birthPosition;
        BirthPositions = Copy(birthPositions);
        BirthVertexIndices = Copy(birthVertexIndices);
        BirthTriangleIndices = Copy(birthTriangleIndices);
        BranchTriangleIndices = Copy(branchTriangleIndices);
        WorldBoundsMin = worldBoundsMin;
        WorldBoundsMax = worldBoundsMax;
        TotalBranchArea = totalBranchArea;
        DownwardFacingArea = downwardFacingArea;
        Severity = severity;
        Confidence = confidence;
        DiagnosticFlags = diagnosticFlags;
    }

    public int CandidateId { get; }

    public float BirthHeight { get; }

    public float? MergeHeight { get; }

    public bool IsMerged
    {
        get { return MergeHeight.HasValue; }
    }

    public bool IsUnmerged
    {
        get { return !MergeHeight.HasValue; }
    }

    public float? PersistenceHeight
    {
        get { return MergeHeight.HasValue ? MathF.Max(0.0f, MergeHeight.Value - BirthHeight) : null; }
    }

    public Vector3 BirthPosition { get; }

    public IReadOnlyList<Vector3> BirthPositions { get; }

    public IReadOnlyList<int> BirthVertexIndices { get; }

    public IReadOnlyList<int> BirthTriangleIndices { get; }

    public IReadOnlyList<int> BranchTriangleIndices { get; }

    public Vector3 WorldBoundsMin { get; }

    public Vector3 WorldBoundsMax { get; }

    public float TotalBranchArea { get; }

    public float DownwardFacingArea { get; }

    public IslandSeverity Severity { get; }

    public IslandConfidence Confidence { get; }

    public IslandCandidateDiagnosticFlags DiagnosticFlags { get; }

    /// <summary>
    /// Copies a public collection so result data cannot be changed by callers.
    /// </summary>
    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
    {
        T[] copy = new T[source.Count];

        for (int i = 0; i < source.Count; i++)
        {
            copy[i] = source[i];
        }

        return new ReadOnlyCollection<T>(copy);
    }
}

/// <summary>
/// Holds a complete transient analysis snapshot for one model and transform.
/// </summary>
public sealed class IslandDetectionResult
{
    /// <summary>
    /// Creates one immutable result with its unfiltered candidate collection.
    /// </summary>
    internal IslandDetectionResult(
        Guid sourceModelId,
        Matrix4x4 transformSnapshot,
        IslandDetectionSettings settings,
        IReadOnlyList<IslandCandidate> candidates,
        IslandDetectionDiagnostics diagnostics)
    {
        SourceModelId = sourceModelId;
        TransformSnapshot = transformSnapshot;
        Settings = settings;
        IslandCandidate[] candidateCopy = new IslandCandidate[candidates.Count];

        for (int i = 0; i < candidates.Count; i++)
        {
            candidateCopy[i] = candidates[i];
        }

        Candidates = new ReadOnlyCollection<IslandCandidate>(candidateCopy);
        Diagnostics = diagnostics;
    }

    public Guid SourceModelId { get; }

    public Matrix4x4 TransformSnapshot { get; }

    public IslandDetectionSettings Settings { get; }

    public IReadOnlyList<IslandCandidate> Candidates { get; }

    public IslandDetectionDiagnostics Diagnostics { get; }
}

/// <summary>
/// Stores presentation-only filters that never require topology analysis.
/// </summary>
public sealed class IslandPresentationFilter
{
    /// <summary>
    /// Creates a validated filter snapshot.
    /// </summary>
    public IslandPresentationFilter(
        float minimumPersistenceHeight = 0.0f,
        float minimumBranchArea = 0.0f,
        float minimumDownwardFacingArea = 0.0f,
        bool showLowConfidenceCandidates = true)
    {
        MinimumPersistenceHeight = ValidateMinimum(minimumPersistenceHeight, nameof(minimumPersistenceHeight));
        MinimumBranchArea = ValidateMinimum(minimumBranchArea, nameof(minimumBranchArea));
        MinimumDownwardFacingArea = ValidateMinimum(minimumDownwardFacingArea, nameof(minimumDownwardFacingArea));
        ShowLowConfidenceCandidates = showLowConfidenceCandidates;
    }

    public float MinimumPersistenceHeight { get; }

    public float MinimumBranchArea { get; }

    public float MinimumDownwardFacingArea { get; }

    public bool ShowLowConfidenceCandidates { get; }

    /// <summary>
    /// Tests one candidate without changing the raw result.
    /// </summary>
    public bool Includes(IslandCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        float persistence = candidate.PersistenceHeight ?? float.PositiveInfinity;
        return persistence >= MinimumPersistenceHeight
            && candidate.TotalBranchArea >= MinimumBranchArea
            && candidate.DownwardFacingArea >= MinimumDownwardFacingArea
            && (ShowLowConfidenceCandidates || candidate.Confidence != IslandConfidence.Low);
    }

    /// <summary>
    /// Validates a non-negative finite filter threshold.
    /// </summary>
    private static float ValidateMinimum(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value < 0.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return value;
    }
}
