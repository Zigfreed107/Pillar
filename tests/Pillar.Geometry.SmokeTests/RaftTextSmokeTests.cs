// RaftTextSmokeTests.cs
// Verifies raft-interior placement, text extrusion, ownership, commands, persistence, layers, and export.
using Pillar.Commands;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Persistence;
using Pillar.Core.Rafts;
using Pillar.Core.RaftTexts;
using Pillar.Geometry.Export;
using Pillar.Geometry.RaftTexts;
using Pillar.Geometry.Tags;
using Pillar.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Pillar.Geometry.SmokeTests;

/// <summary>
/// Runs focused checks for the renderer-neutral Raft Text tool layers.
/// </summary>
internal static class RaftTextSmokeTests
{
    /// <summary>
    /// Adds all Raft Text failures to the shared smoke-test result list.
    /// </summary>
    public static void Run(List<string> failures)
    {
        RunTest("Raft text extrusion", ValidateTextExtrusion, failures);
        RunTest("Raft text plan-view orientation", ValidateOrientation, failures);
        RunTest("Raft text placement restriction", ValidatePlacementRestriction, failures);
        RunTest("Raft text command, layer, persistence, and export", ValidateDocumentWorkflow, failures);
    }

    /// <summary>
    /// Verifies the required half-height overlap below the raft surface.
    /// </summary>
    private static void ValidateTextExtrusion()
    {
        RaftTextSettings settings = new RaftTextSettings(
            text: "A",
            fontSize: 4.0f,
            textHeight: 1.2f);
        RaftTextMeshData localMesh = CreateRectangularTextMesh(settings, 4.0f, 2.0f);
        float minimumZ = float.PositiveInfinity;
        float maximumZ = float.NegativeInfinity;

        for (int i = 0; i < localMesh.Positions.Count; i++)
        {
            minimumZ = MathF.Min(minimumZ, localMesh.Positions[i].Z);
            maximumZ = MathF.Max(maximumZ, localMesh.Positions[i].Z);
        }

        Require(MathF.Abs(minimumZ + 0.6f) < 0.0001f, "Raft text did not extend down by half its Text Height.");
        Require(MathF.Abs(maximumZ - 1.2f) < 0.0001f, "Raft text did not extend up by its Text Height.");
    }

    /// <summary>
    /// Verifies plan-view rotation and its effect on the placement footprint.
    /// </summary>
    private static void ValidateOrientation()
    {
        RaftTextSettings settings = new RaftTextSettings(
            text: "A",
            orientationDegrees: 90.0f);
        RaftTextMeshData localMesh = CreateRectangularTextMesh(settings, 4.0f, 2.0f);
        (Vector3 minimum, Vector3 maximum) = CalculateBounds(localMesh.Positions);

        Require(MathF.Abs(minimum.X + 1.0f) < 0.0001f, "A 90 degree orientation did not rotate the text width.");
        Require(MathF.Abs(maximum.X - 1.0f) < 0.0001f, "A 90 degree orientation did not rotate the text width.");
        Require(MathF.Abs(minimum.Y + 2.0f) < 0.0001f, "A 90 degree orientation did not rotate the text height.");
        Require(MathF.Abs(maximum.Y - 2.0f) < 0.0001f, "A 90 degree orientation did not rotate the text height.");

        RaftTextPlacementPlanner planner = new RaftTextPlacementPlanner(
            CreateRectangularRaft(Guid.NewGuid()),
            localMesh,
            settings.BorderOffset);
        Require(
            planner.TryFindPlacement(new Vector2(9.5f, 0.0f), out Vector3 placement),
            "Rotated raft text did not find an interior placement.");
        Require(placement.X <= 8.0001f && placement.X >= 7.99f, "Rotated text placement ignored its rotated footprint.");

        _ = new RaftTextSettings(orientationDegrees: 360.0f);
        RequireThrowsOrientation(-0.01f);
        RequireThrowsOrientation(360.01f);
    }

    /// <summary>
    /// Verifies that pointer placement clamps the complete bordered text bounds inside the raft.
    /// </summary>
    private static void ValidatePlacementRestriction()
    {
        RaftEntity raft = CreateRectangularRaft(Guid.NewGuid());
        RaftTextSettings settings = new RaftTextSettings(text: "A");
        RaftTextMeshData localMesh = CreateRectangularTextMesh(settings, 4.0f, 2.0f);
        RaftTextPlacementPlanner planner = new RaftTextPlacementPlanner(
            raft,
            localMesh,
            settings.BorderOffset);

        Require(
            planner.TryFindPlacement(new Vector2(9.5f, 0.0f), out Vector3 placement),
            "Raft text planner did not find a valid interior placement.");
        Require(placement.X <= 7.0001f, "Raft text placement crossed the required right-edge border.");
        Require(placement.X >= 6.99f, "Raft text placement did not stay close to the requested edge position.");
        Require(MathF.Abs(placement.Z - 1.0f) < 0.0001f, "Raft text placement did not use the raft top surface.");
    }

    /// <summary>
    /// Verifies ownership, undo, layer nesting, save/load, and STL inclusion together.
    /// </summary>
    private static void ValidateDocumentWorkflow()
    {
        CadDocument document = new CadDocument();
        MeshEntity model = CreateModel();
        document.AddEntity(model);
        RaftEntity raft = CreateRectangularRaft(model.Id);
        document.AddEntity(raft);

        RaftTextSettings settings = new RaftTextSettings(
            text: "Part 42",
            fontFamilyName: "Arial",
            fontSize: 4.0f,
            textHeight: 0.8f,
            orientationDegrees: 35.0f);
        RaftTextMeshData localMesh = CreateRectangularTextMesh(settings, 4.0f, 2.0f);
        Vector3 placement = new Vector3(0.0f, 0.0f, 1.0f);
        RaftTextMeshData placedMesh = RaftTextMeshBuilder.Place(localMesh, placement);
        SupportLayerColor color = new SupportLayerColor(20, 100, 180);
        RaftTextEntity raftText = new RaftTextEntity(
            model.Id,
            settings,
            placement,
            placedMesh.Positions,
            placedMesh.TriangleIndices,
            color);
        ReplaceRaftTextCommand command = new ReplaceRaftTextCommand(document, null, raftText);
        command.Execute();
        Require(document.GetRaftTextsForModel(model.Id).Count == 1, "Raft text add command did not add the entity.");
        command.Undo();
        Require(document.GetRaftTextsForModel(model.Id).Count == 0, "Raft text add undo did not remove the entity.");
        command.Execute();

        SupportLayerColor changedColor = new SupportLayerColor(180, 80, 20);
        SetRaftTextColorCommand colorCommand = new SetRaftTextColorCommand(
            document,
            raftText,
            color,
            changedColor);
        colorCommand.Execute();
        Require(raftText.Color == changedColor, "Raft text color command did not apply the selected color.");
        colorCommand.Undo();
        Require(raftText.Color == color, "Raft text color undo did not restore the prior color.");

        ReplaceRaftCommand removeRaftCommand = new ReplaceRaftCommand(document, raft, null);
        removeRaftCommand.Execute();
        Require(document.GetRaftTextsForModel(model.Id).Count == 0, "Raft removal did not remove owned raft text.");
        removeRaftCommand.Undo();
        Require(document.GetRaftTextsForModel(model.Id).Count == 1, "Raft removal undo did not restore owned raft text.");

        LayerPanelViewModel layerPanel = new LayerPanelViewModel(document);
        layerPanel.SetRaftTargetModelEntityId(model.Id);
        Require(layerPanel.CanAddRaftText, "A model with a raft was not eligible for Raft Text.");
        Require(layerPanel.ModelLayers.Count == 1, "Raft text document did not retain its model layer.");
        LayerTreeItemViewModel raftRow = layerPanel.ModelLayers[0].Children[0];
        Require(raftRow.Kind == LayerTreeItemKind.Raft, "Raft text was not nested under a raft row.");
        Require(raftRow.Children.Count == 1, "Raft row did not contain the raft text layer.");
        Require(raftRow.Children[0].Kind == LayerTreeItemKind.RaftText, "Raft child row used the wrong layer kind.");
        Require(raftRow.Children[0].Name == "Text Part 42", "Raft text layer name did not include its displayed text.");

        string projectPath = Path.Combine(Path.GetTempPath(), $"pillar-raft-text-{Guid.NewGuid():N}.gph");
        string exportPath = Path.Combine(Path.GetTempPath(), $"pillar-raft-text-{Guid.NewGuid():N}.stl");

        try
        {
            GphDocumentSerializer serializer = new GphDocumentSerializer();
            serializer.Save(document, projectPath);
            GphDocumentData loadedData = serializer.LoadDocument(projectPath);
            CadDocument loadedDocument = new CadDocument();
            loadedDocument.ReplaceDocumentData(loadedData.Entities, loadedData.SupportLayerGroups);
            IReadOnlyList<RaftTextEntity> loadedRaftTexts = loadedDocument.GetRaftTextsForModel(model.Id);
            Require(loadedRaftTexts.Count == 1, "Saved raft text was not restored.");
            Require(loadedRaftTexts[0].Settings.Text == "Part 42", "Saved raft text content changed.");
            Require(loadedRaftTexts[0].Settings.FontFamilyName == "Arial", "Saved raft text font changed.");
            Require(MathF.Abs(loadedRaftTexts[0].Settings.OrientationDegrees - 35.0f) < 0.0001f, "Saved raft text orientation changed.");
            Require(loadedRaftTexts[0].Color == color, "Saved raft text color changed.");
            Require(loadedRaftTexts[0].Placement == placement, "Saved raft text placement changed.");

            StlExporter exporter = new StlExporter();
            exporter.ExportModelWithSupports(
                exportPath,
                model,
                Array.Empty<SupportEntity>(),
                16,
                raft,
                Array.Empty<TagEntity>(),
                new[] { raftText });
            using FileStream stream = File.OpenRead(exportPath);
            using BinaryReader reader = new BinaryReader(stream);
            stream.Position = 80;
            uint exportedTriangleCount = reader.ReadUInt32();
            uint expectedTriangleCount = (uint)(
                model.TriangleIndices.Count / 3
                + raft.TriangleIndices.Count / 3
                + raftText.TriangleIndices.Count / 3);
            Require(exportedTriangleCount == expectedTriangleCount, "STL export omitted raft text triangles.");
        }
        finally
        {
            if (File.Exists(projectPath)) File.Delete(projectPath);
            if (File.Exists(exportPath)) File.Delete(exportPath);
        }
    }

    /// <summary>
    /// Calculates deterministic mesh bounds for orientation checks.
    /// </summary>
    private static (Vector3 Minimum, Vector3 Maximum) CalculateBounds(IReadOnlyList<Vector3> positions)
    {
        Vector3 minimum = positions[0];
        Vector3 maximum = positions[0];

        for (int i = 1; i < positions.Count; i++)
        {
            minimum = Vector3.Min(minimum, positions[i]);
            maximum = Vector3.Max(maximum, positions[i]);
        }

        return (minimum, maximum);
    }

    /// <summary>
    /// Verifies an out-of-range orientation is rejected.
    /// </summary>
    private static void RequireThrowsOrientation(float orientationDegrees)
    {
        try
        {
            _ = new RaftTextSettings(orientationDegrees: orientationDegrees);
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }

        throw new InvalidOperationException("An out-of-range raft text orientation was accepted.");
    }

    /// <summary>
    /// Creates deterministic rectangular glyph contours for geometry checks.
    /// </summary>
    private static RaftTextMeshData CreateRectangularTextMesh(
        RaftTextSettings settings,
        float width,
        float height)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        IReadOnlyList<IReadOnlyList<Vector2>> contours = new IReadOnlyList<Vector2>[]
        {
            new Vector2[]
            {
                new Vector2(-halfWidth, -halfHeight),
                new Vector2(halfWidth, -halfHeight),
                new Vector2(halfWidth, halfHeight),
                new Vector2(-halfWidth, halfHeight)
            }
        };
        return RaftTextMeshBuilder.BuildLocal(settings, new TagTextOutlineData(width, contours));
    }

    /// <summary>
    /// Creates a closed 20 by 10 by 1 rectangular raft.
    /// </summary>
    private static RaftEntity CreateRectangularRaft(Guid modelEntityId)
    {
        Vector3[] vertices =
        {
            new Vector3(-10.0f, -5.0f, 0.0f),
            new Vector3(10.0f, -5.0f, 0.0f),
            new Vector3(10.0f, 5.0f, 0.0f),
            new Vector3(-10.0f, 5.0f, 0.0f),
            new Vector3(-10.0f, -5.0f, 1.0f),
            new Vector3(10.0f, -5.0f, 1.0f),
            new Vector3(10.0f, 5.0f, 1.0f),
            new Vector3(-10.0f, 5.0f, 1.0f)
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
        return new RaftEntity(modelEntityId, new RaftSettings(), vertices, indices);
    }

    /// <summary>
    /// Creates the minimal imported model required by ownership validation.
    /// </summary>
    private static MeshEntity CreateModel()
    {
        Vector3[] vertices = { Vector3.Zero, Vector3.UnitX, Vector3.UnitY };
        int[] indices = { 0, 1, 2 };
        return new MeshEntity("Raft Text Model", vertices, indices);
    }

    /// <summary>
    /// Runs one check and records its exception as a readable failure.
    /// </summary>
    private static void RunTest(string name, Action test, List<string> failures)
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
    /// Throws when one smoke-test expectation is not met.
    /// </summary>
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
