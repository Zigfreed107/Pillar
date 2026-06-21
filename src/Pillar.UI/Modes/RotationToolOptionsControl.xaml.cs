// RotationToolOptionsControl.xaml.cs
// Owns rotation input and coordinate-space synchronization without performing document mutations.
using Pillar.Core.Entities;
using System;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for the Transform Rotate options panel.
/// </summary>
public partial class RotationToolOptionsControl : UserControl
{
    private bool _isSynchronizingOptions;

    public event EventHandler<RotationToolOptionsChangedEventArgs>? OptionsChanged;
    public event EventHandler<RotationCoordinateSpaceChangedEventArgs>? CoordinateSpaceChanged;
    public event EventHandler? ResetRequested;
    public event EventHandler? FinishRequested;
    public event EventHandler? CancelRequested;

    /// <summary>
    /// Creates the rotation options control with world-space, zero-delta session state.
    /// </summary>
    public RotationToolOptionsControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes the displayed coordinate space and zero-delta inputs without raising preview changes.
    /// </summary>
    public void SetSessionOptions(RotationCoordinateSpace coordinateSpace)
    {
        _isSynchronizingOptions = true;

        try
        {
            WorldRotationSpaceRadioButton.IsChecked = coordinateSpace == RotationCoordinateSpace.World;
            LocalRotationSpaceRadioButton.IsChecked = coordinateSpace == RotationCoordinateSpace.Local;
            SetRotationDegreesCore(Vector3.Zero);
        }
        finally
        {
            _isSynchronizingOptions = false;
        }
    }

    /// <summary>
    /// Sets all displayed session rotation deltas without raising a preview change.
    /// </summary>
    public void SetRotationDegrees(Vector3 rotationDegrees)
    {
        _isSynchronizingOptions = true;

        try
        {
            SetRotationDegreesCore(rotationDegrees);
        }
        finally
        {
            _isSynchronizingOptions = false;
        }
    }

    /// <summary>
    /// Writes the numeric controls while the caller owns event suppression.
    /// </summary>
    private void SetRotationDegreesCore(Vector3 rotationDegrees)
    {
        RotationXNumericUpDown.Value = rotationDegrees.X;
        RotationYNumericUpDown.Value = rotationDegrees.Y;
        RotationZNumericUpDown.Value = rotationDegrees.Z;
    }

    /// <summary>
    /// Publishes a live preview request whenever one valid axis input changes.
    /// </summary>
    private void RotationNumericUpDown_ValueChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isSynchronizingOptions || !AreControlsReady())
        {
            return;
        }

        OptionsChanged?.Invoke(this, new RotationToolOptionsChangedEventArgs(GetRotationDegrees()));
    }

    /// <summary>
    /// Rebases subsequent zero-delta inputs when the user switches between world and local axes.
    /// </summary>
    private void RotationSpaceRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isSynchronizingOptions || !AreControlsReady())
        {
            return;
        }

        SetRotationDegrees(Vector3.Zero);
        RotationCoordinateSpace coordinateSpace = LocalRotationSpaceRadioButton.IsChecked == true
            ? RotationCoordinateSpace.Local
            : RotationCoordinateSpace.World;
        CoordinateSpaceChanged?.Invoke(this, new RotationCoordinateSpaceChangedEventArgs(coordinateSpace));
    }

    /// <summary>
    /// Requests restoration of the model orientation established when it was imported.
    /// </summary>
    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        SetRotationDegrees(Vector3.Zero);
        ResetRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that the shell commit this session as one undoable transform command.
    /// </summary>
    private void FinishButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        FinishRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that the shell discard this session's live preview.
    /// </summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Reads the three numeric inputs as a rotation delta in degrees.
    /// </summary>
    private Vector3 GetRotationDegrees()
    {
        return new Vector3(
            (float)RotationXNumericUpDown.Value,
            (float)RotationYNumericUpDown.Value,
            (float)RotationZNumericUpDown.Value);
    }

    /// <summary>
    /// Checks whether XAML construction has assigned every control used by early change events.
    /// </summary>
    private bool AreControlsReady()
    {
        return WorldRotationSpaceRadioButton != null
            && LocalRotationSpaceRadioButton != null
            && RotationXNumericUpDown != null
            && RotationYNumericUpDown != null
            && RotationZNumericUpDown != null;
    }
}

/// <summary>
/// Carries a Transform Rotate session's X, Y, and Z degree deltas to the shell.
/// </summary>
public sealed class RotationToolOptionsChangedEventArgs : EventArgs
{
    /// <summary>
    /// Creates event data for one live rotation preview update.
    /// </summary>
    public RotationToolOptionsChangedEventArgs(Vector3 rotationDegrees)
    {
        RotationDegrees = rotationDegrees;
    }

    public Vector3 RotationDegrees { get; }
}

/// <summary>
/// Carries the newly selected rotation coordinate space to the shell.
/// </summary>
public sealed class RotationCoordinateSpaceChangedEventArgs : EventArgs
{
    /// <summary>
    /// Creates event data for a world/local toggle change.
    /// </summary>
    public RotationCoordinateSpaceChangedEventArgs(RotationCoordinateSpace coordinateSpace)
    {
        CoordinateSpace = coordinateSpace;
    }

    public RotationCoordinateSpace CoordinateSpace { get; }
}
