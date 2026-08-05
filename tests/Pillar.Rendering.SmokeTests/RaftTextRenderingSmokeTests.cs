// RaftTextRenderingSmokeTests.cs
// Verifies WPF font shaping, options initialization, and transient raft text rendering behavior.
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Layers;
using Pillar.Core.RaftTexts;
using Pillar.Geometry.RaftTexts;
using Pillar.Geometry.Tags;
using Pillar.Rendering.Preview;
using Pillar.UI.Modes;
using Pillar.UI.Tags;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Pillar.Rendering.SmokeTests;

/// <summary>
/// Runs UI and rendering checks for the Raft Text tool.
/// </summary>
internal static class RaftTextRenderingSmokeTests
{
    /// <summary>
    /// Adds all Raft Text rendering failures to the shared result list.
    /// </summary>
    public static void Run(List<string> failures)
    {
        RunTest("Raft text preview opacity", ValidatePreviewOpacity, failures);
        RunTest("Installed font creates solid raft text", ValidateInstalledFont, failures);
        RunTest("Missing raft text font falls back", ValidateMissingFontFallback, failures);
        RunTest("Raft text options initialize safely", ValidateOptionsInitialization, failures);
    }

    /// <summary>
    /// Verifies that moving and locked previews use the intended render passes.
    /// </summary>
    private static void ValidatePreviewOpacity()
    {
        GroupModel3D root = new GroupModel3D();
        RaftTextPreviewRenderer renderer = new RaftTextPreviewRenderer(root);
        RaftTextMeshData mesh = new RaftTextMeshData(
            new[]
            {
                new System.Numerics.Vector3(0.0f, 0.0f, 0.0f),
                new System.Numerics.Vector3(1.0f, 0.0f, 0.0f),
                new System.Numerics.Vector3(0.0f, 1.0f, 0.0f)
            },
            new[] { 0, 1, 2 });
        renderer.Show(mesh, new SupportLayerColor(64, 128, 192), 0.45f);

        if (root.Children.Count != 1
            || root.Children[0] is not MeshGeometryModel3D previewModel
            || !previewModel.IsTransparent)
        {
            throw new InvalidOperationException("Expected moving raft text to use the transparent render pass.");
        }

        renderer.Show(mesh, new SupportLayerColor(64, 128, 192), 1.0f);

        if (previewModel.IsTransparent || previewModel.Visibility != Visibility.Visible)
        {
            throw new InvalidOperationException("Expected locked raft text to remain visible and opaque.");
        }
        renderer.PrepareMoving(mesh, new SupportLayerColor(64, 128, 192), 0.45f);
        renderer.MovePrepared(new System.Numerics.Vector3(3.0f, 4.0f, 5.0f));

        if (previewModel.Transform is not System.Windows.Media.Media3D.TranslateTransform3D translation
            || System.Math.Abs(translation.OffsetX - 3.0) > 0.0001
            || System.Math.Abs(translation.OffsetY - 4.0) > 0.0001
            || System.Math.Abs(translation.OffsetZ - 5.0) > 0.0001)
        {
            throw new InvalidOperationException("Expected moving raft text to reuse a translation transform.");
        }
    }

    /// <summary>
    /// Verifies the shared WPF outline path creates printable raft text.
    /// </summary>
    private static void ValidateInstalledFont()
    {
        RaftTextSettings settings = new RaftTextSettings(
            text: "B8",
            fontFamilyName: RaftTextSettings.DefaultFontFamilyName,
            fontSize: 5.0f,
            textHeight: 1.0f);
        TagTextOutlineData outline = WpfTagTextOutlineFactory.Create(
            settings.Text,
            settings.FontFamilyName,
            settings.FontSize,
            RaftTextSettings.DefaultFontFamilyName);
        RaftTextMeshData mesh = RaftTextMeshBuilder.BuildLocal(settings, outline);

        if (outline.MeasuredWidth <= 0.0f
            || outline.Contours.Count < 2
            || mesh.Positions.Count == 0
            || mesh.TriangleIndices.Count == 0)
        {
            throw new InvalidOperationException("Expected the installed default font to produce solid raft text.");
        }
    }

    /// <summary>
    /// Verifies unavailable saved fonts use the required fallback family.
    /// </summary>
    private static void ValidateMissingFontFallback()
    {
        RaftTextSettings settings = new RaftTextSettings(
            text: "Fallback",
            fontFamilyName: "Pillar Missing Font Family");
        TagTextOutlineData outline = WpfTagTextOutlineFactory.Create(
            settings.Text,
            settings.FontFamilyName,
            settings.FontSize,
            RaftTextSettings.DefaultFontFamilyName);
        RaftTextMeshData mesh = RaftTextMeshBuilder.BuildLocal(settings, outline);

        if (outline.MeasuredWidth <= 0.0f || mesh.TriangleIndices.Count == 0)
        {
            throw new InvalidOperationException("Expected a missing font to fall back to printable raft text.");
        }
    }

    /// <summary>
    /// Verifies default controls can be constructed and read before a window is shown.
    /// </summary>
    private static void ValidateOptionsInitialization()
    {
        if (Application.Current == null)
        {
            Pillar.UI.App application = new Pillar.UI.App();
            application.InitializeComponent();
        }

        RaftTextToolOptionsControl control = new RaftTextToolOptionsControl();
        RaftTextSettings settings = control.GetSettings();

        if (MathF.Abs(settings.FontSize - RaftTextSettings.DefaultFontSize) > 0.0001f
            || MathF.Abs(settings.TextHeight - RaftTextSettings.DefaultTextHeight) > 0.0001f
            || MathF.Abs(settings.OrientationDegrees - RaftTextSettings.DefaultOrientationDegrees) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the Raft Text options panel to initialize defaults.");
        }

        control.SetSettings(new RaftTextSettings(orientationDegrees: 123.0f));
        settings = control.GetSettings();

        if (MathF.Abs(settings.OrientationDegrees - 123.0f) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the Orientation control to round-trip its value.");
        }
    }

    /// <summary>
    /// Runs one check and records its exception.
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
}
