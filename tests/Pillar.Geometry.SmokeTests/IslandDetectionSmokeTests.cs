// IslandDetectionSmokeTests.cs
// Verifies island births, merges, transforms, diagnostics, filtering, and cancellation on deterministic indexed fixtures.
using Pillar.Core.Entities;
using Pillar.Geometry.Analysis;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace Pillar.Geometry.SmokeTests;

/// <summary>
/// Runs focused dependency-free checks for the reusable mesh island analyzer.
/// </summary>
public static class IslandDetectionSmokeTests
{
    /// <summary>
    /// Adds every island detection check to the shared smoke-test run.
    /// </summary>
    public static void Run(List<string> failures)
    {
        RunTest(failures, "Island grounded cube", ValidateGroundedCube);
        RunTest(failures, "Island floating cube", ValidateFloatingCube);
        RunTest(failures, "Island flat birth plateau", ValidateFlatBirthPlateau);
        RunTest(failures, "Island branch merge", ValidateBranchMerge);
        RunTest(failures, "Island simultaneous branch merges", ValidateSimultaneousBranchMerges);
        RunTest(failures, "Island disconnected shells", ValidateDisconnectedShells);
        RunTest(failures, "Island transform snapshot", ValidateTransformSnapshot);
        RunTest(failures, "Island build plate tolerance", ValidateBuildPlateTolerance);
        RunTest(failures, "Island coincident indices", ValidateCoincidentIndices);
        RunTest(failures, "Island degenerate diagnostics", ValidateDegenerateDiagnostics);
        RunTest(failures, "Island non-manifold diagnostics", ValidateNonManifoldDiagnostics);
        RunTest(failures, "Island presentation filter", ValidatePresentationFilter);
        RunTest(failures, "Island topology cancellation", () => ValidateCancellation(IslandDetectionStage.BuildingTopology));
        RunTest(failures, "Island sweep cancellation", () => ValidateCancellation(IslandDetectionStage.SweepingComponents));
    }

    /// <summary>
    /// Confirms plate-connected closed geometry has no above-plate birth.
    /// </summary>
    private static void ValidateGroundedCube()
    {
        IslandDetectionResult result = AnalyzeBox(0.0f);

        if (result.Candidates.Count != 0
            || result.Diagnostics.ValidTriangleCount != 12
            || result.Diagnostics.OpenEdgeCount != 0)
        {
            throw new InvalidOperationException("Expected a clean grounded cube with no candidates.");
        }
    }

    /// <summary>
    /// Confirms a complete shell above the plate remains unmerged and retains all source triangles.
    /// </summary>
    private static void ValidateFloatingCube()
    {
        IslandDetectionResult result = AnalyzeBox(3.0f);

        if (result.Candidates.Count != 1
            || !result.Candidates[0].IsUnmerged
            || MathF.Abs(result.Candidates[0].BirthHeight - 3.0f) > 0.0001f
            || result.Candidates[0].BranchTriangleIndices.Count != 12)
        {
            throw new InvalidOperationException("Expected one floating-shell candidate born at Z=3.");
        }
    }

    /// <summary>
    /// Confirms the four connected bottom vertices form one flat birth plateau.
    /// </summary>
    private static void ValidateFlatBirthPlateau()
    {
        IslandCandidate candidate = AnalyzeBox(4.0f).Candidates[0];

        if (candidate.BirthVertexIndices.Count != 4 || candidate.BirthPositions.Count != 4)
        {
            throw new InvalidOperationException("Expected one four-vertex bottom plateau.");
        }
    }

    /// <summary>
    /// Confirms a younger component closes when a shared high vertex joins grounded geometry.
    /// </summary>
    private static void ValidateBranchMerge()
    {
        Vector3[] positions =
        {
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(3.0f, 0.0f, 2.0f),
            new Vector3(4.0f, 0.0f, 2.0f),
            new Vector3(3.0f, 1.0f, 2.0f),
            new Vector3(2.0f, 0.5f, 5.0f)
        };
        int[] indices = { 0, 1, 2, 3, 4, 5, 0, 1, 6, 3, 4, 6 };
        IslandDetectionResult result = new MeshIslandAnalyzer().Analyze(new MeshEntity("Merge", positions, indices));
        float mergeHeight = result.Candidates.Count > 0 ? result.Candidates[0].MergeHeight ?? float.NaN : float.NaN;

        if (result.Candidates.Count != 1
            || !result.Candidates[0].MergeHeight.HasValue
            || MathF.Abs(result.Candidates[0].BirthHeight - 2.0f) > 0.0001f
            || MathF.Abs(mergeHeight - 5.0f) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the floating branch to merge at Z=5.");
        }
    }

    /// <summary>
    /// Confirms multiple younger branches close deterministically at one shared saddle height.
    /// </summary>
    private static void ValidateSimultaneousBranchMerges()
    {
        Vector3[] positions =
        {
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(3.0f, 0.0f, 2.0f),
            new Vector3(4.0f, 0.0f, 2.0f),
            new Vector3(3.0f, 1.0f, 2.0f),
            new Vector3(-4.0f, 0.0f, 2.0f),
            new Vector3(-3.0f, 0.0f, 2.0f),
            new Vector3(-3.0f, 1.0f, 2.0f),
            new Vector3(0.5f, 0.5f, 5.0f)
        };
        int[] indices =
        {
            0, 1, 2,
            3, 4, 5,
            6, 7, 8,
            0, 1, 9,
            3, 4, 9,
            6, 7, 9
        };
        IslandDetectionResult result = new MeshIslandAnalyzer().Analyze(new MeshEntity("Simultaneous merge", positions, indices));

        if (result.Candidates.Count != 2)
        {
            throw new InvalidOperationException("Expected both floating branches to close at the grounded saddle.");
        }

        for (int candidateIndex = 0; candidateIndex < result.Candidates.Count; candidateIndex++)
        {
            float mergeHeight = result.Candidates[candidateIndex].MergeHeight ?? float.NaN;

            if (MathF.Abs(mergeHeight - 5.0f) > 0.0001f)
            {
                throw new InvalidOperationException("Expected every younger branch to merge at Z=5.");
            }
        }
    }

    /// <summary>
    /// Confirms one grounded and one floating disconnected shell produce only the floating candidate.
    /// </summary>
    private static void ValidateDisconnectedShells()
    {
        Buffers grounded = CreateBoxBuffers(0.0f, 0.0f);
        Buffers floating = CreateBoxBuffers(3.0f, 3.0f);
        IslandDetectionResult result = new MeshIslandAnalyzer().Analyze(Combine(grounded, floating));

        if (result.Candidates.Count != 1 || !result.Candidates[0].IsUnmerged)
        {
            throw new InvalidOperationException("Expected only the floating disconnected shell.");
        }
    }

    /// <summary>
    /// Confirms world-space translation is captured before height classification.
    /// </summary>
    private static void ValidateTransformSnapshot()
    {
        Buffers buffers = CreateBoxBuffers(0.0f, 0.0f);
        MeshEntity mesh = new MeshEntity(
            "Translated",
            buffers.Positions,
            buffers.Indices,
            userTransform: Transform3DData.CreateTranslation(new Vector3(0.0f, 0.0f, 6.0f)));
        IslandDetectionResult result = new MeshIslandAnalyzer().Analyze(mesh);

        if (result.Candidates.Count != 1 || MathF.Abs(result.Candidates[0].BirthHeight - 6.0f) > 0.0001f)
        {
            throw new InvalidOperationException("Expected transformed birth height Z=6.");
        }
    }

    /// <summary>
    /// Confirms the contact boundary is inclusive and a birth above it is an island.
    /// </summary>
    private static void ValidateBuildPlateTolerance()
    {
        IslandDetectionSettings settings = new IslandDetectionSettings(0.001f, 0.0f, 0.01f);
        MeshIslandAnalyzer analyzer = new MeshIslandAnalyzer();

        if (analyzer.Analyze(CreateBox(0.01f, 0.0f), settings).Candidates.Count != 0
            || analyzer.Analyze(CreateBox(0.0102f, 0.0f), settings).Candidates.Count != 1)
        {
            throw new InvalidOperationException("Expected inclusive build-plate contact tolerance.");
        }
    }

    /// <summary>
    /// Confirms exact coordinate duplicates with distinct indices remain disconnected and diagnosed.
    /// </summary>
    private static void ValidateCoincidentIndices()
    {
        Buffers first = CreateBoxBuffers(2.0f, 0.0f);
        Buffers second = CreateBoxBuffers(2.0f, 0.0f);
        IslandDetectionResult result = new MeshIslandAnalyzer().Analyze(Combine(first, second));

        if (result.Candidates.Count != 2 || result.Diagnostics.CoincidentDistinctPositionCount != 8)
        {
            throw new InvalidOperationException("Expected two candidates and eight preserved duplicate indices.");
        }
    }

    /// <summary>
    /// Confirms unusable faces are ignored while their presence remains visible in diagnostics.
    /// </summary>
    private static void ValidateDegenerateDiagnostics()
    {
        Buffers box = CreateBoxBuffers(2.0f, 0.0f);
        int[] indices = new int[box.Indices.Length + 3];
        Array.Copy(box.Indices, indices, box.Indices.Length);
        indices[box.Indices.Length] = 0;
        indices[box.Indices.Length + 1] = 0;
        indices[box.Indices.Length + 2] = 1;
        IslandDetectionResult result = new MeshIslandAnalyzer().Analyze(
            new MeshEntity("Degenerate", box.Positions, indices));

        if (result.Candidates.Count != 1 || result.Diagnostics.DegenerateTriangleCount != 1)
        {
            throw new InvalidOperationException("Expected a meaningful candidate plus one degenerate warning.");
        }
    }

    /// <summary>
    /// Confirms three valid faces sharing one indexed edge are reported as non-manifold.
    /// </summary>
    private static void ValidateNonManifoldDiagnostics()
    {
        Vector3[] positions =
        {
            new Vector3(0.0f, 0.0f, 2.0f),
            new Vector3(1.0f, 0.0f, 2.0f),
            new Vector3(0.5f, 1.0f, 3.0f),
            new Vector3(0.5f, -1.0f, 3.0f),
            new Vector3(0.5f, 0.0f, 4.0f)
        };
        int[] indices =
        {
            0, 1, 2,
            1, 0, 3,
            0, 1, 4
        };
        IslandDetectionResult result = new MeshIslandAnalyzer().Analyze(
            new MeshEntity("Non-manifold", positions, indices));

        if (result.Diagnostics.NonManifoldEdgeCount != 1
            || result.Candidates.Count != 1
            || result.Candidates[0].Confidence != IslandConfidence.Low)
        {
            throw new InvalidOperationException("Expected a low-confidence candidate with one non-manifold edge.");
        }
    }

    /// <summary>
    /// Confirms presentation filters hide without deleting raw analysis candidates.
    /// </summary>
    private static void ValidatePresentationFilter()
    {
        IslandDetectionResult result = AnalyzeBox(2.0f);
        IslandPresentationFilter filter = new IslandPresentationFilter(minimumBranchArea: 1000.0f);

        if (filter.Includes(result.Candidates[0]) || result.Candidates.Count != 1)
        {
            throw new InvalidOperationException("Expected non-destructive presentation filtering.");
        }
    }

    /// <summary>
    /// Confirms synchronous progress callbacks can cancel topology and sweep stages.
    /// </summary>
    private static void ValidateCancellation(IslandDetectionStage stage)
    {
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        CancelAtStageProgress progress = new CancelAtStageProgress(stage, cancellation);

        try
        {
            _ = new MeshIslandAnalyzer().Analyze(
                CreateBox(2.0f, 0.0f),
                cancellationToken: cancellation.Token,
                progress: progress);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected cancellation during {stage}.");
    }

    /// <summary>
    /// Analyzes one standard box at the requested birth height.
    /// </summary>
    private static IslandDetectionResult AnalyzeBox(float minimumZ)
    {
        return new MeshIslandAnalyzer().Analyze(CreateBox(minimumZ, 0.0f));
    }

    /// <summary>
    /// Creates one closed box offset in X for disconnected-shell fixtures.
    /// </summary>
    private static MeshEntity CreateBox(float minimumZ, float minimumX)
    {
        Buffers buffers = CreateBoxBuffers(minimumZ, minimumX);
        return new MeshEntity("Box", buffers.Positions, buffers.Indices);
    }

    /// <summary>
    /// Creates authoritative shared corners and consistently wound box triangles.
    /// </summary>
    private static Buffers CreateBoxBuffers(float minimumZ, float minimumX)
    {
        float maximumX = minimumX + 1.0f;
        float maximumZ = minimumZ + 1.0f;
        Vector3[] positions =
        {
            new Vector3(minimumX, 0.0f, minimumZ),
            new Vector3(maximumX, 0.0f, minimumZ),
            new Vector3(maximumX, 1.0f, minimumZ),
            new Vector3(minimumX, 1.0f, minimumZ),
            new Vector3(minimumX, 0.0f, maximumZ),
            new Vector3(maximumX, 0.0f, maximumZ),
            new Vector3(maximumX, 1.0f, maximumZ),
            new Vector3(minimumX, 1.0f, maximumZ)
        };
        int[] indices =
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4,
            1, 2, 6, 1, 6, 5,
            2, 3, 7, 2, 7, 6,
            3, 0, 4, 3, 4, 7
        };
        return new Buffers(positions, indices);
    }

    /// <summary>
    /// Combines two shells while retaining separate position indices.
    /// </summary>
    private static MeshEntity Combine(Buffers first, Buffers second)
    {
        Vector3[] positions = new Vector3[first.Positions.Length + second.Positions.Length];
        int[] indices = new int[first.Indices.Length + second.Indices.Length];
        Array.Copy(first.Positions, positions, first.Positions.Length);
        Array.Copy(second.Positions, 0, positions, first.Positions.Length, second.Positions.Length);
        Array.Copy(first.Indices, indices, first.Indices.Length);

        for (int index = 0; index < second.Indices.Length; index++)
        {
            indices[first.Indices.Length + index] = second.Indices[index] + first.Positions.Length;
        }

        return new MeshEntity("Combined", positions, indices);
    }

    /// <summary>
    /// Records a failed check while allowing remaining smoke tests to run.
    /// </summary>
    private static void RunTest(List<string> failures, string name, Action test)
    {
        try
        {
            test();
        }
        catch (Exception ex)
        {
            failures.Add($"{name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Stores compact fixture buffers.
    /// </summary>
    private readonly struct Buffers
    {
        public Buffers(Vector3[] positions, int[] indices)
        {
            Positions = positions;
            Indices = indices;
        }

        public Vector3[] Positions { get; }
        public int[] Indices { get; }
    }

    /// <summary>
    /// Cancels immediately when a requested coarse analysis stage is reported.
    /// </summary>
    private sealed class CancelAtStageProgress : IProgress<IslandDetectionProgress>
    {
        private readonly IslandDetectionStage _stage;
        private readonly CancellationTokenSource _cancellation;

        public CancelAtStageProgress(IslandDetectionStage stage, CancellationTokenSource cancellation)
        {
            _stage = stage;
            _cancellation = cancellation;
        }

        /// <summary>
        /// Cancels the shared token at the requested stage.
        /// </summary>
        public void Report(IslandDetectionProgress value)
        {
            if (value.Stage == _stage)
            {
                _cancellation.Cancel();
            }
        }
    }
}
