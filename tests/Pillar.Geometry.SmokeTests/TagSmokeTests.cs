// TagSmokeTests.cs
// Verifies raft-edge placement, tapered tag body generation, ownership, commands, and persistence.
using Pillar.Commands;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Persistence;
using Pillar.Core.Rafts;
using Pillar.Core.Tags;
using Pillar.Geometry.Export;
using Pillar.Geometry.Tags;
using Pillar.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Pillar.Geometry.SmokeTests;

/// <summary>
/// Runs focused checks for the renderer-neutral Tag tool layers.
/// </summary>
internal static class TagSmokeTests
{
    /// <summary>
    /// Adds all Tag failures to the shared smoke-test result list.
    /// </summary>
    public static void Run(List<string> failures)
    {
        RunTest("Tag closest-edge placement", ValidateClosestEdgePlacement, failures);
        RunTest("Tag tapered body", ValidateTagBody, failures);
        RunTest("Tag text extrusion", ValidateTagTextExtrusion, failures);
        RunTest("Tag outside text placement and flip", ValidateTagOutsideTextPlacementAndFlip, failures);
        RunTest("Tag command and persistence", ValidateCommandAndPersistence, failures);
    }

    /// <summary>
    /// Resolves the nearest point on the rectangular raft bottom perimeter.
    /// </summary>
    private static void ValidateClosestEdgePlacement()
    {
        RaftEntity raft = CreateRectangularRaft(Guid.NewGuid());
        TagPlacementPlanner planner = new TagPlacementPlanner(raft);
        Require(
            planner.TryFindClosestPlacement(new Vector2(8.0f, 1.0f), out TagPlacement placement),
            "Placement planner did not find the raft perimeter.");
        Require(MathF.Abs(placement.AttachmentPoint.X - 5.0f) < 0.0001f, "Placement did not clamp to the nearest right edge.");
        Require(MathF.Abs(placement.AttachmentPoint.Y - 1.0f) < 0.0001f, "Placement changed the along-edge coordinate.");
        Require(MathF.Abs(placement.AttachmentPoint.Z) < 0.0001f, "Placement did not use the raft bottom plane.");
        Require(MathF.Abs(placement.Tangent.Length() - 1.0f) < 0.0001f, "Placement tangent was not normalized.");
        Vector2 outward = new Vector2(-placement.Tangent.Y, placement.Tangent.X);
        Require(outward.X > 0.999f, "Placement winding did not produce an outward normal on the raft's right edge.");
    }

    /// <summary>
    /// Checks body dimensions, taper growth, and closed indexed topology.
    /// </summary>
    private static void ValidateTagBody()
    {
        TagSettings settings = new TagSettings(
            tagHeight: 0.7f,
            edgeAngleDegrees: 45.0f,
            borderOffset: 1.0f,
            text: "ABC",
            fontSize: 5.0f);
        TagPlacement placement = new TagPlacement(Vector3.Zero, Vector2.UnitX);
        TagMeshData mesh = TagMeshBuilder.Build(settings, 8.0f, placement);

        Require(mesh.Positions.Count == 8, "Tag body did not contain two four-corner contours.");
        Require(mesh.TriangleIndices.Count == 36, "Tag body was not a closed twelve-triangle frustum.");

        float bottomLength = mesh.Positions[1].X - mesh.Positions[0].X;
        float bottomDepth = mesh.Positions[3].Y - mesh.Positions[0].Y;
        float topLength = mesh.Positions[5].X - mesh.Positions[4].X;
        Require(MathF.Abs(bottomLength - 10.0f) < 0.0001f, "Tag length did not include text width and both border offsets.");
        Require(MathF.Abs(settings.OuterWidth - 8.5f) < 0.0001f, "Default Outer Width did not use 1.5 times font size plus border.");
        Require(MathF.Abs(settings.InnerWidth - 8.5f) < 0.0001f, "Default Inner Width did not use 1.5 times font size plus border.");
        Require(MathF.Abs(mesh.Positions[0].Y + settings.InnerWidth) < 0.0001f, "Tag inner edge did not use Inner Width from the tangent.");
        Require(MathF.Abs(mesh.Positions[3].Y - settings.OuterWidth) < 0.0001f, "Tag outer edge did not use Outer Width from the tangent.");
        Require(MathF.Abs(bottomDepth - 17.0f) < 0.0001f, "Tag depth did not combine Inner Width and Outer Width.");
        Require(MathF.Abs(topLength - 11.4f) < 0.0001f, "Tag top did not expand for the 45-degree edge.");

        TagSettings clampedSettings = new TagSettings(
            borderOffset: 1.0f,
            fontSize: 5.0f,
            outerWidth: 2.0f,
            innerWidth: 3.0f);
        Require(MathF.Abs(clampedSettings.OuterWidth - 6.0f) < 0.0001f, "Outer Width was not clamped to font size plus border.");
        Require(MathF.Abs(clampedSettings.InnerWidth - 3.0f) < 0.0001f, "Inner Width was unexpectedly clamped to the outer minimum.");

        TagSettings borderClampedSettings = new TagSettings(
            tagHeight: 2.0f,
            borderOffset: 0.5f,
            fontSize: 5.0f,
            outerWidth: 1.0f);
        Require(MathF.Abs(borderClampedSettings.BorderOffset - 2.0f) < 0.0001f, "Border Offset was not clamped to Tag Height.");
        Require(MathF.Abs(borderClampedSettings.OuterWidth - 7.0f) < 0.0001f, "Outer Width did not use the clamped Border Offset.");
    }

    /// <summary>
    /// Checks that glyph holes remain open and text overlaps the upper half of the tag body.
    /// </summary>
    private static void ValidateTagTextExtrusion()
    {
        TagSettings settings = new TagSettings(
            tagHeight: 0.8f,
            text: "O",
            fontSize: 5.0f,
            textHeight: 1.2f);
        TagTextMeshData textMesh = CreateHollowTextMesh(settings, 4.0f);
        Require(textMesh.Positions.Count > 0, "Text extrusion produced no vertices.");
        Require(textMesh.TriangleIndices.Count > 0, "Text extrusion produced no triangles.");

        float minimumZ = float.PositiveInfinity;
        float maximumZ = float.NegativeInfinity;

        for (int positionIndex = 0; positionIndex < textMesh.Positions.Count; positionIndex++)
        {
            minimumZ = MathF.Min(minimumZ, textMesh.Positions[positionIndex].Z);
            maximumZ = MathF.Max(maximumZ, textMesh.Positions[positionIndex].Z);
        }

        Require(MathF.Abs(minimumZ - 0.4f) < 0.0001f, "Text did not begin halfway inside the body.");
        Require(MathF.Abs(maximumZ - 2.0f) < 0.0001f, "Text did not reach its configured height above the body.");

        for (int index = 0; index < textMesh.TriangleIndices.Count; index += 3)
        {
            Vector3 first = textMesh.Positions[textMesh.TriangleIndices[index]];
            Vector3 second = textMesh.Positions[textMesh.TriangleIndices[index + 1]];
            Vector3 third = textMesh.Positions[textMesh.TriangleIndices[index + 2]];

            if (MathF.Abs(first.Z - maximumZ) < 0.0001f
                && MathF.Abs(second.Z - maximumZ) < 0.0001f
                && MathF.Abs(third.Z - maximumZ) < 0.0001f)
            {
                Require(
                    !ContainsPointInTriangle(Vector2.Zero, new Vector2(first.X, first.Y), new Vector2(second.X, second.Y), new Vector2(third.X, third.Y)),
                    "Text triangulation filled a glyph hole.");
            }
        }

        TagMeshData placed = TagMeshBuilder.Build(
            settings,
            textMesh,
            new TagPlacement(new Vector3(10.0f, 20.0f, 3.0f), Vector2.UnitY));
        Require(placed.Positions.Count > textMesh.Positions.Count, "Placed tag did not combine its body and text meshes.");
    }

    /// <summary>
    /// Tests whether a point lies inside or on one projected triangle.
    /// </summary>
    private static bool ContainsPointInTriangle(Vector2 point, Vector2 first, Vector2 second, Vector2 third)
    {
        float firstCross = Cross(second - first, point - first);
        float secondCross = Cross(third - second, point - second);
        float thirdCross = Cross(first - third, point - third);
        bool hasNegative = firstCross < -0.000001f || secondCross < -0.000001f || thirdCross < -0.000001f;
        bool hasPositive = firstCross > 0.000001f || secondCross > 0.000001f || thirdCross > 0.000001f;
        return !(hasNegative && hasPositive);
    }

    /// <summary>
    /// Checks asymmetric widths, the text's outer border, and the persisted 180-degree orientation choice.
    /// </summary>
    private static void ValidateTagOutsideTextPlacementAndFlip()
    {
        TagTextMeshData markerText = new TagTextMeshData(
            2.0f,
            new[]
            {
                new Vector3(1.0f, -2.0f, 0.0f),
                new Vector3(1.0f, 2.0f, 0.0f)
            },
            Array.Empty<int>());
        TagPlacement placement = new TagPlacement(Vector3.Zero, Vector2.UnitX);
        TagSettings defaultSettings = new TagSettings(
            edgeAngleDegrees: 45.0f,
            borderOffset: 1.0f,
            fontSize: 5.0f,
            outerWidth: 10.0f,
            innerWidth: 3.0f);
        TagMeshData defaultMesh = TagMeshBuilder.Build(defaultSettings, markerText, placement);
        Vector3 defaultBaseMarker = defaultMesh.Positions[8];
        Vector3 defaultTopMarker = defaultMesh.Positions[9];
        Require(MathF.Abs(defaultMesh.Positions[0].Y + 3.0f) < 0.0001f, "Custom Inner Width did not position the inner edge.");
        Require(MathF.Abs(defaultMesh.Positions[3].Y - 10.0f) < 0.0001f, "Custom Outer Width did not position the outer edge.");
        Require(MathF.Abs(defaultMesh.Positions[7].Y - 10.7f) < 0.0001f, "The sloped top did not expand beyond Outer Width.");
        Require(MathF.Abs(defaultBaseMarker.X + 1.0f) < 0.0001f, "Default text was not rotated away from the previous orientation.");
        Require(MathF.Abs(defaultBaseMarker.Y - 9.7f) < 0.0001f, "Default text base was not one border offset from the top outer edge.");
        Require(MathF.Abs(defaultTopMarker.Y - 5.7f) < 0.0001f, "Default text extended in the wrong direction from its outer base.");

        TagSettings flippedSettings = new TagSettings(
            edgeAngleDegrees: 45.0f,
            borderOffset: 1.0f,
            fontSize: 5.0f,
            isTextFlipped: true,
            outerWidth: 10.0f,
            innerWidth: 3.0f);
        TagMeshData flippedMesh = TagMeshBuilder.Build(flippedSettings, markerText, placement);
        Vector3 flippedBaseMarker = flippedMesh.Positions[8];
        Vector3 flippedTopMarker = flippedMesh.Positions[9];
        Require(MathF.Abs(flippedBaseMarker.X - 1.0f) < 0.0001f, "Flip did not restore the previous text orientation.");
        Require(MathF.Abs(flippedBaseMarker.Y - 5.7f) < 0.0001f, "Flip did not rotate the text base inward.");
        Require(MathF.Abs(flippedTopMarker.Y - 9.7f) < 0.0001f, "Flipped text was not one border offset from the top outer edge.");
    }

    /// <summary>
    /// Returns the scalar two-dimensional cross product.
    /// </summary>
    private static float Cross(Vector2 first, Vector2 second)
    {
        return first.X * second.Y - first.Y * second.X;
    }

    /// <summary>
    /// Verifies add/update boundaries and a complete saved settings round trip.
    /// </summary>
    private static void ValidateCommandAndPersistence()
    {
        CadDocument document = new CadDocument();
        MeshEntity model = CreateModel();
        document.AddEntity(model);
        RaftEntity raft = CreateRectangularRaft(model.Id);
        document.AddEntity(raft);
        TagSettings settings = new TagSettings(
            0.8f,
            60.0f,
            1.2f,
            "Part 42",
            "Arial",
            6.0f,
            1.5f,
            true,
            9.0f,
            4.0f);
        TagPlacement placement = new TagPlacement(new Vector3(5.0f, 0.0f, 0.0f), Vector2.UnitY);
        TagTextMeshData textMesh = CreateHollowTextMesh(settings, 12.0f);
        TagMeshData mesh = TagMeshBuilder.Build(settings, textMesh, placement);
        SupportLayerColor color = new SupportLayerColor(20, 80, 140);
        TagEntity tag = new TagEntity(
            model.Id,
            settings,
            placement.AttachmentPoint,
            placement.Tangent,
            mesh.Positions,
            mesh.TriangleIndices,
            color);
        ReplaceTagCommand command = new ReplaceTagCommand(document, null, tag);
        command.Execute();
        Require(document.GetTagsForModel(model.Id).Count == 1, "Tag command did not add the tag.");
        LayerPanelViewModel layerPanel = new LayerPanelViewModel(document);
        layerPanel.SetRaftTargetModelEntityId(model.Id);
        Require(layerPanel.CanAddTag, "A selected model with a raft did not enable Add Tag.");
        Require(layerPanel.ModelLayers.Count == 1, "Tag fixture did not create one model layer.");
        Require(layerPanel.ModelLayers[0].Children.Count == 1, "Raft was not nested under its model.");
        Require(layerPanel.ModelLayers[0].Children[0].Kind == LayerTreeItemKind.Raft, "Expected the first model child to be the raft.");
        Require(layerPanel.ModelLayers[0].Children[0].Children.Count == 1, "Tag was not nested under its raft layer.");
        Require(layerPanel.ModelLayers[0].Children[0].Children[0].Name == "Tag Part 42", "Tag layer name did not reflect its text.");
        command.Undo();
        Require(document.GetTagsForModel(model.Id).Count == 0, "Tag command undo did not remove the tag.");
        command.Execute();
        ReplaceRaftCommand removeRaftCommand = new ReplaceRaftCommand(document, raft, null);
        removeRaftCommand.Execute();
        Require(document.FindRaftForModel(model.Id) == null, "Removing the raft left the raft in the document.");
        Require(document.GetTagsForModel(model.Id).Count == 0, "Removing the raft left its tags behind.");
        removeRaftCommand.Undo();
        Require(document.FindRaftForModel(model.Id) == raft, "Raft removal undo did not restore the raft.");
        Require(document.GetTagsForModel(model.Id).Count == 1, "Raft removal undo did not restore its tags.");

        string path = Path.Combine(Path.GetTempPath(), $"pillar-tag-{Guid.NewGuid():N}.gph");
        string exportPath = Path.Combine(Path.GetTempPath(), $"pillar-tag-{Guid.NewGuid():N}.stl");

        try
        {
            GphDocumentSerializer serializer = new GphDocumentSerializer();
            serializer.Save(document, path);
            GphDocumentData loadedData = serializer.LoadDocument(path);
            CadDocument loadedDocument = new CadDocument();
            loadedDocument.ReplaceDocumentData(loadedData.Entities, loadedData.SupportLayerGroups);
            IReadOnlyList<TagEntity> loadedTags = loadedDocument.GetTagsForModel(model.Id);
            Require(loadedTags.Count == 1, "Saved tag was not restored.");
            Require(loadedTags[0].Settings.Text == "Part 42", "Saved tag text changed.");
            Require(loadedTags[0].Settings.FontFamilyName == "Arial", "Saved tag font changed.");
            Require(loadedTags[0].Settings.IsTextFlipped, "Saved tag flip state changed.");
            Require(MathF.Abs(loadedTags[0].Settings.OuterWidth - 9.0f) < 0.0001f, "Saved Outer Width changed.");
            Require(MathF.Abs(loadedTags[0].Settings.InnerWidth - 4.0f) < 0.0001f, "Saved Inner Width changed.");
            Require(loadedTags[0].Color == color, "Saved tag color changed.");
            Require(loadedTags[0].Vertices.Count == mesh.Positions.Count, "Saved tag geometry changed.");
            Require(loadedTags[0].TriangleIndices.Count == mesh.TriangleIndices.Count, "Saved tag text triangles changed.");

            StlExporter exporter = new StlExporter();
            exporter.ExportModelWithSupports(
                exportPath,
                model,
                Array.Empty<SupportEntity>(),
                16,
                raft,
                new[] { tag });
            using FileStream exportStream = File.OpenRead(exportPath);
            using BinaryReader reader = new BinaryReader(exportStream);
            exportStream.Position = 80;
            uint exportedTriangleCount = reader.ReadUInt32();
            uint expectedTriangleCount = (uint)(
                model.TriangleIndices.Count / 3
                + raft.TriangleIndices.Count / 3
                + tag.TriangleIndices.Count / 3);
            Require(exportedTriangleCount == expectedTriangleCount, "STL export omitted tag body or text triangles.");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (File.Exists(exportPath))
            {
                File.Delete(exportPath);
            }
        }
    }

    /// <summary>
    /// Creates deterministic hollow glyph-like contours for geometry, persistence, and export checks.
    /// </summary>
    private static TagTextMeshData CreateHollowTextMesh(TagSettings settings, float measuredWidth)
    {
        IReadOnlyList<IReadOnlyList<Vector2>> contours = new IReadOnlyList<Vector2>[]
        {
            new Vector2[]
            {
                new Vector2(-2.0f, -2.0f),
                new Vector2(2.0f, -2.0f),
                new Vector2(2.0f, 2.0f),
                new Vector2(-2.0f, 2.0f)
            },
            new Vector2[]
            {
                new Vector2(-1.0f, -1.0f),
                new Vector2(-1.0f, 1.0f),
                new Vector2(1.0f, 1.0f),
                new Vector2(1.0f, -1.0f)
            }
        };
        TagTextOutlineData outline = new TagTextOutlineData(measuredWidth, contours);
        return TagTextMeshBuilder.Build(settings, outline);
    }

    /// <summary>
    /// Creates a simple closed 10 by 8 by 1 raft mesh with a triangulated bottom.
    /// </summary>
    private static RaftEntity CreateRectangularRaft(Guid modelEntityId)
    {
        Vector3[] vertices =
        {
            new Vector3(-5.0f, -4.0f, 0.0f),
            new Vector3(5.0f, -4.0f, 0.0f),
            new Vector3(5.0f, 4.0f, 0.0f),
            new Vector3(-5.0f, 4.0f, 0.0f),
            new Vector3(-5.0f, -4.0f, 1.0f),
            new Vector3(5.0f, -4.0f, 1.0f),
            new Vector3(5.0f, 4.0f, 1.0f),
            new Vector3(-5.0f, 4.0f, 1.0f)
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
    /// Creates the minimal imported model required by document ownership validation.
    /// </summary>
    private static MeshEntity CreateModel()
    {
        Vector3[] vertices =
        {
            Vector3.Zero,
            Vector3.UnitX,
            Vector3.UnitY
        };
        int[] indices = { 0, 1, 2 };
        return new MeshEntity("Tag Model", vertices, indices);
    }

    /// <summary>
    /// Runs one check and records its exception as a readable smoke-test failure.
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
