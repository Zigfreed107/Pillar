// ScaleToolOptionsControlSmokeTests.cs
// Verifies Scale options publish live previews and expose distinct cancel and finish session actions.
using Pillar.UI.Controls;
using Pillar.UI.Modes;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.SmokeTests;

/// <summary>
/// Runs focused interaction checks for the Transform Scale options panel.
/// </summary>
internal static class ScaleToolOptionsControlSmokeTests
{
    private const float ScaleTolerance = 0.0001f;

    /// <summary>
    /// Adds all Scale options failures to the shared UI smoke-test result.
    /// </summary>
    public static void Run(List<string> failures)
    {
        EnsureApplication();
        RunTest(failures, "Scale option edits publish live preview values", ValidateOptionChanges);
        RunTest(failures, "Scale Reset publishes original scale", ValidateResetPublishesOriginalScale);
        RunTest(failures, "Scale Cancel is left of Finish and raises cancel", ValidateCancelAction);
    }

    /// <summary>
    /// Verifies percentage changes immediately publish factors and synchronize displayed size.
    /// </summary>
    private static void ValidateOptionChanges()
    {
        ScaleToolOptionsControl panel = CreatePanel();
        ScaleToolOptionsChangedEventArgs? change = null;
        panel.OptionsChanged += (_, e) => change = e;

        NumericUpDown scaleX = GetNumericUpDown(panel, "ScaleXNumericUpDown");
        NumericUpDown sizeX = GetNumericUpDown(panel, "SizeXNumericUpDown");
        scaleX.Value = 150.0;

        if (change == null || MathF.Abs(change.ScaleFactors.X - 1.5f) > ScaleTolerance)
        {
            throw new InvalidOperationException("Expected a 150% X edit to publish an X scale factor of 1.5.");
        }

        if (Math.Abs(sizeX.Value - 15.0) > ScaleTolerance)
        {
            throw new InvalidOperationException("Expected the X size field to update with the live scale value.");
        }
    }

    /// <summary>
    /// Verifies Reset publishes a live preview that returns every axis to imported size.
    /// </summary>
    private static void ValidateResetPublishesOriginalScale()
    {
        ScaleToolOptionsControl panel = CreatePanel();
        ScaleToolOptionsChangedEventArgs? change = null;
        panel.SetScaleFactors(new Vector3(1.5f, 2.0f, 2.5f));
        panel.OptionsChanged += (_, e) => change = e;

        Button resetButton = GetButton(panel, "ResetScaleButton");
        resetButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        if (change == null || Vector3.Distance(change.ScaleFactors, Vector3.One) > ScaleTolerance)
        {
            throw new InvalidOperationException("Expected Reset to publish 100% scale on every axis.");
        }
    }

    /// <summary>
    /// Verifies Cancel appears to the left of Finish and publishes its explicit session action.
    /// </summary>
    private static void ValidateCancelAction()
    {
        ScaleToolOptionsControl panel = CreatePanel();
        Button cancelButton = GetButton(panel, "CancelScaleButton");
        Button finishButton = GetButton(panel, "FinishScaleButton");
        bool cancelRequested = false;
        panel.CancelRequested += (_, _) => cancelRequested = true;

        if (Grid.GetColumn(cancelButton) >= Grid.GetColumn(finishButton))
        {
            throw new InvalidOperationException("Expected Cancel to be positioned to the left of Finish.");
        }

        cancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        if (!cancelRequested)
        {
            throw new InvalidOperationException("Expected the Cancel button to publish CancelRequested.");
        }
    }

    /// <summary>
    /// Creates a panel with a deterministic imported size and neutral starting scale.
    /// </summary>
    private static ScaleToolOptionsControl CreatePanel()
    {
        ScaleToolOptionsControl panel = new ScaleToolOptionsControl();
        panel.SetOriginalSize(new Vector3(10.0f, 20.0f, 30.0f));
        panel.SetScaleFactors(Vector3.One);
        return panel;
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
    /// Gets one named numeric editor from the panel.
    /// </summary>
    private static NumericUpDown GetNumericUpDown(ScaleToolOptionsControl panel, string name)
    {
        return panel.FindName(name) as NumericUpDown
            ?? throw new InvalidOperationException($"Expected Scale numeric editor '{name}'.");
    }

    /// <summary>
    /// Gets one named button from the panel.
    /// </summary>
    private static Button GetButton(ScaleToolOptionsControl panel, string name)
    {
        return panel.FindName(name) as Button
            ?? throw new InvalidOperationException($"Expected Scale button '{name}'.");
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
