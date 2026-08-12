// TranslateToolOptionsControl.xaml.cs
// Forwards Transform Translate actions to the shell that owns selection and command history.
using System;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using Pillar.Core.Entities;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for the Transform Translate options panel.
/// </summary>
public partial class TranslateToolOptionsControl : UserControl
{
    private bool _isSynchronizingOptions;

    /// <summary>
    /// Creates the Transform Translate options control.
    /// </summary>
    public TranslateToolOptionsControl()
    {
        _isSynchronizingOptions = true;
        InitializeComponent();
        _isSynchronizingOptions = false;
    }

    /// <summary>
    /// Raised when the user edits the model origin's absolute position.
    /// </summary>
    public event EventHandler<TranslateToolPositionChangedEventArgs>? PositionChanged;

    /// <summary>
    /// Raised when the user asks to align the model origin with build-plate X/Y zero.
    /// </summary>
    public event EventHandler? MoveToOriginRequested;

    /// <summary>
    /// Raised when the user asks to place the selected model on the build plate.
    /// </summary>
    public event EventHandler? MoveToPlateRequested;

    /// <summary>
    /// Raised when the user asks to close the Transform Translate options.
    /// </summary>
    public event EventHandler? FinishRequested;

    /// <summary>
    /// Synchronizes absolute position values and their printable movement ranges without raising edits.
    /// </summary>
    public void SetPositionAndLimits(Vector3 worldOrigin, MeshTranslationLimits limits)
    {
        _isSynchronizingOptions = true;

        try
        {
            if (limits.CanFitPrintableArea)
            {
                PositionXNumericUpDown.Minimum = limits.MinimumOriginX;
                PositionXNumericUpDown.Maximum = limits.MaximumOriginX;
                PositionYNumericUpDown.Minimum = limits.MinimumOriginY;
                PositionYNumericUpDown.Maximum = limits.MaximumOriginY;
                PositionZNumericUpDown.Minimum = limits.MinimumOriginZ;
                PositionZNumericUpDown.Maximum = float.MaxValue;
            }
            else
            {
                PositionXNumericUpDown.Minimum = worldOrigin.X;
                PositionXNumericUpDown.Maximum = worldOrigin.X;
                PositionYNumericUpDown.Minimum = worldOrigin.Y;
                PositionYNumericUpDown.Maximum = worldOrigin.Y;
                PositionZNumericUpDown.Minimum = worldOrigin.Z;
                PositionZNumericUpDown.Maximum = worldOrigin.Z;
            }

            PositionXNumericUpDown.IsEnabled = limits.CanFitPrintableArea;
            PositionYNumericUpDown.IsEnabled = limits.CanFitPrintableArea;
            PositionZNumericUpDown.IsEnabled = limits.CanFitPrintableArea;
            PositionXNumericUpDown.Value = worldOrigin.X;
            PositionYNumericUpDown.Value = worldOrigin.Y;
            PositionZNumericUpDown.Value = worldOrigin.Z;
        }
        finally
        {
            _isSynchronizingOptions = false;
        }
    }

    /// <summary>
    /// Synchronizes only the displayed absolute position after a drag or button action.
    /// </summary>
    public void SetPosition(Vector3 worldOrigin)
    {
        _isSynchronizingOptions = true;

        try
        {
            PositionXNumericUpDown.Value = worldOrigin.X;
            PositionYNumericUpDown.Value = worldOrigin.Y;
            PositionZNumericUpDown.Value = worldOrigin.Z;
        }
        finally
        {
            _isSynchronizingOptions = false;
        }
    }

    /// <summary>
    /// Raises one absolute position edit after the numeric controls have coerced their input ranges.
    /// </summary>
    private void PositionNumericUpDown_ValueChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isSynchronizingOptions || !ArePositionControlsReady())
        {
            return;
        }

        Vector3 position = new Vector3(
            (float)PositionXNumericUpDown.Value,
            (float)PositionYNumericUpDown.Value,
            (float)PositionZNumericUpDown.Value);
        PositionChanged?.Invoke(this, new TranslateToolPositionChangedEventArgs(position));
    }

    /// <summary>
    /// Forwards the Move to Origin button request to the owning shell.
    /// </summary>
    private void MoveToOriginButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        MoveToOriginRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Forwards the Move to Plate button request to the owning shell.
    /// </summary>
    private void MoveToPlateButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        MoveToPlateRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Forwards the Finish button request to the owning shell.
    /// </summary>
    private void FinishButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        FinishRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Checks whether XAML construction has assigned all position inputs used by early value events.
    /// </summary>
    private bool ArePositionControlsReady()
    {
        return PositionXNumericUpDown != null
            && PositionYNumericUpDown != null
            && PositionZNumericUpDown != null;
    }
}

/// <summary>
/// Carries one requested absolute model-origin position from the Translate options panel.
/// </summary>
public sealed class TranslateToolPositionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Creates immutable event data for one absolute position edit.
    /// </summary>
    public TranslateToolPositionChangedEventArgs(Vector3 position)
    {
        Position = position;
    }

    public Vector3 Position { get; }
}
