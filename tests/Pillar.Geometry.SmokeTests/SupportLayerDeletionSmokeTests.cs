// SupportLayerDeletionSmokeTests.cs
// Verifies support-layer deletion batching, undo restoration, deferred layer-tree refresh, and cached mesh bounds.
using Pillar.Commands;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Supports;
using Pillar.ViewModels;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Geometry.SmokeTests;

/// <summary>
/// Provides focused regression coverage for support-layer deletion performance contracts.
/// </summary>
internal static class SupportLayerDeletionSmokeTests
{
    private const int SupportCount = 24;

    /// <summary>
    /// Runs the support-layer deletion test group and appends readable failures.
    /// </summary>
    public static void Run(List<string> failures)
    {
        Run(failures, "Support-layer removal and undo each complete one entity batch", ValidateRemovalAndUndoBatching);
        Run(failures, "Mesh local bounds remain cached while world transforms stay live", ValidateCachedMeshBounds);
    }

    /// <summary>
    /// Verifies bulk support removal stays incremental for renderers but refreshes aggregate observers once.
    /// </summary>
    private static void ValidateRemovalAndUndoBatching()
    {
        CadDocument document = new CadDocument();
        MeshEntity mesh = CreateMesh();
        document.AddEntity(mesh);
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(mesh.Id, "Deletion batching");
        document.AddSupportLayerGroup(supportLayerGroup);
        SupportProfile profile = SupportDefaults.CreateProfile();

        for (int i = 0; i < SupportCount; i++)
        {
            Vector3 basePosition = new Vector3(i * 2.0f, 0.0f, 0.0f);
            Vector3 tipPosition = new Vector3(basePosition.X, 0.0f, 10.0f);
            document.AddEntity(new SupportEntity(supportLayerGroup.Id, tipPosition, basePosition, profile));
        }

        LayerPanelViewModel layerPanel = new LayerPanelViewModel(document);
        RemoveSupportLayerGroupCommand command = new RemoveSupportLayerGroupCommand(document, supportLayerGroup);
        int entityChangeCount = 0;
        int entityChangesOutsideBatch = 0;
        int batchCompletionCount = 0;
        int layerTreeChangeCount = 0;

        document.EntitiesChanged += (sender, eventArgs) =>
        {
            _ = sender;
            entityChangeCount += (eventArgs.NewItems?.Count ?? 0) + (eventArgs.OldItems?.Count ?? 0);

            if (!document.IsEntityBatchUpdateActive)
            {
                entityChangesOutsideBatch++;
            }
        };
        document.EntityBatchUpdateCompleted += (sender, eventArgs) =>
        {
            _ = sender;
            _ = eventArgs;
            batchCompletionCount++;
        };
        layerPanel.ModelLayers.CollectionChanged += (sender, eventArgs) =>
        {
            _ = sender;
            _ = eventArgs;
            layerTreeChangeCount++;
        };

        command.Execute();

        Require(entityChangeCount == SupportCount, "Removal did not publish one incremental event per support.");
        Require(entityChangesOutsideBatch == 0, "Removal published support changes outside its entity batch.");
        Require(batchCompletionCount == 1, "Removal did not complete exactly one entity batch.");
        Require(layerTreeChangeCount == 2, "Removal rebuilt the model-layer collection more than once.");
        Require(document.FindSupportLayerGroupById(supportLayerGroup.Id) == null, "Removal retained the support group.");
        Require(document.GetSupportEntitiesForGroup(supportLayerGroup.Id).Count == 0, "Removal retained support entities.");
        Require(layerPanel.ModelLayers.Count == 1 && layerPanel.ModelLayers[0].Children.Count == 0, "Layer Panel retained the removed support group.");

        entityChangeCount = 0;
        entityChangesOutsideBatch = 0;
        batchCompletionCount = 0;
        layerTreeChangeCount = 0;
        command.Undo();

        Require(entityChangeCount == SupportCount, "Undo did not publish one incremental event per restored support.");
        Require(entityChangesOutsideBatch == 0, "Undo published support changes outside its entity batch.");
        Require(batchCompletionCount == 1, "Undo did not complete exactly one entity batch.");
        Require(layerTreeChangeCount == 2, "Undo rebuilt the model-layer collection more than once.");
        Require(document.FindSupportLayerGroupById(supportLayerGroup.Id) == supportLayerGroup, "Undo did not restore the support group.");
        Require(document.GetSupportEntitiesForGroup(supportLayerGroup.Id).Count == SupportCount, "Undo did not restore every support entity.");
        Require(layerPanel.ModelLayers.Count == 1 && layerPanel.ModelLayers[0].Children.Count == 1, "Layer Panel did not restore the support group.");
    }

    /// <summary>
    /// Verifies copied vertex bounds stay stable while world-space transforms continue to update them.
    /// </summary>
    private static void ValidateCachedMeshBounds()
    {
        List<Vector3> sourceVertices = new List<Vector3>
        {
            new Vector3(-2.0f, -1.0f, -3.0f),
            new Vector3(4.0f, -1.0f, -3.0f),
            new Vector3(-2.0f, 5.0f, 6.0f)
        };
        MeshEntity mesh = new MeshEntity(
            "Cached bounds",
            sourceVertices,
            new[] { 0, 1, 2 },
            Array.Empty<Vector3>());

        (Vector3 Min, Vector3 Max) localBounds = mesh.GetLocalBounds();
        Require(AreClose(localBounds.Min, new Vector3(-2.0f, -1.0f, -3.0f)), "Cached local minimum was incorrect.");
        Require(AreClose(localBounds.Max, new Vector3(4.0f, 5.0f, 6.0f)), "Cached local maximum was incorrect.");

        sourceVertices[0] = new Vector3(-100.0f, -100.0f, -100.0f);
        (Vector3 Min, Vector3 Max) copiedBounds = mesh.GetLocalBounds();
        Require(AreClose(copiedBounds.Min, localBounds.Min) && AreClose(copiedBounds.Max, localBounds.Max), "External vertex changes affected cached mesh bounds.");

        mesh.UserTransform = Transform3DData.CreateTranslation(new Vector3(10.0f, 20.0f, 30.0f));
        (Vector3 Min, Vector3 Max) worldBounds = mesh.GetBounds();
        Require(AreClose(worldBounds.Min, new Vector3(8.0f, 19.0f, 27.0f)), "Translated world minimum was incorrect.");
        Require(AreClose(worldBounds.Max, new Vector3(14.0f, 25.0f, 36.0f)), "Translated world maximum was incorrect.");
    }

    /// <summary>
    /// Creates one small imported model that can own the support-layer fixture.
    /// </summary>
    private static MeshEntity CreateMesh()
    {
        return new MeshEntity(
            "Deletion test model",
            new[]
            {
                Vector3.Zero,
                new Vector3(100.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 100.0f, 0.0f)
            },
            new[] { 0, 1, 2 },
            Array.Empty<Vector3>());
    }

    /// <summary>
    /// Compares vectors with a small tolerance suitable for transform-bound calculations.
    /// </summary>
    private static bool AreClose(Vector3 left, Vector3 right)
    {
        return Vector3.DistanceSquared(left, right) <= 0.000001f;
    }

    /// <summary>
    /// Runs one test while preserving the remaining smoke-test group.
    /// </summary>
    private static void Run(List<string> failures, string name, Action test)
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
    /// Throws a readable test failure when a required condition is false.
    /// </summary>
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
