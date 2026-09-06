// FaceSetSelectionToolPanelSmokeTests.cs
// Verifies the face-selection panel keeps operation choice separate from shared coplanar expansion.
using Pillar.Rendering.Tools;
using Pillar.UI.Modes;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Pillar.UI.SmokeTests;

/// <summary>
/// Runs focused UI-state checks for the face-selection helper panel.
/// </summary>
internal static class FaceSetSelectionToolPanelSmokeTests
{
    /// <summary>
    /// Adds all face-selection panel failures to the shared UI smoke-test result.
    /// </summary>
    public static void Run(List<string> failures)
    {
        EnsureApplication();
        RunTest(failures, "Face selection angle toggle is independent from operation", ValidateIndependentAngleToggle);
        RunTest(failures, "Face selection threshold follows Angle Select", ValidateThresholdEnabledState);
        RunTest(failures, "Face selection drawing prompts explain finish and cancel gestures", ValidateDrawingPrompts);
    }

    /// <summary>
    /// Verifies Polygon Select participates in the exclusive operation group without changing Angle Select.
    /// </summary>
    private static void ValidateIndependentAngleToggle()
    {
        FaceSetSelectionToolPanel panel = new FaceSetSelectionToolPanel();
        ToggleButton selectButton = GetToggleButton(panel, "SelectToolButton");
        ToggleButton lineButton = GetToggleButton(panel, "LineSelectToolButton");
        ToggleButton polygonButton = GetToggleButton(panel, "PolygonSelectToolButton");
        CheckBox angleCheckBox = panel.FindName("AngleSelectCheckBox") as CheckBox
            ?? throw new InvalidOperationException("Expected the Angle Select checkbox.");

        panel.SetToolKind(FaceSetSelectionToolKind.PolygonSelect);
        panel.SetCoplanarExpansionEnabled(true);

        if (selectButton.IsChecked == true
            || lineButton.IsChecked == true
            || polygonButton.IsChecked != true
            || angleCheckBox.IsChecked != true)
        {
            throw new InvalidOperationException("Expected Polygon Select and independent Angle Select expansion to be active together.");
        }

        panel.SetToolKind(FaceSetSelectionToolKind.Select);

        if (selectButton.IsChecked != true
            || polygonButton.IsChecked == true
            || angleCheckBox.IsChecked != true)
        {
            throw new InvalidOperationException("Expected changing the operation to preserve the Angle Select toggle.");
        }
    }

    /// <summary>
    /// Verifies the coplanar threshold is editable only while Angle Select is enabled.
    /// </summary>
    private static void ValidateThresholdEnabledState()
    {
        FaceSetSelectionToolPanel panel = new FaceSetSelectionToolPanel();
        FrameworkElement thresholdControl = panel.FindName("CoplanarThresholdNumericUpDown") as FrameworkElement
            ?? throw new InvalidOperationException("Expected the coplanar-threshold control.");

        panel.SetCoplanarExpansionEnabled(false);

        if (thresholdControl.IsEnabled)
        {
            throw new InvalidOperationException("Expected the coplanar threshold to be disabled when Angle Select is off.");
        }

        panel.SetCoplanarExpansionEnabled(true);

        if (!thresholdControl.IsEnabled)
        {
            throw new InvalidOperationException("Expected the coplanar threshold to be enabled when Angle Select is on.");
        }
    }

    /// <summary>
    /// Verifies both drawing operations advertise right-click and Enter completion without assigning Escape to finish.
    /// </summary>
    private static void ValidateDrawingPrompts()
    {
        FaceSetSelectionToolPanel panel = new FaceSetSelectionToolPanel();
        System.Windows.Controls.TextBlock prompt = panel.FindName("ToolPromptTextBlock") as System.Windows.Controls.TextBlock
            ?? throw new InvalidOperationException("Expected the face-selection tool prompt.");

        panel.SetToolKind(FaceSetSelectionToolKind.LineSelect);
        ValidateCompletionPrompt(prompt.Text, "Line Select");
        panel.SetToolKind(FaceSetSelectionToolKind.PolygonSelect);
        ValidateCompletionPrompt(prompt.Text, "Polygon Select");
    }

    /// <summary>
    /// Checks one drawing prompt for the agreed completion and cancellation input contract.
    /// </summary>
    private static void ValidateCompletionPrompt(string prompt, string operationName)
    {
        if (!prompt.Contains("right-click", StringComparison.OrdinalIgnoreCase)
            || !prompt.Contains("Enter", StringComparison.OrdinalIgnoreCase)
            || !prompt.Contains("Esc cancels", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Expected {operationName} to describe right-click, Enter, and Escape behavior.");
        }
    }

    /// <summary>
    /// Creates application resources required by the panel when the harness runs alone.
    /// </summary>
    private static void EnsureApplication()
    {
        Application? currentApplication = Application.Current;

        if (currentApplication == null)
        {
            Pillar.UI.App application = new Pillar.UI.App();
            application.InitializeComponent();
            currentApplication = application;
        }

        currentApplication.ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    /// <summary>
    /// Gets one named toggle from the panel's XAML namescope.
    /// </summary>
    private static ToggleButton GetToggleButton(FaceSetSelectionToolPanel panel, string name)
    {
        return panel.FindName(name) as ToggleButton
            ?? throw new InvalidOperationException($"Expected face-selection toggle '{name}'.");
    }

    /// <summary>
    /// Records one failed validation while continuing the remaining UI smoke checks.
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
}
