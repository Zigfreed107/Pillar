// ToolOptionsPanel.xaml.cs
// Hosts per-tool options controls and exposes a stable shell-facing API for the active tool options overlay.
using System;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for the active-tool options overlay host.
/// </summary>
public partial class ToolOptionsPanel : UserControl
{
    private const string RingSupportToolName = "Ring Support";
    private const string ScaleToolName = "Scale";
    public const float DefaultRingSupportSpacing = RingSupportToolOptionsControl.DefaultRingSupportSpacing;

    /// <summary>
    /// Raised when a Ring Support option changes and the active preview should be rebuilt.
    /// </summary>
    public event EventHandler? RingSupportOptionsChanged;

    /// <summary>
    /// Raised when the user accepts the current Ring Support preview.
    /// </summary>
    public event EventHandler? RingSupportApplyRequested;

    /// <summary>
    /// Raised when the user cancels the current Ring Support operation.
    /// </summary>
    public event EventHandler? RingSupportCancelRequested;

    /// <summary>
    /// Raised when one of the Transform Scale percentage fields changes.
    /// </summary>
    public event EventHandler<ScaleOptionsChangedEventArgs>? ScaleOptionsChanged;

    /// <summary>
    /// Raised when the user asks to close the Transform Scale options.
    /// </summary>
    public event EventHandler? ScaleFinishRequested;

    /// <summary>
    /// Creates the Tool Options Panel overlay and wires child option events.
    /// </summary>
    public ToolOptionsPanel()
    {
        InitializeComponent();
        WireChildOptionEvents();
    }

    /// <summary>
    /// Updates the host copy and reveals the options control for the selected tool.
    /// </summary>
    public void SetSelectedTool(string selectedToolName)
    {
        SelectedToolNameTextBlock.Text = selectedToolName;

        if (string.Equals(selectedToolName, RingSupportToolName, StringComparison.Ordinal))
        {
            SettingsSummaryTextBlock.Text = "Ring Support options";
            ShowOnlyRingSupportOptions();
            return;
        }

        if (string.Equals(selectedToolName, ScaleToolName, StringComparison.Ordinal))
        {
            SettingsSummaryTextBlock.Text = "Transform Scale options";
            ShowOnlyScaleOptions();
            return;
        }

        SettingsSummaryTextBlock.Text = $"{selectedToolName} does not have options wired yet.";
        HideToolSpecificOptions();
    }

    /// <summary>
    /// Attempts to read the Ring Support spacing field in millimeters.
    /// </summary>
    public bool TryGetRingSupportSpacing(out float spacing)
    {
        return RingSupportOptionsControl.TryGetRingSupportSpacing(out spacing);
    }

    /// <summary>
    /// Sets the Ring Support spacing field without raising live-preview refresh events.
    /// </summary>
    public void SetRingSupportSpacing(float spacing)
    {
        RingSupportOptionsControl.SetRingSupportSpacing(spacing);
    }

    /// <summary>
    /// Sets Transform Scale fields from stored scale factors without raising scale-change events.
    /// </summary>
    public void SetScaleFactors(Vector3 scaleFactors)
    {
        ScaleOptionsControl.SetScaleFactors(scaleFactors);
    }

    /// <summary>
    /// Attempts to read Transform Scale fields as scale factors where 1.0 means 100%.
    /// </summary>
    public bool TryGetScaleFactors(out Vector3 scaleFactors)
    {
        return ScaleOptionsControl.TryGetScaleFactors(out scaleFactors);
    }

    /// <summary>
    /// Wires child option controls to the host events consumed by MainWindow.
    /// </summary>
    private void WireChildOptionEvents()
    {
        RingSupportOptionsControl.OptionsChanged += RingSupportOptionsControl_OptionsChanged;
        RingSupportOptionsControl.ApplyRequested += RingSupportOptionsControl_ApplyRequested;
        RingSupportOptionsControl.CancelRequested += RingSupportOptionsControl_CancelRequested;
        ScaleOptionsControl.OptionsChanged += ScaleOptionsControl_OptionsChanged;
        ScaleOptionsControl.FinishRequested += ScaleOptionsControl_FinishRequested;
    }

    /// <summary>
    /// Shows only the Ring Support options control.
    /// </summary>
    private void ShowOnlyRingSupportOptions()
    {
        RingSupportOptionsControl.Visibility = Visibility.Visible;
        ScaleOptionsControl.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Shows only the Transform Scale options control.
    /// </summary>
    private void ShowOnlyScaleOptions()
    {
        RingSupportOptionsControl.Visibility = Visibility.Collapsed;
        ScaleOptionsControl.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Hides all tool-specific option controls.
    /// </summary>
    private void HideToolSpecificOptions()
    {
        RingSupportOptionsControl.Visibility = Visibility.Collapsed;
        ScaleOptionsControl.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Forwards Ring Support option edits to shell code.
    /// </summary>
    private void RingSupportOptionsControl_OptionsChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        RingSupportOptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Forwards Ring Support apply requests to shell code.
    /// </summary>
    private void RingSupportOptionsControl_ApplyRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        RingSupportApplyRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Forwards Ring Support cancel requests to shell code.
    /// </summary>
    private void RingSupportOptionsControl_CancelRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        RingSupportCancelRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Converts child Transform Scale events into the host event type consumed by shell code.
    /// </summary>
    private void ScaleOptionsControl_OptionsChanged(object? sender, ScaleToolOptionsChangedEventArgs e)
    {
        _ = sender;
        ScaleOptionsChanged?.Invoke(this, new ScaleOptionsChangedEventArgs(e.ScaleFactors));
    }

    /// <summary>
    /// Forwards Transform Scale finish requests to shell code.
    /// </summary>
    private void ScaleOptionsControl_FinishRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        ScaleFinishRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Carries Transform Scale factor values from the options panel to the owning shell.
    /// </summary>
    public sealed class ScaleOptionsChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates event data for one scale-factor edit.
        /// </summary>
        public ScaleOptionsChangedEventArgs(Vector3 scaleFactors)
        {
            ScaleFactors = scaleFactors;
        }

        /// <summary>
        /// Gets the requested scale factors where 1.0 means 100%.
        /// </summary>
        public Vector3 ScaleFactors { get; }
    }
}
