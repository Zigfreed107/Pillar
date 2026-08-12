// IslandDetectionRenderingSmokeTests.cs
// Verifies island overlays reuse a small non-selectable scene footprint and clear all transient geometry.
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Entities;
using Pillar.Geometry.Analysis;
using Pillar.Rendering.Preview;
using Pillar.UI.Analysis;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;

namespace Pillar.Rendering.SmokeTests;

/// <summary>
/// Runs focused rendering checks for island analysis overlays.
/// </summary>
public static class IslandDetectionRenderingSmokeTests
{
    /// <summary>
    /// Adds island rendering checks to the shared failure list.
    /// </summary>
    public static void Run(List<string> failures)
    {
        RunTest(failures, "Island result panel constructs", ValidatePanelConstruction);
        RunTest(failures, "Island preview reuses non-selectable models", ValidateReusableModels);
        RunTest(failures, "Island preview clears transient geometry", ValidateHideClearsGeometry);
    }

    /// <summary>
    /// Confirms custom numeric and routed checkbox events bind to compatible panel handlers.
    /// </summary>
    private static void ValidatePanelConstruction()
    {
        IslandDetectionPanel panel = new IslandDetectionPanel();
        IslandPresentationFilter filter = panel.CreateFilter();

        if (filter.MinimumPersistenceHeight != 0.0f || filter.MinimumBranchArea != 0.0f)
        {
            throw new InvalidOperationException("Expected the island panel to initialize its default filters.");
        }
    }

    /// <summary>
    /// Confirms multiple candidates use three combined non-hit-testable overlay models.
    /// </summary>
    private static void ValidateReusableModels()
    {
        (GroupModel3D Root, IslandDetectionPreviewRenderer Renderer, MeshEntity Mesh, IslandDetectionResult Result) fixture = CreateFixture();
        fixture.Renderer.Show(fixture.Mesh, fixture.Result.Candidates, 0, 0.25f);

        if (fixture.Root.Children.Count != 3)
        {
            throw new InvalidOperationException("Expected one inactive marker, active marker, and birth-face model.");
        }

        for (int modelIndex = 0; modelIndex < fixture.Root.Children.Count; modelIndex++)
        {
            if (fixture.Root.Children[modelIndex] is not MeshGeometryModel3D model
                || model.IsHitTestVisible
                || model.Geometry == null
                || model.Visibility != Visibility.Visible)
            {
                throw new InvalidOperationException("Expected visible non-selectable combined island geometry.");
            }
        }
    }

    /// <summary>
    /// Confirms closing the workflow drops every buffer and collapses every reusable model.
    /// </summary>
    private static void ValidateHideClearsGeometry()
    {
        (GroupModel3D Root, IslandDetectionPreviewRenderer Renderer, MeshEntity Mesh, IslandDetectionResult Result) fixture = CreateFixture();
        fixture.Renderer.Show(fixture.Mesh, fixture.Result.Candidates, 1, 0.25f);
        fixture.Renderer.Hide();

        for (int modelIndex = 0; modelIndex < fixture.Root.Children.Count; modelIndex++)
        {
            if (fixture.Root.Children[modelIndex] is not MeshGeometryModel3D model
                || model.Geometry != null
                || model.Visibility != Visibility.Collapsed)
            {
                throw new InvalidOperationException("Expected hidden island models with released transient buffers.");
            }
        }
    }

    /// <summary>
    /// Creates two disconnected floating boxes and their renderer-independent result.
    /// </summary>
    private static (GroupModel3D Root, IslandDetectionPreviewRenderer Renderer, MeshEntity Mesh, IslandDetectionResult Result) CreateFixture()
    {
        Buffers first = CreateBox(new Vector3(0.0f, 0.0f, 2.0f));
        Buffers second = CreateBox(new Vector3(3.0f, 0.0f, 3.0f));
        Vector3[] positions = new Vector3[16];
        int[] indices = new int[72];
        Array.Copy(first.Positions, positions, 8);
        Array.Copy(second.Positions, 0, positions, 8, 8);
        Array.Copy(first.Indices, indices, 36);

        for (int index = 0; index < second.Indices.Length; index++)
        {
            indices[36 + index] = second.Indices[index] + 8;
        }

        MeshEntity mesh = new MeshEntity("Floating shells", positions, indices);
        IslandDetectionResult result = new MeshIslandAnalyzer().Analyze(mesh);

        if (result.Candidates.Count != 2)
        {
            throw new InvalidOperationException("Expected a two-candidate renderer fixture.");
        }

        GroupModel3D root = new GroupModel3D();
        return (root, new IslandDetectionPreviewRenderer(root), mesh, result);
    }

    /// <summary>
    /// Creates one closed unit box with shared corner indices.
    /// </summary>
    private static Buffers CreateBox(Vector3 minimum)
    {
        Vector3 maximum = minimum + Vector3.One;
        Vector3[] positions =
        {
            new Vector3(minimum.X, minimum.Y, minimum.Z),
            new Vector3(maximum.X, minimum.Y, minimum.Z),
            new Vector3(maximum.X, maximum.Y, minimum.Z),
            new Vector3(minimum.X, maximum.Y, minimum.Z),
            new Vector3(minimum.X, minimum.Y, maximum.Z),
            new Vector3(maximum.X, minimum.Y, maximum.Z),
            new Vector3(maximum.X, maximum.Y, maximum.Z),
            new Vector3(minimum.X, maximum.Y, maximum.Z)
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
    /// Records one failed check while allowing remaining rendering tests to run.
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
}
