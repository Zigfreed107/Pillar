// BracingPerformanceBenchmarks.cs
// Provides opt-in, non-gating timing coverage for large Brace All workloads.
using Pillar.Commands;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Supports;
using Pillar.Geometry.Supports;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace Pillar.Geometry.SmokeTests;

/// <summary>
/// Runs deterministic bracing timings when explicitly enabled by the developer.
/// </summary>
internal static class BracingPerformanceBenchmarks
{
    private const string EnableEnvironmentVariable = "PILLAR_RUN_BRACING_BENCHMARKS";
    private const float SupportSpacing = 3.0f;
    private const float SupportHeight = 30.0f;

    /// <summary>
    /// Runs non-gating benchmark sizes only when the opt-in environment variable equals one.
    /// </summary>
    public static void RunIfRequested()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnableEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            return;
        }

        int[] supportCounts = { 256, 1024, 4096 };

        for (int i = 0; i < supportCounts.Length; i++)
        {
            RunBraceAllBenchmark(supportCounts[i]);
        }
    }

    /// <summary>
    /// Times target creation, brace planning, document commit, and a headless scene-mesh synchronization proxy.
    /// </summary>
    private static void RunBraceAllBenchmark(int supportCount)
    {
        CadDocument document = new CadDocument();
        MeshEntity mesh = CreateBenchmarkMesh();
        document.AddEntity(mesh);
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(mesh.Id, $"Brace benchmark {supportCount}");
        document.AddSupportLayerGroup(supportLayerGroup);

        long targetStart = Stopwatch.GetTimestamp();
        List<SupportEntity> sourceSupports = CreateGridSupports(supportLayerGroup.Id, supportCount);
        List<Guid> targetIds = new List<Guid>(sourceSupports.Count);

        for (int i = 0; i < sourceSupports.Count; i++)
        {
            targetIds.Add(sourceSupports[i].Id);
        }

        TimeSpan targetElapsed = Stopwatch.GetElapsedTime(targetStart);
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewBrace(
            0,
            SupportBraceModifierSettings.CreateDefault(),
            targetIds,
            supportLayerGroup.SourceGeneratorRevision);

        long plannerStart = Stopwatch.GetTimestamp();
        SupportBracingEvaluationResult evaluation = SupportBracingPlanner.EvaluateBrace(sourceSupports, modifier);
        TimeSpan plannerElapsed = Stopwatch.GetElapsedTime(plannerStart);

        for (int i = 0; i < sourceSupports.Count; i++)
        {
            document.AddEntity(sourceSupports[i]);
        }

        long meshSynchronizationTicks = 0;
        document.EntitiesChanged += (sender, eventArgs) =>
        {
            _ = sender;

            if (eventArgs.NewItems == null)
            {
                return;
            }

            foreach (object? item in eventArgs.NewItems)
            {
                if (item is not SupportEntity support || support.Style.Kind != SupportStyleKind.BraceMember)
                {
                    continue;
                }

                long meshStart = Stopwatch.GetTimestamp();
                _ = SupportMeshBuilder.Build(support, 16);
                meshSynchronizationTicks += Stopwatch.GetTimestamp() - meshStart;
            }
        };
        List<SupportModifierDefinition> newModifiers = new List<SupportModifierDefinition> { modifier };
        ReplaceSupportLayerOutputAndModifiersCommand command = new ReplaceSupportLayerOutputAndModifiersCommand(
            document,
            supportLayerGroup,
            sourceSupports,
            evaluation.SupportEntities,
            Array.Empty<SupportModifierDefinition>(),
            newModifiers,
            "Brace Benchmark");
        long commitStart = Stopwatch.GetTimestamp();
        command.Execute();
        TimeSpan commitElapsed = Stopwatch.GetElapsedTime(commitStart);
        double meshSynchronizationMilliseconds = meshSynchronizationTicks * 1000.0 / Stopwatch.Frequency;

        Console.WriteLine(
            $"BraceAll {supportCount}: targets={targetElapsed.TotalMilliseconds:0.0}ms, "
            + $"planner={plannerElapsed.TotalMilliseconds:0.0}ms, "
            + $"commit+mesh={commitElapsed.TotalMilliseconds:0.0}ms, "
            + $"mesh-sync-proxy={meshSynchronizationMilliseconds:0.0}ms, "
            + $"members={evaluation.AddedMemberCount}");
    }

    /// <summary>
    /// Creates a regular support grid with the requested total population.
    /// </summary>
    private static List<SupportEntity> CreateGridSupports(Guid supportLayerGroupId, int supportCount)
    {
        int columnCount = (int)MathF.Ceiling(MathF.Sqrt(supportCount));
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> result = new List<SupportEntity>(supportCount);

        for (int i = 0; i < supportCount; i++)
        {
            int xIndex = i % columnCount;
            int yIndex = i / columnCount;
            Vector3 basePosition = new Vector3(xIndex * SupportSpacing, yIndex * SupportSpacing, 0.0f);
            Vector3 tipPosition = new Vector3(basePosition.X, basePosition.Y, SupportHeight);
            result.Add(new SupportEntity(supportLayerGroupId, tipPosition, basePosition, profile));
        }

        return result;
    }

    /// <summary>
    /// Creates the minimal owning model required by a support layer group.
    /// </summary>
    private static MeshEntity CreateBenchmarkMesh()
    {
        return new MeshEntity(
            "Brace benchmark model",
            new List<Vector3>
            {
                Vector3.Zero,
                new Vector3(1.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 1.0f, 0.0f)
            },
            new List<int> { 0, 1, 2 },
            new List<Vector3>());
    }
}
