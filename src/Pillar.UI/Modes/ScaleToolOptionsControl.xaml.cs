// ScaleToolOptionsControl.xaml.cs
// Owns Transform Scale tool option input, axis locking, reset behavior, and scale-factor validation.
using System;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for Transform Scale tool options.
/// </summary>
public partial class ScaleToolOptionsControl : UserControl
{
    private const double ScalePercentageToFactor = 0.01;
    private const double ScaleFactorToPercentage = 100.0;
    private bool _isSynchronizingOptions;

    /// <summary>
    /// Raised when one of the scale percentage fields changes.
    /// </summary>
    public event EventHandler<ScaleToolOptionsChangedEventArgs>? OptionsChanged;

    /// <summary>
    /// Raised when the user asks to close the Transform Scale options.
    /// </summary>
    public event EventHandler? FinishRequested;

    /// <summary>
    /// Creates the Transform Scale options control.
    /// </summary>
    public ScaleToolOptionsControl()
    {
        _isSynchronizingOptions = true;
        InitializeComponent();
        _isSynchronizingOptions = false;
    }

    /// <summary>
    /// Sets scale fields from stored scale factors without raising scale-change events.
    /// </summary>
    public void SetScaleFactors(Vector3 scaleFactors)
    {
        _isSynchronizingOptions = true;

        try
        {
            if (!AreScaleFactorsUniform(scaleFactors))
            {
                ScaleLockToggleButton.IsChecked = false;
            }

            ScaleXNumericUpDown.Value = scaleFactors.X * ScaleFactorToPercentage;
            ScaleYNumericUpDown.Value = scaleFactors.Y * ScaleFactorToPercentage;
            ScaleZNumericUpDown.Value = scaleFactors.Z * ScaleFactorToPercentage;
        }
        finally
        {
            _isSynchronizingOptions = false;
        }
    }

    /// <summary>
    /// Attempts to read scale fields as scale factors where 1.0 means 100%.
    /// </summary>
    public bool TryGetScaleFactors(out Vector3 scaleFactors)
    {
        if (TryCreateScaleFactors(
            ScaleXNumericUpDown.Value,
            ScaleYNumericUpDown.Value,
            ScaleZNumericUpDown.Value,
            out scaleFactors))
        {
            return true;
        }

        scaleFactors = Vector3.One;
        return false;
    }

    /// <summary>
    /// Applies lock behavior and raises a scale change event for the owning host.
    /// </summary>
    private void ScaleNumericUpDown_ValueChanged(object? sender, EventArgs e)
    {
        if (_isSynchronizingOptions || !AreScaleControlsReady())
        {
            return;
        }

        if (ScaleLockToggleButton.IsChecked == true && sender is Pillar.UI.Controls.NumericUpDown numericUpDown)
        {
            SynchronizeLockedScaleValues(numericUpDown.Value);
        }

        RaiseOptionsChanged();
    }

    /// <summary>
    /// Makes all scale axes match when locking is enabled.
    /// </summary>
    private void ScaleLockToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (!AreScaleControlsReady())
        {
            return;
        }

        SynchronizeLockedScaleValues(ScaleXNumericUpDown.Value);
        RaiseOptionsChanged();
    }

    /// <summary>
    /// Leaves the current scale values unchanged when axis locking is disabled.
    /// </summary>
    private void ScaleLockToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
    }

    /// <summary>
    /// Resets all model scale axes to the original imported size.
    /// </summary>
    private void ResetScaleButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (!AreScaleControlsReady())
        {
            return;
        }

        _isSynchronizingOptions = true;

        try
        {
            ScaleXNumericUpDown.Value = ScaleFactorToPercentage;
            ScaleYNumericUpDown.Value = ScaleFactorToPercentage;
            ScaleZNumericUpDown.Value = ScaleFactorToPercentage;
        }
        finally
        {
            _isSynchronizingOptions = false;
        }

        RaiseOptionsChanged();
    }

    /// <summary>
    /// Requests that the owning shell close the Transform Scale tool options.
    /// </summary>
    private void FinishScaleButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        FinishRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Copies one scale value into all axes without causing nested scale-change events.
    /// </summary>
    private void SynchronizeLockedScaleValues(double value)
    {
        if (!AreScaleControlsReady())
        {
            return;
        }

        _isSynchronizingOptions = true;

        try
        {
            ScaleXNumericUpDown.Value = value;
            ScaleYNumericUpDown.Value = value;
            ScaleZNumericUpDown.Value = value;
        }
        finally
        {
            _isSynchronizingOptions = false;
        }
    }

    /// <summary>
    /// Raises a scale-change event when all three percentage fields contain valid factor values.
    /// </summary>
    private void RaiseOptionsChanged()
    {
        if (!AreScaleControlsReady())
        {
            return;
        }

        if (!TryGetScaleFactors(out Vector3 scaleFactors))
        {
            return;
        }

        OptionsChanged?.Invoke(this, new ScaleToolOptionsChangedEventArgs(scaleFactors));
    }

    /// <summary>
    /// Converts three UI percentages into finite, non-negative scale factors.
    /// </summary>
    private static bool TryCreateScaleFactors(double xPercentage, double yPercentage, double zPercentage, out Vector3 scaleFactors)
    {
        if (!IsValidScalePercentage(xPercentage)
            || !IsValidScalePercentage(yPercentage)
            || !IsValidScalePercentage(zPercentage))
        {
            scaleFactors = Vector3.One;
            return false;
        }

        scaleFactors = new Vector3(
            (float)(xPercentage * ScalePercentageToFactor),
            (float)(yPercentage * ScalePercentageToFactor),
            (float)(zPercentage * ScalePercentageToFactor));
        return true;
    }

    /// <summary>
    /// Checks whether one UI percentage can become a persisted scale factor.
    /// </summary>
    private static bool IsValidScalePercentage(double value)
    {
        return value >= 0.0
            && !double.IsNaN(value)
            && !double.IsInfinity(value)
            && value <= float.MaxValue;
    }

    /// <summary>
    /// Checks whether stored scale factors can be represented as one locked value.
    /// </summary>
    private static bool AreScaleFactorsUniform(Vector3 scaleFactors)
    {
        const float Tolerance = 0.0001f;

        return MathF.Abs(scaleFactors.X - scaleFactors.Y) <= Tolerance
            && MathF.Abs(scaleFactors.X - scaleFactors.Z) <= Tolerance;
    }

    /// <summary>
    /// Checks whether XAML construction has assigned every Scale option control referenced by event handlers.
    /// </summary>
    private bool AreScaleControlsReady()
    {
        return ScaleLockToggleButton != null
            && ScaleXNumericUpDown != null
            && ScaleYNumericUpDown != null
            && ScaleZNumericUpDown != null;
    }
}

/// <summary>
/// Carries Transform Scale factor values from the Scale options control to its host.
/// </summary>
public sealed class ScaleToolOptionsChangedEventArgs : EventArgs
{
    /// <summary>
    /// Creates event data for one scale-factor edit.
    /// </summary>
    public ScaleToolOptionsChangedEventArgs(Vector3 scaleFactors)
    {
        ScaleFactors = scaleFactors;
    }

    /// <summary>
    /// Gets the requested scale factors where 1.0 means 100%.
    /// </summary>
    public Vector3 ScaleFactors { get; }
}
