// MeshIslandAnalyzer.cs
// Detects above-plate mesh components with a transformed indexed lower-star sweep and deterministic elder rule.
using Pillar.Core.Entities;
using Pillar.Core.Geometry;
using Pillar.Geometry.Topology;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;

namespace Pillar.Geometry.Analysis;

/// <summary>
/// Performs reusable renderer-independent island detection on authoritative indexed mesh data.
/// </summary>
public sealed class MeshIslandAnalyzer
{
    private const float DegenerateAreaSquaredTolerance = 0.000000000001f;
    private const int CancellationBatchSize = 256;

    /// <summary>
    /// Analyzes one mesh with its current identity and world-transform snapshot.
    /// </summary>
    public IslandDetectionResult Analyze(
        MeshEntity mesh,
        IslandDetectionSettings? settings = null,
        CancellationToken cancellationToken = default,
        IProgress<IslandDetectionProgress>? progress = null)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        return Analyze(
            mesh.Id,
            mesh.Vertices,
            mesh.TriangleIndices,
            mesh.WorldTransform,
            settings ?? new IslandDetectionSettings(),
            cancellationToken,
            progress);
    }

    /// <summary>
    /// Analyzes explicit immutable buffers so geometry-aware tools can provide their own transform snapshot.
    /// </summary>
    public IslandDetectionResult Analyze(
        Guid sourceModelId,
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<int> triangleIndices,
        Matrix4x4 worldTransform,
        IslandDetectionSettings settings,
        CancellationToken cancellationToken = default,
        IProgress<IslandDetectionProgress>? progress = null)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        IndexedMeshValidator.Validate(positions, triangleIndices);
        ValidateTransform(worldTransform);
        cancellationToken.ThrowIfCancellationRequested();
        Stopwatch totalStopwatch = Stopwatch.StartNew();
        Report(progress, IslandDetectionStage.TransformingVertices, 0.0, "Transforming mesh vertices");
        Vector3[] worldPositions = TransformPositions(positions, worldTransform, cancellationToken);
        Report(progress, IslandDetectionStage.BuildingTopology, 0.15, "Building indexed mesh topology");
        Stopwatch topologyStopwatch = Stopwatch.StartNew();
        AnalysisTopology topology = BuildTopology(
            positions,
            worldPositions,
            triangleIndices,
            cancellationToken);
        topologyStopwatch.Stop();

        if (topology.ValidTriangleCount == 0)
        {
            throw new ArgumentException("The indexed mesh contains no non-degenerate triangles.", nameof(triangleIndices));
        }

        Report(progress, IslandDetectionStage.GroupingPlateaus, 0.45, "Grouping equal-height plateaus");
        PlateauGraph plateauGraph = BuildPlateauGraph(
            worldPositions,
            triangleIndices,
            topology,
            settings.HeightGroupingTolerance,
            cancellationToken);
        Report(progress, IslandDetectionStage.SweepingComponents, 0.60, "Sweeping mesh components upward");
        Stopwatch sweepStopwatch = Stopwatch.StartNew();
        List<CandidateBuildData> candidateData = SweepPlateaus(
            worldPositions,
            triangleIndices,
            topology,
            plateauGraph,
            settings,
            cancellationToken,
            progress);
        sweepStopwatch.Stop();
        Report(progress, IslandDetectionStage.FinalizingResults, 0.95, "Finalizing island metrics");
        List<IslandCandidate> candidates = CreateCandidates(candidateData, topology);
        totalStopwatch.Stop();
        IslandDetectionDiagnostics diagnostics = new IslandDetectionDiagnostics(
            positions.Count,
            triangleIndices.Count / 3,
            topology.ValidTriangleCount,
            topology.EdgeOwners.Count,
            topology.OpenEdgeCount,
            topology.NonManifoldEdgeCount,
            topology.DegenerateTriangleCount,
            topology.CoincidentDistinctPositionCount,
            plateauGraph.Plateaus.Length,
            topologyStopwatch.Elapsed,
            sweepStopwatch.Elapsed,
            totalStopwatch.Elapsed);
        Report(progress, IslandDetectionStage.FinalizingResults, 1.0, "Island detection complete");
        return new IslandDetectionResult(sourceModelId, worldTransform, settings, candidates, diagnostics);
    }

    /// <summary>
    /// Transforms each authoritative position exactly once for the analysis run.
    /// </summary>
    private static Vector3[] TransformPositions(
        IReadOnlyList<Vector3> positions,
        Matrix4x4 worldTransform,
        CancellationToken cancellationToken)
    {
        Vector3[] worldPositions = new Vector3[positions.Count];

        for (int positionIndex = 0; positionIndex < positions.Count; positionIndex++)
        {
            CheckCancellation(positionIndex, cancellationToken);
            Vector3 worldPosition = Vector3.Transform(positions[positionIndex], worldTransform);

            if (!float.IsFinite(worldPosition.X) || !float.IsFinite(worldPosition.Y) || !float.IsFinite(worldPosition.Z))
            {
                throw new ArgumentException($"Transforming position {positionIndex} produced a non-finite coordinate.", nameof(worldTransform));
            }

            worldPositions[positionIndex] = worldPosition;
        }

        return worldPositions;
    }

    /// <summary>
    /// Builds position adjacency, reverse triangle maps, area metrics, and indexed-edge diagnostics.
    /// </summary>
    private static AnalysisTopology BuildTopology(
        IReadOnlyList<Vector3> localPositions,
        IReadOnlyList<Vector3> worldPositions,
        IReadOnlyList<int> triangleIndices,
        CancellationToken cancellationToken)
    {
        int triangleCount = triangleIndices.Count / 3;
        List<int>?[] positionAdjacency = new List<int>?[worldPositions.Count];
        List<int>?[] positionTriangles = new List<int>?[worldPositions.Count];
        Dictionary<IndexedEdgeKey, int> edgeOwners = new Dictionary<IndexedEdgeKey, int>(triangleIndices.Count);
        bool[] validTriangles = new bool[triangleCount];
        float[] triangleAreas = new float[triangleCount];
        float[] downwardFacingAreas = new float[triangleCount];
        bool[] usedPositions = new bool[worldPositions.Count];
        int validTriangleCount = 0;
        int degenerateTriangleCount = 0;

        for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            CheckCancellation(triangleIndex, cancellationToken);
            int baseIndex = triangleIndex * 3;
            int firstIndex = triangleIndices[baseIndex];
            int secondIndex = triangleIndices[baseIndex + 1];
            int thirdIndex = triangleIndices[baseIndex + 2];
            Vector3 first = worldPositions[firstIndex];
            Vector3 second = worldPositions[secondIndex];
            Vector3 third = worldPositions[thirdIndex];
            Vector3 areaVector = Vector3.Cross(second - first, third - first);
            float areaVectorLengthSquared = areaVector.LengthSquared();

            if (firstIndex == secondIndex
                || secondIndex == thirdIndex
                || thirdIndex == firstIndex
                || areaVectorLengthSquared <= DegenerateAreaSquaredTolerance)
            {
                degenerateTriangleCount++;
                continue;
            }

            float areaVectorLength = MathF.Sqrt(areaVectorLengthSquared);
            float area = areaVectorLength * 0.5f;
            validTriangles[triangleIndex] = true;
            triangleAreas[triangleIndex] = area;
            downwardFacingAreas[triangleIndex] = areaVector.Z < 0.0f ? area : 0.0f;
            validTriangleCount++;
            usedPositions[firstIndex] = true;
            usedPositions[secondIndex] = true;
            usedPositions[thirdIndex] = true;
            AddPositionTriangle(positionTriangles, firstIndex, triangleIndex);
            AddPositionTriangle(positionTriangles, secondIndex, triangleIndex);
            AddPositionTriangle(positionTriangles, thirdIndex, triangleIndex);
            AddEdge(positionAdjacency, edgeOwners, firstIndex, secondIndex);
            AddEdge(positionAdjacency, edgeOwners, secondIndex, thirdIndex);
            AddEdge(positionAdjacency, edgeOwners, thirdIndex, firstIndex);
        }

        int openEdgeCount = 0;
        int nonManifoldEdgeCount = 0;

        foreach (KeyValuePair<IndexedEdgeKey, int> edgeEntry in edgeOwners)
        {
            if (edgeEntry.Value == 1)
            {
                openEdgeCount++;
            }
            else if (edgeEntry.Value > 2)
            {
                nonManifoldEdgeCount++;
            }
        }

        int coincidentDistinctPositionCount = CountCoincidentDistinctPositions(localPositions, usedPositions);
        return new AnalysisTopology(
            positionAdjacency,
            positionTriangles,
            edgeOwners,
            validTriangles,
            triangleAreas,
            downwardFacingAreas,
            usedPositions,
            validTriangleCount,
            openEdgeCount,
            nonManifoldEdgeCount,
            degenerateTriangleCount,
            coincidentDistinctPositionCount);
    }

    /// <summary>
    /// Groups connected near-equal vertices into plateaus and builds the plateau adjacency graph.
    /// </summary>
    private static PlateauGraph BuildPlateauGraph(
        IReadOnlyList<Vector3> worldPositions,
        IReadOnlyList<int> triangleIndices,
        AnalysisTopology topology,
        float heightTolerance,
        CancellationToken cancellationToken)
    {
        int[] vertexParents = new int[worldPositions.Count];
        int[] vertexRanks = new int[worldPositions.Count];

        for (int positionIndex = 0; positionIndex < worldPositions.Count; positionIndex++)
        {
            vertexParents[positionIndex] = positionIndex;
        }

        for (int positionIndex = 0; positionIndex < worldPositions.Count; positionIndex++)
        {
            CheckCancellation(positionIndex, cancellationToken);

            if (!topology.UsedPositions[positionIndex])
            {
                continue;
            }

            List<int>? neighbors = topology.PositionAdjacency[positionIndex];

            if (neighbors == null)
            {
                continue;
            }

            for (int neighborIndex = 0; neighborIndex < neighbors.Count; neighborIndex++)
            {
                int neighbor = neighbors[neighborIndex];

                if (neighbor <= positionIndex
                    || MathF.Abs(worldPositions[positionIndex].Z - worldPositions[neighbor].Z) > heightTolerance)
                {
                    continue;
                }

                UnionByRank(vertexParents, vertexRanks, positionIndex, neighbor);
            }
        }

        Dictionary<int, int> plateauIndexByRoot = new Dictionary<int, int>();
        List<MutablePlateau> mutablePlateaus = new List<MutablePlateau>();
        int[] plateauByPosition = new int[worldPositions.Count];
        Array.Fill(plateauByPosition, -1);

        for (int positionIndex = 0; positionIndex < worldPositions.Count; positionIndex++)
        {
            if (!topology.UsedPositions[positionIndex])
            {
                continue;
            }

            int root = FindRoot(vertexParents, positionIndex);

            if (!plateauIndexByRoot.TryGetValue(root, out int plateauIndex))
            {
                plateauIndex = mutablePlateaus.Count;
                plateauIndexByRoot.Add(root, plateauIndex);
                mutablePlateaus.Add(new MutablePlateau());
            }

            plateauByPosition[positionIndex] = plateauIndex;
            mutablePlateaus[plateauIndex].AddVertex(positionIndex, worldPositions[positionIndex]);
        }

        for (int positionIndex = 0; positionIndex < worldPositions.Count; positionIndex++)
        {
            if (!topology.UsedPositions[positionIndex])
            {
                continue;
            }

            int plateauIndex = plateauByPosition[positionIndex];
            List<int>? neighbors = topology.PositionAdjacency[positionIndex];

            if (neighbors == null)
            {
                continue;
            }

            for (int neighborIndex = 0; neighborIndex < neighbors.Count; neighborIndex++)
            {
                int neighborPlateauIndex = plateauByPosition[neighbors[neighborIndex]];

                if (neighborPlateauIndex != plateauIndex)
                {
                    mutablePlateaus[plateauIndex].Neighbors.Add(neighborPlateauIndex);
                }
            }
        }

        for (int triangleIndex = 0; triangleIndex < topology.ValidTriangles.Length; triangleIndex++)
        {
            CheckCancellation(triangleIndex, cancellationToken);

            if (!topology.ValidTriangles[triangleIndex])
            {
                continue;
            }

            int baseIndex = triangleIndex * 3;
            int lowestPositionIndex = triangleIndices[baseIndex];
            int secondPositionIndex = triangleIndices[baseIndex + 1];
            int thirdPositionIndex = triangleIndices[baseIndex + 2];
            lowestPositionIndex = SelectLowerPosition(lowestPositionIndex, secondPositionIndex, worldPositions);
            lowestPositionIndex = SelectLowerPosition(lowestPositionIndex, thirdPositionIndex, worldPositions);
            int startingPlateauIndex = plateauByPosition[lowestPositionIndex];
            mutablePlateaus[startingPlateauIndex].StartingTriangles.Add(triangleIndex);
        }

        Plateau[] plateaus = new Plateau[mutablePlateaus.Count];

        for (int plateauIndex = 0; plateauIndex < mutablePlateaus.Count; plateauIndex++)
        {
            MutablePlateau mutablePlateau = mutablePlateaus[plateauIndex];
            plateaus[plateauIndex] = new Plateau(
                mutablePlateau.Vertices.ToArray(),
                ToSortedArray(mutablePlateau.Neighbors),
                mutablePlateau.StartingTriangles.ToArray(),
                mutablePlateau.MinimumHeight,
                mutablePlateau.StablePosition);
        }

        int[] sweepOrder = new int[plateaus.Length];

        for (int plateauIndex = 0; plateauIndex < plateaus.Length; plateauIndex++)
        {
            sweepOrder[plateauIndex] = plateauIndex;
        }

        Array.Sort(sweepOrder, (int first, int second) => ComparePlateaus(plateaus[first], plateaus[second]));
        return new PlateauGraph(plateaus, plateauByPosition, sweepOrder);
    }

    /// <summary>
    /// Activates plateaus in height order and closes younger branches when components merge.
    /// </summary>
    private static List<CandidateBuildData> SweepPlateaus(
        IReadOnlyList<Vector3> worldPositions,
        IReadOnlyList<int> triangleIndices,
        AnalysisTopology topology,
        PlateauGraph plateauGraph,
        IslandDetectionSettings settings,
        CancellationToken cancellationToken,
        IProgress<IslandDetectionProgress>? progress)
    {
        int plateauCount = plateauGraph.Plateaus.Length;
        int[] componentParents = new int[plateauCount];
        bool[] activePlateaus = new bool[plateauCount];
        BranchRecord?[] branchesByRoot = new BranchRecord?[plateauCount];
        List<CandidateBuildData> candidates = new List<CandidateBuildData>();
        int[] lowerRoots = new int[plateauCount > 0 ? plateauCount : 1];

        for (int plateauIndex = 0; plateauIndex < plateauCount; plateauIndex++)
        {
            componentParents[plateauIndex] = plateauIndex;
        }

        for (int orderIndex = 0; orderIndex < plateauGraph.SweepOrder.Length; orderIndex++)
        {
            CheckCancellation(orderIndex, cancellationToken);
            int plateauIndex = plateauGraph.SweepOrder[orderIndex];
            Plateau plateau = plateauGraph.Plateaus[plateauIndex];
            activePlateaus[plateauIndex] = true;
            int lowerRootCount = 0;

            for (int neighborIndex = 0; neighborIndex < plateau.Neighbors.Length; neighborIndex++)
            {
                int neighborPlateauIndex = plateau.Neighbors[neighborIndex];

                if (!activePlateaus[neighborPlateauIndex])
                {
                    continue;
                }

                int root = FindRoot(componentParents, neighborPlateauIndex);

                if (!Contains(lowerRoots, lowerRootCount, root))
                {
                    lowerRoots[lowerRootCount++] = root;
                }
            }

            BranchRecord survivor;
            int survivorRoot;

            if (lowerRootCount == 0)
            {
                bool isGrounded = plateau.MinimumHeight <= settings.BuildPlateZ + settings.BuildPlateContactTolerance;
                survivor = CreateBranch(plateau, topology, worldPositions, isGrounded);
                survivorRoot = plateauIndex;
                branchesByRoot[survivorRoot] = survivor;
            }
            else
            {
                survivorRoot = SelectElderRoot(lowerRoots, lowerRootCount, branchesByRoot);
                survivor = branchesByRoot[survivorRoot]
                    ?? throw new InvalidOperationException("An active island component did not have branch metadata.");

                for (int rootIndex = 0; rootIndex < lowerRootCount; rootIndex++)
                {
                    int lowerRoot = lowerRoots[rootIndex];

                    if (lowerRoot == survivorRoot)
                    {
                        continue;
                    }

                    BranchRecord youngerBranch = branchesByRoot[lowerRoot]
                        ?? throw new InvalidOperationException("A merging island component did not have branch metadata.");

                    if (!youngerBranch.IsGrounded)
                    {
                        candidates.Add(youngerBranch.Close(plateau.MinimumHeight));
                    }

                    componentParents[lowerRoot] = survivorRoot;
                    branchesByRoot[lowerRoot] = null;
                }

                componentParents[plateauIndex] = survivorRoot;
                branchesByRoot[plateauIndex] = null;
            }

            AttributeTriangles(survivor, plateau.StartingTriangles, worldPositions, triangleIndices, topology);

            if ((orderIndex & (CancellationBatchSize - 1)) == 0)
            {
                double fraction = 0.60 + (0.34 * (orderIndex + 1) / Math.Max(1, plateauCount));
                Report(progress, IslandDetectionStage.SweepingComponents, fraction, "Sweeping mesh components upward");
            }
        }

        HashSet<int> completedRoots = new HashSet<int>();

        for (int plateauIndex = 0; plateauIndex < plateauCount; plateauIndex++)
        {
            if (!activePlateaus[plateauIndex])
            {
                continue;
            }

            int root = FindRoot(componentParents, plateauIndex);

            if (!completedRoots.Add(root))
            {
                continue;
            }

            BranchRecord? branch = branchesByRoot[root];

            if (branch != null && !branch.IsGrounded)
            {
                candidates.Add(branch.Close(null));
            }
        }

        return candidates;
    }

    /// <summary>
    /// Creates immutable sorted candidates and applies conservative diagnostic confidence.
    /// </summary>
    private static List<IslandCandidate> CreateCandidates(
        List<CandidateBuildData> candidateData,
        AnalysisTopology topology)
    {
        candidateData.Sort(CompareCandidateData);
        List<IslandCandidate> candidates = new List<IslandCandidate>(candidateData.Count);
        IslandCandidateDiagnosticFlags globalFlags = IslandCandidateDiagnosticFlags.None;

        if (topology.OpenEdgeCount > 0)
        {
            globalFlags |= IslandCandidateDiagnosticFlags.OpenMeshBoundary;
        }

        if (topology.NonManifoldEdgeCount > 0)
        {
            globalFlags |= IslandCandidateDiagnosticFlags.NonManifoldEdge;
        }

        if (topology.DegenerateTriangleCount > 0)
        {
            globalFlags |= IslandCandidateDiagnosticFlags.DegenerateTriangle;
        }

        if (topology.CoincidentDistinctPositionCount > 0)
        {
            globalFlags |= IslandCandidateDiagnosticFlags.CoincidentDistinctPositions;
        }

        for (int candidateIndex = 0; candidateIndex < candidateData.Count; candidateIndex++)
        {
            CandidateBuildData data = candidateData[candidateIndex];
            IslandCandidateDiagnosticFlags flags = globalFlags;

            if (data.BirthRegionArea <= 0.00000001f)
            {
                flags |= IslandCandidateDiagnosticFlags.ZeroAreaBirthPlateau;
            }

            IslandConfidence confidence = GetConfidence(flags);
            IslandSeverity severity = GetSeverity(data);
            candidates.Add(new IslandCandidate(
                candidateIndex + 1,
                data.BirthHeight,
                data.MergeHeight,
                data.BirthPosition,
                data.BirthPositions,
                data.BirthVertexIndices,
                data.BirthTriangleIndices,
                data.BranchTriangleIndices,
                data.WorldBoundsMin,
                data.WorldBoundsMax,
                data.TotalBranchArea,
                data.DownwardFacingArea,
                severity,
                confidence,
                flags));
        }

        return candidates;
    }

    /// <summary>
    /// Initializes branch metadata at a newly born plateau.
    /// </summary>
    private static BranchRecord CreateBranch(
        Plateau plateau,
        AnalysisTopology topology,
        IReadOnlyList<Vector3> worldPositions,
        bool isGrounded)
    {
        HashSet<int> birthTriangles = new HashSet<int>();
        Vector3 birthPosition = Vector3.Zero;

        for (int vertexIndex = 0; vertexIndex < plateau.Vertices.Length; vertexIndex++)
        {
            int positionIndex = plateau.Vertices[vertexIndex];
            birthPosition += worldPositions[positionIndex];
            List<int>? triangles = topology.PositionTriangles[positionIndex];

            if (triangles == null)
            {
                continue;
            }

            for (int triangleListIndex = 0; triangleListIndex < triangles.Count; triangleListIndex++)
            {
                birthTriangles.Add(triangles[triangleListIndex]);
            }
        }

        birthPosition /= plateau.Vertices.Length;
        int[] sortedBirthTriangles = ToSortedArray(birthTriangles);
        float birthRegionArea = 0.0f;

        for (int triangleListIndex = 0; triangleListIndex < sortedBirthTriangles.Length; triangleListIndex++)
        {
            int triangleIndex = sortedBirthTriangles[triangleListIndex];
            birthRegionArea += topology.TriangleAreas[triangleIndex];
        }

        return new BranchRecord(
            plateau.MinimumHeight,
            plateau.StablePosition,
            birthPosition,
            plateau.Vertices,
            sortedBirthTriangles,
            birthRegionArea,
            worldPositions,
            isGrounded);
    }

    /// <summary>
    /// Attributes original source triangles whose lowest plateau belongs to the current branch.
    /// </summary>
    private static void AttributeTriangles(
        BranchRecord branch,
        IReadOnlyList<int> triangleOrdinals,
        IReadOnlyList<Vector3> worldPositions,
        IReadOnlyList<int> triangleIndices,
        AnalysisTopology topology)
    {
        for (int triangleListIndex = 0; triangleListIndex < triangleOrdinals.Count; triangleListIndex++)
        {
            int triangleIndex = triangleOrdinals[triangleListIndex];
            int baseIndex = triangleIndex * 3;
            branch.AddTriangle(
                triangleIndex,
                worldPositions[triangleIndices[baseIndex]],
                worldPositions[triangleIndices[baseIndex + 1]],
                worldPositions[triangleIndices[baseIndex + 2]],
                topology.TriangleAreas[triangleIndex],
                topology.DownwardFacingAreas[triangleIndex]);
        }
    }

    /// <summary>
    /// Chooses the oldest component with grounded state, birth height, and stable position tie-breakers.
    /// </summary>
    private static int SelectElderRoot(int[] roots, int rootCount, BranchRecord?[] branchesByRoot)
    {
        int elderRoot = roots[0];

        for (int rootIndex = 1; rootIndex < rootCount; rootIndex++)
        {
            int candidateRoot = roots[rootIndex];
            BranchRecord elder = branchesByRoot[elderRoot]
                ?? throw new InvalidOperationException("Elder component metadata was missing.");
            BranchRecord candidate = branchesByRoot[candidateRoot]
                ?? throw new InvalidOperationException("Candidate component metadata was missing.");

            if (CompareBranches(candidate, elder) < 0)
            {
                elderRoot = candidateRoot;
            }
        }

        return elderRoot;
    }

    /// <summary>
    /// Applies the deterministic elder ordering between active branches.
    /// </summary>
    private static int CompareBranches(BranchRecord first, BranchRecord second)
    {
        if (first.IsGrounded != second.IsGrounded)
        {
            return first.IsGrounded ? -1 : 1;
        }

        int heightComparison = first.BirthHeight.CompareTo(second.BirthHeight);

        if (heightComparison != 0)
        {
            return heightComparison;
        }

        return ComparePositions(first.StablePosition, second.StablePosition);
    }

    /// <summary>
    /// Ranks candidates without filtering any raw detection result.
    /// </summary>
    private static int CompareCandidateData(CandidateBuildData first, CandidateBuildData second)
    {
        IslandSeverity firstSeverity = GetSeverity(first);
        IslandSeverity secondSeverity = GetSeverity(second);
        int severityComparison = secondSeverity.CompareTo(firstSeverity);

        if (severityComparison != 0)
        {
            return severityComparison;
        }

        int heightComparison = first.BirthHeight.CompareTo(second.BirthHeight);

        if (heightComparison != 0)
        {
            return heightComparison;
        }

        return ComparePositions(first.StablePosition, second.StablePosition);
    }

    /// <summary>
    /// Calculates a compact initial severity from persistence and attributed area.
    /// </summary>
    private static IslandSeverity GetSeverity(CandidateBuildData data)
    {
        if (!data.MergeHeight.HasValue)
        {
            return IslandSeverity.Critical;
        }

        float persistence = MathF.Max(0.0f, data.MergeHeight.Value - data.BirthHeight);

        if (persistence >= 5.0f || data.DownwardFacingArea >= 25.0f || data.TotalBranchArea >= 100.0f)
        {
            return IslandSeverity.High;
        }

        if (persistence >= 1.0f || data.DownwardFacingArea >= 1.0f || data.TotalBranchArea >= 10.0f)
        {
            return IslandSeverity.Medium;
        }

        return IslandSeverity.Low;
    }

    /// <summary>
    /// Derives confidence conservatively from topology diagnostics.
    /// </summary>
    private static IslandConfidence GetConfidence(IslandCandidateDiagnosticFlags flags)
    {
        if ((flags & (IslandCandidateDiagnosticFlags.NonManifoldEdge
            | IslandCandidateDiagnosticFlags.CoincidentDistinctPositions)) != 0)
        {
            return IslandConfidence.Low;
        }

        if (flags != IslandCandidateDiagnosticFlags.None)
        {
            return IslandConfidence.Medium;
        }

        return IslandConfidence.High;
    }

    /// <summary>
    /// Adds one undirected indexed edge to both graph adjacency and ownership diagnostics.
    /// </summary>
    private static void AddEdge(
        List<int>?[] positionAdjacency,
        Dictionary<IndexedEdgeKey, int> edgeOwners,
        int firstPositionIndex,
        int secondPositionIndex)
    {
        if (firstPositionIndex == secondPositionIndex)
        {
            return;
        }

        AddUniqueNeighbor(positionAdjacency, firstPositionIndex, secondPositionIndex);
        AddUniqueNeighbor(positionAdjacency, secondPositionIndex, firstPositionIndex);
        IndexedEdgeKey edgeKey = new IndexedEdgeKey(firstPositionIndex, secondPositionIndex);
        edgeOwners.TryGetValue(edgeKey, out int ownerCount);
        edgeOwners[edgeKey] = ownerCount + 1;
    }

    /// <summary>
    /// Adds one unique position neighbor.
    /// </summary>
    private static void AddUniqueNeighbor(List<int>?[] adjacency, int positionIndex, int neighborPositionIndex)
    {
        List<int>? neighbors = adjacency[positionIndex];

        if (neighbors == null)
        {
            neighbors = new List<int>(6);
            adjacency[positionIndex] = neighbors;
        }

        if (!neighbors.Contains(neighborPositionIndex))
        {
            neighbors.Add(neighborPositionIndex);
        }
    }

    /// <summary>
    /// Adds one triangle ordinal to a position-to-triangle reverse map.
    /// </summary>
    private static void AddPositionTriangle(List<int>?[] positionTriangles, int positionIndex, int triangleIndex)
    {
        List<int>? triangles = positionTriangles[positionIndex];

        if (triangles == null)
        {
            triangles = new List<int>(6);
            positionTriangles[positionIndex] = triangles;
        }

        if (!triangles.Contains(triangleIndex))
        {
            triangles.Add(triangleIndex);
        }
    }

    /// <summary>
    /// Counts exact duplicate coordinates that retain separate authoritative position indices.
    /// </summary>
    private static int CountCoincidentDistinctPositions(IReadOnlyList<Vector3> positions, IReadOnlyList<bool> usedPositions)
    {
        Dictionary<Vector3, int> firstIndexByPosition = new Dictionary<Vector3, int>();
        int duplicateCount = 0;

        for (int positionIndex = 0; positionIndex < positions.Count; positionIndex++)
        {
            if (!usedPositions[positionIndex])
            {
                continue;
            }

            if (firstIndexByPosition.ContainsKey(positions[positionIndex]))
            {
                duplicateCount++;
            }
            else
            {
                firstIndexByPosition.Add(positions[positionIndex], positionIndex);
            }
        }

        return duplicateCount;
    }

    /// <summary>
    /// Selects the lower vertex with a lexicographic tie-breaker for deterministic attribution.
    /// </summary>
    private static int SelectLowerPosition(int firstIndex, int secondIndex, IReadOnlyList<Vector3> positions)
    {
        int heightComparison = positions[firstIndex].Z.CompareTo(positions[secondIndex].Z);

        if (heightComparison < 0)
        {
            return firstIndex;
        }

        if (heightComparison > 0)
        {
            return secondIndex;
        }

        return ComparePositions(positions[firstIndex], positions[secondIndex]) <= 0 ? firstIndex : secondIndex;
    }

    /// <summary>
    /// Orders plateaus by sweep height and coordinate-stable identity.
    /// </summary>
    private static int ComparePlateaus(Plateau first, Plateau second)
    {
        int heightComparison = first.MinimumHeight.CompareTo(second.MinimumHeight);
        return heightComparison != 0 ? heightComparison : ComparePositions(first.StablePosition, second.StablePosition);
    }

    /// <summary>
    /// Orders positions lexicographically for index-order-independent tie breaking.
    /// </summary>
    private static int ComparePositions(Vector3 first, Vector3 second)
    {
        int xComparison = first.X.CompareTo(second.X);

        if (xComparison != 0)
        {
            return xComparison;
        }

        int yComparison = first.Y.CompareTo(second.Y);

        if (yComparison != 0)
        {
            return yComparison;
        }

        return first.Z.CompareTo(second.Z);
    }

    /// <summary>
    /// Checks whether a small root buffer already contains a component.
    /// </summary>
    private static bool Contains(int[] values, int count, int value)
    {
        for (int index = 0; index < count; index++)
        {
            if (values[index] == value)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Performs one rank-based union for plateau grouping.
    /// </summary>
    private static void UnionByRank(int[] parents, int[] ranks, int first, int second)
    {
        int firstRoot = FindRoot(parents, first);
        int secondRoot = FindRoot(parents, second);

        if (firstRoot == secondRoot)
        {
            return;
        }

        if (ranks[firstRoot] < ranks[secondRoot])
        {
            parents[firstRoot] = secondRoot;
        }
        else if (ranks[firstRoot] > ranks[secondRoot])
        {
            parents[secondRoot] = firstRoot;
        }
        else
        {
            parents[secondRoot] = firstRoot;
            ranks[firstRoot]++;
        }
    }

    /// <summary>
    /// Finds and compresses one union-find root.
    /// </summary>
    private static int FindRoot(int[] parents, int value)
    {
        int root = value;

        while (parents[root] != root)
        {
            root = parents[root];
        }

        while (parents[value] != value)
        {
            int next = parents[value];
            parents[value] = root;
            value = next;
        }

        return root;
    }

    /// <summary>
    /// Converts one integer set to deterministic sorted storage.
    /// </summary>
    private static int[] ToSortedArray(HashSet<int> values)
    {
        int[] result = new int[values.Count];
        values.CopyTo(result);
        Array.Sort(result);
        return result;
    }

    /// <summary>
    /// Validates that all transform components are finite before the snapshot is used.
    /// </summary>
    private static void ValidateTransform(Matrix4x4 transform)
    {
        if (!float.IsFinite(transform.M11) || !float.IsFinite(transform.M12) || !float.IsFinite(transform.M13) || !float.IsFinite(transform.M14)
            || !float.IsFinite(transform.M21) || !float.IsFinite(transform.M22) || !float.IsFinite(transform.M23) || !float.IsFinite(transform.M24)
            || !float.IsFinite(transform.M31) || !float.IsFinite(transform.M32) || !float.IsFinite(transform.M33) || !float.IsFinite(transform.M34)
            || !float.IsFinite(transform.M41) || !float.IsFinite(transform.M42) || !float.IsFinite(transform.M43) || !float.IsFinite(transform.M44))
        {
            throw new ArgumentException("The world transform must contain only finite values.", nameof(transform));
        }
    }

    /// <summary>
    /// Checks cancellation at coarse batches to keep large-mesh analysis responsive.
    /// </summary>
    private static void CheckCancellation(int index, CancellationToken cancellationToken)
    {
        if ((index & (CancellationBatchSize - 1)) == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// Reports one optional coarse progress update.
    /// </summary>
    private static void Report(
        IProgress<IslandDetectionProgress>? progress,
        IslandDetectionStage stage,
        double fraction,
        string message)
    {
        progress?.Report(new IslandDetectionProgress(stage, fraction, message));
    }

    /// <summary>
    /// Stores topology arrays used only during one analysis run.
    /// </summary>
    private sealed class AnalysisTopology
    {
        public AnalysisTopology(
            List<int>?[] positionAdjacency,
            List<int>?[] positionTriangles,
            Dictionary<IndexedEdgeKey, int> edgeOwners,
            bool[] validTriangles,
            float[] triangleAreas,
            float[] downwardFacingAreas,
            bool[] usedPositions,
            int validTriangleCount,
            int openEdgeCount,
            int nonManifoldEdgeCount,
            int degenerateTriangleCount,
            int coincidentDistinctPositionCount)
        {
            PositionAdjacency = positionAdjacency;
            PositionTriangles = positionTriangles;
            EdgeOwners = edgeOwners;
            ValidTriangles = validTriangles;
            TriangleAreas = triangleAreas;
            DownwardFacingAreas = downwardFacingAreas;
            UsedPositions = usedPositions;
            ValidTriangleCount = validTriangleCount;
            OpenEdgeCount = openEdgeCount;
            NonManifoldEdgeCount = nonManifoldEdgeCount;
            DegenerateTriangleCount = degenerateTriangleCount;
            CoincidentDistinctPositionCount = coincidentDistinctPositionCount;
        }

        public List<int>?[] PositionAdjacency { get; }
        public List<int>?[] PositionTriangles { get; }
        public Dictionary<IndexedEdgeKey, int> EdgeOwners { get; }
        public bool[] ValidTriangles { get; }
        public float[] TriangleAreas { get; }
        public float[] DownwardFacingAreas { get; }
        public bool[] UsedPositions { get; }
        public int ValidTriangleCount { get; }
        public int OpenEdgeCount { get; }
        public int NonManifoldEdgeCount { get; }
        public int DegenerateTriangleCount { get; }
        public int CoincidentDistinctPositionCount { get; }
    }

    /// <summary>
    /// Accumulates one plateau before its immutable graph node is created.
    /// </summary>
    private sealed class MutablePlateau
    {
        public List<int> Vertices { get; } = new List<int>();
        public HashSet<int> Neighbors { get; } = new HashSet<int>();
        public List<int> StartingTriangles { get; } = new List<int>();
        public float MinimumHeight { get; private set; } = float.PositiveInfinity;
        public Vector3 StablePosition { get; private set; } = new Vector3(float.PositiveInfinity);

        /// <summary>
        /// Adds one authoritative position while updating deterministic plateau identity.
        /// </summary>
        public void AddVertex(int positionIndex, Vector3 position)
        {
            Vertices.Add(positionIndex);
            MinimumHeight = MathF.Min(MinimumHeight, position.Z);

            if (ComparePositions(position, StablePosition) < 0)
            {
                StablePosition = position;
            }
        }
    }

    /// <summary>
    /// Stores one immutable plateau graph node.
    /// </summary>
    private sealed class Plateau
    {
        public Plateau(int[] vertices, int[] neighbors, int[] startingTriangles, float minimumHeight, Vector3 stablePosition)
        {
            Vertices = vertices;
            Neighbors = neighbors;
            StartingTriangles = startingTriangles;
            MinimumHeight = minimumHeight;
            StablePosition = stablePosition;
        }

        public int[] Vertices { get; }
        public int[] Neighbors { get; }
        public int[] StartingTriangles { get; }
        public float MinimumHeight { get; }
        public Vector3 StablePosition { get; }
    }

    /// <summary>
    /// Stores the complete plateau graph and deterministic activation order.
    /// </summary>
    private sealed class PlateauGraph
    {
        public PlateauGraph(Plateau[] plateaus, int[] plateauByPosition, int[] sweepOrder)
        {
            Plateaus = plateaus;
            PlateauByPosition = plateauByPosition;
            SweepOrder = sweepOrder;
        }

        public Plateau[] Plateaus { get; }
        public int[] PlateauByPosition { get; }
        public int[] SweepOrder { get; }
    }

    /// <summary>
    /// Accumulates renderer-independent geometry for one living sweep branch.
    /// </summary>
    private sealed class BranchRecord
    {
        private readonly List<int> _branchTriangleIndices = new List<int>();
        private Vector3 _worldBoundsMin;
        private Vector3 _worldBoundsMax;

        public BranchRecord(
            float birthHeight,
            Vector3 stablePosition,
            Vector3 birthPosition,
            int[] birthVertexIndices,
            int[] birthTriangleIndices,
            float birthRegionArea,
            IReadOnlyList<Vector3> worldPositions,
            bool isGrounded)
        {
            BirthHeight = birthHeight;
            StablePosition = stablePosition;
            BirthPosition = birthPosition;
            BirthVertexIndices = (int[])birthVertexIndices.Clone();
            BirthTriangleIndices = (int[])birthTriangleIndices.Clone();
            BirthRegionArea = birthRegionArea;
            IsGrounded = isGrounded;
            BirthPositions = new Vector3[birthVertexIndices.Length];
            _worldBoundsMin = worldPositions[birthVertexIndices[0]];
            _worldBoundsMax = _worldBoundsMin;

            for (int vertexIndex = 0; vertexIndex < birthVertexIndices.Length; vertexIndex++)
            {
                Vector3 position = worldPositions[birthVertexIndices[vertexIndex]];
                BirthPositions[vertexIndex] = position;
                IncludePosition(position);
            }
        }

        public float BirthHeight { get; }
        public Vector3 StablePosition { get; }
        public Vector3 BirthPosition { get; }
        public Vector3[] BirthPositions { get; }
        public int[] BirthVertexIndices { get; }
        public int[] BirthTriangleIndices { get; }
        public float BirthRegionArea { get; }
        public bool IsGrounded { get; }
        public float TotalBranchArea { get; private set; }
        public float DownwardFacingArea { get; private set; }

        /// <summary>
        /// Adds one original source triangle to this branch's reusable result geometry.
        /// </summary>
        public void AddTriangle(int triangleIndex, Vector3 first, Vector3 second, Vector3 third, float area, float downwardArea)
        {
            _branchTriangleIndices.Add(triangleIndex);
            TotalBranchArea += area;
            DownwardFacingArea += downwardArea;
            IncludePosition(first);
            IncludePosition(second);
            IncludePosition(third);
        }

        /// <summary>
        /// Captures the finished branch at a finite merge or as an unmerged shell.
        /// </summary>
        public CandidateBuildData Close(float? mergeHeight)
        {
            int[] branchTriangles = _branchTriangleIndices.ToArray();
            Array.Sort(branchTriangles);
            return new CandidateBuildData(
                BirthHeight,
                mergeHeight,
                StablePosition,
                BirthPosition,
                BirthPositions,
                BirthVertexIndices,
                BirthTriangleIndices,
                branchTriangles,
                _worldBoundsMin,
                _worldBoundsMax,
                TotalBranchArea,
                DownwardFacingArea,
                BirthRegionArea);
        }

        /// <summary>
        /// Expands branch bounds by one world-space point.
        /// </summary>
        private void IncludePosition(Vector3 position)
        {
            _worldBoundsMin = Vector3.Min(_worldBoundsMin, position);
            _worldBoundsMax = Vector3.Max(_worldBoundsMax, position);
        }
    }

    /// <summary>
    /// Stores finalized branch data before deterministic ranking assigns candidate ids.
    /// </summary>
    private sealed class CandidateBuildData
    {
        public CandidateBuildData(
            float birthHeight,
            float? mergeHeight,
            Vector3 stablePosition,
            Vector3 birthPosition,
            Vector3[] birthPositions,
            int[] birthVertexIndices,
            int[] birthTriangleIndices,
            int[] branchTriangleIndices,
            Vector3 worldBoundsMin,
            Vector3 worldBoundsMax,
            float totalBranchArea,
            float downwardFacingArea,
            float birthRegionArea)
        {
            BirthHeight = birthHeight;
            MergeHeight = mergeHeight;
            StablePosition = stablePosition;
            BirthPosition = birthPosition;
            BirthPositions = birthPositions;
            BirthVertexIndices = birthVertexIndices;
            BirthTriangleIndices = birthTriangleIndices;
            BranchTriangleIndices = branchTriangleIndices;
            WorldBoundsMin = worldBoundsMin;
            WorldBoundsMax = worldBoundsMax;
            TotalBranchArea = totalBranchArea;
            DownwardFacingArea = downwardFacingArea;
            BirthRegionArea = birthRegionArea;
        }

        public float BirthHeight { get; }
        public float? MergeHeight { get; }
        public Vector3 StablePosition { get; }
        public Vector3 BirthPosition { get; }
        public Vector3[] BirthPositions { get; }
        public int[] BirthVertexIndices { get; }
        public int[] BirthTriangleIndices { get; }
        public int[] BranchTriangleIndices { get; }
        public Vector3 WorldBoundsMin { get; }
        public Vector3 WorldBoundsMax { get; }
        public float TotalBranchArea { get; }
        public float DownwardFacingArea { get; }
        public float BirthRegionArea { get; }
    }
}
