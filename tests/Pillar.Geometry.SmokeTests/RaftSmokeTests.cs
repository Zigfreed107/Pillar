// RaftSmokeTests.cs
// Validates procedural raft geometry, ownership invariants, and project-file persistence.
using Pillar.Commands;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Persistence;
using Pillar.Core.Rafts;
using Pillar.Core.Supports;
using Pillar.Geometry.Rafts;
using Pillar.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Pillar.Geometry.SmokeTests;

/// <summary>
/// Provides focused dependency-free tests for all raft strategies.
/// </summary>
internal static class RaftSmokeTests
{
    /// <summary>
    /// Runs the raft test group and appends readable failures.
    /// </summary>
    public static void Run(List<string> failures)
    {
        Run(failures, "Footprint raft creates a finite support-base hull", ValidateFootprintRaft);
        Run(failures, "Footprint raft creates disc and capsule degeneracy fallbacks", ValidateFootprintDegenerateCounts);
        Run(failures, "Footprint offset expands and rejects negative values", ValidateFootprintOffset);
        Run(failures, "Footprint raft triangles remain finite and non-degenerate", ValidateFootprintTriangleQuality);
        Run(failures, "Mesh raft creates Delaunay wireframe prisms", ValidateMeshRaft);
        Run(failures, "Single-base Mesh raft creates a circular pad", ValidateSingleBaseMeshRaft);
        Run(failures, "Mesh raft omits sides longer than the configured maximum", ValidateMeshRaftMaximumSideLength);
        Run(failures, "Feet raft creates one foot per unique support base", ValidateFeetRaft);
        Run(failures, "Raft eligibility requires a concrete support base", ValidateRaftEligibility);
        Run(failures, "Document permits only one raft per model", ValidateSingleRaftInvariant);
        Run(failures, "Raft settings, color, and geometry survive save and load", ValidatePersistence);
        Run(failures, "Raft color changes are undoable", ValidateRaftColorUndo);
        Run(failures, "Model removal undo restores its raft", ValidateModelRemovalUndo);
    }

    /// <summary>
    /// Verifies projected footprint, build-plane placement, and lip height.
    /// </summary>
    private static void ValidateFootprintRaft()
    {
        RaftSettings settings = new RaftSettings(RaftType.Footprint, lipHeight: 1.0f, lipWidth: 0.5f);
        RaftMeshData mesh = RaftMeshBuilder.Build(CreateSupports(), settings);
        Require(mesh.Positions.Count > 0 && mesh.TriangleIndices.Count > 0, "Footprint raft was empty.");
        Require(MathF.Abs(GetMinimumZ(mesh.Positions)) < 0.0001f, "Footprint raft did not begin on the build plane.");
        Require(MathF.Abs(GetMaximumZ(mesh.Positions) - 1.7f) < 0.0001f, "Footprint lip height was not applied.");

        Guid groupId = Guid.NewGuid();
        SupportProfile normalProfile = SupportDefaults.CreateProfile();
        SupportProfile largeProfile = CreateProfileWithBaseRadius(2.0f);
        IReadOnlyList<SupportEntity> hullSupports = new[]
        {
            new SupportEntity(groupId, new Vector3(0, 0, 5), Vector3.Zero, normalProfile),
            new SupportEntity(groupId, new Vector3(0, 0, 5), Vector3.Zero, largeProfile),
            new SupportEntity(groupId, new Vector3(10, 0, 5), new Vector3(10, 0, 0), normalProfile),
            new SupportEntity(groupId, new Vector3(0, 10, 5), new Vector3(0, 10, 0), normalProfile),
            new SupportEntity(groupId, new Vector3(3, 3, 5), new Vector3(3, 3, 0), normalProfile)
        };
        RaftSettings hullSettings = new RaftSettings(RaftType.Footprint, lipHeight: 0.0f, edgeAngleDegrees: 90.0f);
        RaftMeshData hullMesh = RaftMeshBuilder.Build(hullSupports, hullSettings);

        Require(GetMinimumX(hullMesh.Positions) <= -2.0f, "Coincident support bases did not retain the largest physical radius.");
        Require(GetMaximumX(hullMesh.Positions) >= 10.0f + SupportDefaults.DefaultBaseBottomRadius, "The convex hull did not cover an exterior support base.");
        Require(GetMaximumY(hullMesh.Positions) >= 10.0f + SupportDefaults.DefaultBaseBottomRadius, "The convex hull did not cover all support-base directions.");
    }

    /// <summary>
    /// Verifies one and two support locations create valid disc and capsule envelopes.
    /// </summary>
    private static void ValidateFootprintDegenerateCounts()
    {
        RaftSettings settings = new RaftSettings(RaftType.Footprint, lipHeight: 0.0f, edgeAngleDegrees: 90.0f);
        RaftMeshData disc = RaftMeshBuilder.Build(CreateSingleSupport(), settings);
        RaftMeshData capsule = RaftMeshBuilder.Build(CreateTwoSupports(10.0f), settings);
        float baseRadius = SupportDefaults.DefaultBaseBottomRadius;

        Require(disc.Positions.Count >= 64, "A single support did not create a circular footprint.");
        Require(GetMinimumX(disc.Positions) <= -baseRadius && GetMaximumX(disc.Positions) >= baseRadius, "The disc did not cover the physical support base.");
        Require(GetMinimumX(capsule.Positions) <= -baseRadius, "The capsule did not cover its first support base.");
        Require(GetMaximumX(capsule.Positions) >= 10.0f + baseRadius, "The capsule did not cover its second support base.");
    }

    /// <summary>
    /// Verifies positive footprint offsets expand the hull and negative offsets are rejected.
    /// </summary>
    private static void ValidateFootprintOffset()
    {
        IReadOnlyList<SupportEntity> support = CreateSingleSupport();
        RaftSettings baseSettings = new RaftSettings(RaftType.Footprint, lipHeight: 0.0f, edgeAngleDegrees: 90.0f);
        RaftSettings offsetSettings = new RaftSettings(RaftType.Footprint, lipHeight: 0.0f, footprintOffset: 2.0f, edgeAngleDegrees: 90.0f);
        RaftMeshData baseMesh = RaftMeshBuilder.Build(support, baseSettings);
        RaftMeshData offsetMesh = RaftMeshBuilder.Build(support, offsetSettings);
        bool rejectedNegativeOffset = false;

        try
        {
            _ = new RaftSettings(RaftType.Footprint, footprintOffset: -0.1f);
        }
        catch (ArgumentOutOfRangeException)
        {
            rejectedNegativeOffset = true;
        }

        Require(GetMaximumX(offsetMesh.Positions) - GetMaximumX(baseMesh.Positions) >= 1.99f, "A positive footprint offset did not expand the support envelope.");
        Require(rejectedNegativeOffset, "Raft settings accepted a negative footprint offset.");
    }

    /// <summary>
    /// Verifies the default tall lip produces bounded, finite, non-degenerate triangle geometry.
    /// </summary>
    private static void ValidateFootprintTriangleQuality()
    {
        RaftSettings settings = new RaftSettings(RaftType.Footprint);
        RaftMeshData mesh = RaftMeshBuilder.Build(CreateSingleSupport(), settings);
        float upperZ = settings.RaftHeight + settings.LipHeight;

        for (int i = 0; i < mesh.Positions.Count; i++)
        {
            Vector3 position = mesh.Positions[i];
            Require(float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z), "Footprint raft contained a non-finite vertex.");
            bool expectedZ = MathF.Abs(position.Z) < 0.0001f
                || MathF.Abs(position.Z - settings.RaftHeight) < 0.0001f
                || MathF.Abs(position.Z - upperZ) < 0.0001f;
            Require(expectedZ, "Footprint raft contained an unexpected Z projection.");
        }

        for (int i = 0; i + 2 < mesh.TriangleIndices.Count; i += 3)
        {
            Vector3 first = mesh.Positions[mesh.TriangleIndices[i]];
            Vector3 second = mesh.Positions[mesh.TriangleIndices[i + 1]];
            Vector3 third = mesh.Positions[mesh.TriangleIndices[i + 2]];
            Vector3 normal = Vector3.Cross(second - first, third - first);
            Require(normal.LengthSquared() > 0.00000001f, "Footprint raft contained a degenerate triangle.");

            if (MathF.Abs(first.Z - second.Z) < 0.0001f && MathF.Abs(second.Z - third.Z) < 0.0001f)
            {
                if (MathF.Abs(first.Z) < 0.0001f)
                {
                    Require(normal.Z < 0.0f, "A bottom footprint face had inward winding.");
                }
                else
                {
                    Require(normal.Z > 0.0f, "A top footprint face had inward winding.");
                }
            }
        }

        Require(GetMaximumRadius(mesh.Positions) < 20.0f, "Footprint raft generated an unbounded spike.");
    }

    /// <summary>
    /// Verifies Mesh generation produces Delaunay edge prisms at the requested height.
    /// </summary>
    private static void ValidateMeshRaft()
    {
        RaftMeshData mesh = RaftMeshBuilder.Build(CreateSupports(), new RaftSettings(RaftType.Mesh));
        Require(mesh.Positions.Count >= 24, "Mesh raft did not create wireframe edge prisms.");
        Require(MathF.Abs(GetMaximumZ(mesh.Positions) - RaftSettings.DefaultRaftThickness) < 0.0001f, "Mesh raft thickness was not applied.");
        Require(MathF.Abs(Vector3.Distance(mesh.Positions[0], mesh.Positions[3]) - RaftSettings.DefaultLineThickness) < 0.0001f, "Mesh raft bottom width changed.");
        Require(Vector3.Distance(mesh.Positions[4], mesh.Positions[7]) > RaftSettings.DefaultLineThickness, "Mesh raft top did not widen for a non-vertical edge angle.");
    }

    /// <summary>
    /// Verifies a Mesh raft with one unique support base creates a circular pad.
    /// </summary>
    private static void ValidateSingleBaseMeshRaft()
    {
        RaftSettings settings = new RaftSettings(RaftType.Mesh);
        RaftMeshData mesh = RaftMeshBuilder.Build(CreateSingleSupport(), settings);

        Require(mesh.Positions.Count == 64, "Single-base Mesh fallback did not create two 32-point circular contours.");
        Require(MathF.Abs(GetMinimumZ(mesh.Positions)) < 0.0001f, "Single-base Mesh fallback did not begin on the build plane.");
        Require(MathF.Abs(GetMaximumZ(mesh.Positions) - settings.RaftThickness) < 0.0001f, "Single-base Mesh fallback did not apply raft thickness.");
        Require(GetMaximumX(mesh.Positions) >= SupportDefaults.DefaultBaseBottomRadius, "Single-base Mesh fallback did not cover the support base.");
    }

    /// <summary>
    /// Verifies Mesh generation includes the limit boundary and rejects longer Delaunay edges.
    /// </summary>
    private static void ValidateMeshRaftMaximumSideLength()
    {
        RaftSettings settings = new RaftSettings(RaftType.Mesh);
        RaftMeshData includedMesh = RaftMeshBuilder.Build(CreateTwoSupports(50.0f), settings);
        RaftMeshData excludedMesh = RaftMeshBuilder.Build(CreateTwoSupports(50.01f), settings);

        Require(MathF.Abs(settings.MaxSideLength - RaftSettings.DefaultMaxSideLength) < 0.0001f, "Mesh raft maximum side length default changed.");
        Require(includedMesh.Positions.Count == 8, "Mesh raft excluded an edge at the maximum side length.");
        Require(excludedMesh.Positions.Count == 0, "Mesh raft generated an edge longer than the maximum side length.");
    }

    /// <summary>
    /// Verifies Feet generation produces one square frustum per unique support base.
    /// </summary>
    private static void ValidateFeetRaft()
    {
        RaftMeshData mesh = RaftMeshBuilder.Build(CreateSupports(), new RaftSettings(RaftType.Feet));
        Require(mesh.Positions.Count == 24, "Feet raft did not create exactly three eight-vertex feet.");
        Require(MathF.Abs(GetMaximumZ(mesh.Positions) - RaftSettings.DefaultRaftHeight) < 0.0001f, "Foot height was not applied.");
        Require(MathF.Abs(Vector3.Distance(mesh.Positions[0], mesh.Positions[1]) - RaftSettings.DefaultFootSize) < 0.0001f, "Foot bottom size changed.");
        Require(Vector3.Distance(mesh.Positions[4], mesh.Positions[5]) > RaftSettings.DefaultFootSize, "Foot top did not widen for a non-vertical edge angle.");
    }

    /// <summary>
    /// Verifies raft eligibility follows concrete support entities rather than empty support layers.
    /// </summary>
    private static void ValidateRaftEligibility()
    {
        CadDocument document = new CadDocument();
        MeshEntity model = CreateCubeModel();
        document.AddEntity(model);
        LayerPanelViewModel viewModel = new LayerPanelViewModel(document);
        viewModel.SetRaftTargetModelEntityId(model.Id);
        Require(!viewModel.CanGenerateRaft, "A model without support layers enabled the Raft tool.");

        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(model.Id, "Raft eligibility");
        document.AddSupportLayerGroup(supportLayerGroup);
        Require(!viewModel.CanGenerateRaft, "An empty support layer enabled the Raft tool.");

        SupportEntity support = new SupportEntity(
            supportLayerGroup.Id,
            new Vector3(0.0f, 0.0f, 5.0f),
            Vector3.Zero,
            SupportDefaults.CreateProfile());
        document.AddEntity(support);
        Require(viewModel.CanGenerateRaft, "Adding a concrete support did not enable the Raft tool.");

        document.RemoveEntity(support);
        Require(!viewModel.CanGenerateRaft, "Removing the final support did not disable the Raft tool.");
    }

    /// <summary>
    /// Verifies the document rejects a second raft owned by one model.
    /// </summary>
    private static void ValidateSingleRaftInvariant()
    {
        CadDocument document = new CadDocument();
        MeshEntity model = CreateCubeModel();
        document.AddEntity(model);
        RaftMeshData mesh = RaftMeshBuilder.Build(CreateSupports(), new RaftSettings());
        document.AddEntity(new RaftEntity(model.Id, new RaftSettings(), mesh.Positions, mesh.TriangleIndices));
        bool rejected = false;
        try
        {
            document.AddEntity(new RaftEntity(model.Id, new RaftSettings(), mesh.Positions, mesh.TriangleIndices));
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected, "Document accepted a second raft for one model.");
    }

    /// <summary>
    /// Verifies saved raft intent and buffers load back into a usable document.
    /// </summary>
    private static void ValidatePersistence()
    {
        CadDocument document = new CadDocument();
        MeshEntity model = CreateCubeModel();
        document.AddEntity(model);
        RaftSettings settings = new RaftSettings(RaftType.Footprint, 0.8f, 1.2f, 0.6f, 2.0f, edgeAngleDegrees: 60.0f, maxSideLength: 37.5f);
        RaftMeshData mesh = RaftMeshBuilder.Build(CreateSupports(), settings);
        SupportLayerColor savedColor = new SupportLayerColor(32, 96, 160);
        document.AddEntity(new RaftEntity(model.Id, settings, mesh.Positions, mesh.TriangleIndices, savedColor));
        string path = Path.Combine(Path.GetTempPath(), $"pillar-raft-{Guid.NewGuid():N}.gph");
        try
        {
            GphDocumentSerializer serializer = new GphDocumentSerializer();
            serializer.Save(document, path);
            GphDocumentData loaded = serializer.LoadDocument(path);
            RaftEntity? raft = null;
            for (int i = 0; i < loaded.Entities.Count; i++) raft = loaded.Entities[i] as RaftEntity ?? raft;
            Require(raft != null, "Saved raft was not loaded.");
            Require(raft!.Settings.Type == RaftType.Footprint && MathF.Abs(raft.Settings.FootprintOffset - 2.0f) < 0.0001f, "Saved raft settings changed.");
            Require(MathF.Abs(raft.Settings.MaxSideLength - 37.5f) < 0.0001f, "Saved maximum side length changed.");
            Require(raft.Color == savedColor, "Saved raft color changed.");
            Require(raft.Vertices.Count == mesh.Positions.Count, "Saved raft geometry changed.");
            CadDocument restoredDocument = new CadDocument();
            restoredDocument.ReplaceDocumentData(loaded.Entities, loaded.SupportLayerGroups);
            Require(restoredDocument.FindRaftForModel(model.Id) != null, "Loaded raft could not be applied to a document.");

            string savedJson = File.ReadAllText(path);
            string legacyJson = savedJson.Replace("\"footprintOffset\": 2", "\"footprintOffset\": -2", StringComparison.Ordinal);
            Require(!string.Equals(savedJson, legacyJson, StringComparison.Ordinal), "Persistence fixture could not create a legacy negative offset.");
            File.WriteAllText(path, legacyJson);
            GphDocumentData legacyLoaded = serializer.LoadDocument(path);
            RaftEntity? legacyRaft = null;
            for (int i = 0; i < legacyLoaded.Entities.Count; i++)
            {
                legacyRaft = legacyLoaded.Entities[i] as RaftEntity ?? legacyRaft;
            }
            Require(legacyRaft != null && MathF.Abs(legacyRaft.Settings.FootprintOffset) < 0.0001f, "A legacy negative footprint offset was not clamped to zero.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies a completed raft color edit can be executed and undone without replacing geometry.
    /// </summary>
    private static void ValidateRaftColorUndo()
    {
        CadDocument document = new CadDocument();
        MeshEntity model = CreateCubeModel();
        document.AddEntity(model);
        RaftMeshData mesh = RaftMeshBuilder.Build(CreateSupports(), new RaftSettings());
        SupportLayerColor oldColor = new SupportLayerColor(20, 40, 60);
        SupportLayerColor newColor = new SupportLayerColor(80, 100, 120);
        RaftEntity raft = new RaftEntity(model.Id, new RaftSettings(), mesh.Positions, mesh.TriangleIndices, oldColor);
        document.AddEntity(raft);
        SetRaftColorCommand command = new SetRaftColorCommand(document, raft, oldColor, newColor);

        command.Execute();
        Require(raft.Color == newColor, "Raft color command did not apply the requested color.");
        command.Undo();
        Require(raft.Color == oldColor, "Raft color command undo did not restore the original color.");
    }

    /// <summary>
    /// Verifies model-removal undo keeps model-owned raft state.
    /// </summary>
    private static void ValidateModelRemovalUndo()
    {
        CadDocument document = new CadDocument();
        MeshEntity model = CreateCubeModel();
        document.AddEntity(model);
        RaftMeshData mesh = RaftMeshBuilder.Build(CreateSupports(), new RaftSettings());
        RaftEntity raft = new RaftEntity(model.Id, new RaftSettings(), mesh.Positions, mesh.TriangleIndices);
        document.AddEntity(raft);
        RemoveModelWithSupportGroupsCommand command = new RemoveModelWithSupportGroupsCommand(
            document,
            model,
            Array.Empty<Pillar.Core.Layers.SupportLayerGroup>());

        command.Execute();
        Require(document.FindRaftForModel(model.Id) == null, "Removing a model left its raft behind.");
        command.Undo();
        Require(document.FindRaftForModel(model.Id) == raft, "Undo did not restore the model's raft.");
    }

    /// <summary>
    /// Creates the cube used by raft ownership and persistence tests.
    /// </summary>
    private static MeshEntity CreateCubeModel()
    {
        Vector3[] vertices =
        {
            new Vector3(-5, -5, 0), new Vector3(5, -5, 0), new Vector3(5, 5, 0), new Vector3(-5, 5, 0),
            new Vector3(-5, -5, 10), new Vector3(5, -5, 10), new Vector3(5, 5, 10), new Vector3(-5, 5, 10)
        };
        int[] indices = { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7 };
        return new MeshEntity("Cube", vertices, indices, Array.Empty<Vector3>());
    }

    /// <summary>
    /// Creates three support bases suitable for triangular Mesh and Feet tests.
    /// </summary>
    private static IReadOnlyList<SupportEntity> CreateSupports()
    {
        Guid groupId = Guid.NewGuid();
        SupportProfile profile = SupportDefaults.CreateProfile();
        return new[]
        {
            new SupportEntity(groupId, new Vector3(0, 0, 5), new Vector3(0, 0, 0), profile),
            new SupportEntity(groupId, new Vector3(10, 0, 5), new Vector3(10, 0, 0), profile),
            new SupportEntity(groupId, new Vector3(0, 10, 5), new Vector3(0, 10, 0), profile)
        };
    }

    /// <summary>
    /// Creates one support base for circular Footprint and Mesh fallback tests.
    /// </summary>
    private static IReadOnlyList<SupportEntity> CreateSingleSupport()
    {
        Guid groupId = Guid.NewGuid();
        SupportProfile profile = SupportDefaults.CreateProfile();
        return new[]
        {
            new SupportEntity(groupId, new Vector3(0, 0, 5), Vector3.Zero, profile)
        };
    }

    /// <summary>
    /// Copies the default support profile while replacing its build-plate radius.
    /// </summary>
    private static SupportProfile CreateProfileWithBaseRadius(float baseRadius)
    {
        SupportProfile defaults = SupportDefaults.CreateProfile();
        return new SupportProfile(
            baseRadius,
            defaults.BaseHeight,
            defaults.StemBottomDiameter,
            defaults.StemTopDiameter,
            defaults.MaximumBranchLength,
            defaults.ModelClearance,
            defaults.BranchAngleFromVerticalDegrees,
            defaults.HeadHeight,
            defaults.HeadPenetrationDepth,
            defaults.HeadTopDiameter,
            defaults.MaxHeadAngleFromVerticalDegrees);
    }

    /// <summary>
    /// Creates two support bases separated along X for Mesh edge-limit tests.
    /// </summary>
    private static IReadOnlyList<SupportEntity> CreateTwoSupports(float separation)
    {
        Guid groupId = Guid.NewGuid();
        SupportProfile profile = SupportDefaults.CreateProfile();
        return new[]
        {
            new SupportEntity(groupId, new Vector3(0, 0, 5), new Vector3(0, 0, 0), profile),
            new SupportEntity(groupId, new Vector3(separation, 0, 5), new Vector3(separation, 0, 0), profile)
        };
    }

    /// <summary>
    /// Gets the lowest generated X coordinate.
    /// </summary>
    private static float GetMinimumX(IReadOnlyList<Vector3> positions)
    {
        float value = float.PositiveInfinity;
        for (int i = 0; i < positions.Count; i++) value = MathF.Min(value, positions[i].X);
        return value;
    }

    /// <summary>
    /// Gets the highest generated X coordinate.
    /// </summary>
    private static float GetMaximumX(IReadOnlyList<Vector3> positions)
    {
        float value = float.NegativeInfinity;
        for (int i = 0; i < positions.Count; i++) value = MathF.Max(value, positions[i].X);
        return value;
    }

    /// <summary>
    /// Gets the highest generated Y coordinate.
    /// </summary>
    private static float GetMaximumY(IReadOnlyList<Vector3> positions)
    {
        float value = float.NegativeInfinity;
        for (int i = 0; i < positions.Count; i++) value = MathF.Max(value, positions[i].Y);
        return value;
    }

    /// <summary>
    /// Gets the largest XY radius generated around the origin.
    /// </summary>
    private static float GetMaximumRadius(IReadOnlyList<Vector3> positions)
    {
        float value = 0.0f;
        for (int i = 0; i < positions.Count; i++)
        {
            value = MathF.Max(value, new Vector2(positions[i].X, positions[i].Y).Length());
        }

        return value;
    }

    /// <summary>
    /// Gets the lowest generated vertex.
    /// </summary>
    private static float GetMinimumZ(IReadOnlyList<Vector3> positions)
    {
        float value = float.PositiveInfinity;
        for (int i = 0; i < positions.Count; i++) value = MathF.Min(value, positions[i].Z);
        return value;
    }

    /// <summary>
    /// Gets the highest generated vertex.
    /// </summary>
    private static float GetMaximumZ(IReadOnlyList<Vector3> positions)
    {
        float value = float.NegativeInfinity;
        for (int i = 0; i < positions.Count; i++) value = MathF.Max(value, positions[i].Z);
        return value;
    }

    /// <summary>
    /// Runs one test while preserving the remaining smoke-test group.
    /// </summary>
    private static void Run(List<string> failures, string name, Action test)
    {
        try { test(); }
        catch (Exception ex) { failures.Add($"{name}: {ex.Message}"); }
    }

    /// <summary>
    /// Throws a readable test failure when a required condition is false.
    /// </summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
