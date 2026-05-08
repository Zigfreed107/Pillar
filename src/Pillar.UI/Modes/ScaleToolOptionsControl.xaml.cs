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
    private const float DimensionTolerance = 0.0001f;
    private bool _isSynchronizingOptions;
    private Vector3 _originalSize = Vector3.One;

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
            SetSizeValuesFromScaleFactors(scaleFactors);
        }
        finally
        {
            _isSynchronizingOptions = false;
        }
    }

    /// <summary>
    /// Sets the model's original 100% size used to synchronize scale percentages and unit-size fields.
    /// </summary>
    public void SetOriginalSize(Vector3 originalSize)
    {
        _originalSize = new Vector3(
            SanitizeOriginalDimension(originalSize.X),
            SanitizeOriginalDimension(originalSize.Y),
            SanitizeOriginalDimension(originalSize.Z));

        _isSynchronizingOptions = true;

        try
        {
            SizeXNumericUpDown.IsEnabled = CanScaleFromSize(_originalSize.X);
            SizeYNumericUpDown.IsEnabled = CanScaleFromSize(_originalSize.Y);
            SizeZNumericUpDown.IsEnabled = CanScaleFromSize(_originalSize.Z);

            if (TryGetScaleFactors(out Vector3 scaleFactors))
            {
                SetSizeValuesFromScaleFactors(scaleFactors);
            }
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
        else if (TryGetScaleFactors(out Vector3 scaleFactors))
        {
            _isSynchronizingOptions = true;

            try
            {
                SetSizeValuesFromScaleFactors(scaleFactors);
            }
            finally
            {
                _isSynchronizingOptions = false;
            }
        }

        RaiseOptionsChanged();
    }

    /// <summary>
    /// Derives scale factors from a unit-size edit, then synchronizes the percentage fields.
    /// </summary>
    private void SizeNumericUpDown_ValueChanged(object? sender, EventArgs e)
    {
        if (_isSynchronizingOptions || !AreScaleControlsReady())
        {
            return;
        }

        if (!TryGetScaleFactors(out Vector3 scaleFactors))
        {
            return;
        }

        if (sender == SizeXNumericUpDown && CanScaleFromSize(_originalSize.X))
        {
            scaleFactors.X = (float)(SizeXNumericUpDown.Value / _originalSize.X);
        }
        else if (sender == SizeYNumericUpDown && CanScaleFromSize(_originalSize.Y))
        {
            scaleFactors.Y = (float)(SizeYNumericUpDown.Value / _originalSize.Y);
        }
        else if (sender == SizeZNumericUpDown && CanScaleFromSize(_originalSize.Z))
        {
            scaleFactors.Z = (float)(SizeZNumericUpDown.Value / _originalSize.Z);
        }

        if (ScaleLockToggleButton.IsChecked == true)
        {
            float lockedScale = GetEditedScaleFactor(sender, scaleFactors);
            scaleFactors = new Vector3(lockedScale, lockedScale, lockedScale);
        }

        _isSynchronizingOptions = true;

        try
        {
            ScaleXNumericUpDown.Value = scaleFactors.X * ScaleFactorToPercentage;
            ScaleYNumericUpDown.Value = scaleFactors.Y * ScaleFactorToPercentage;
            ScaleZNumericUpDown.Value = scaleFactors.Z * ScaleFactorToPercentage;
            SetSizeValuesFromScaleFactors(scaleFactors);
        }
        finally
        {
            _isSynchronizingOptions = false;
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
            SetSizeValuesFromScaleFactors(Vector3.One);
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
            SetSizeValuesFromScaleFactors(new Vector3((float)(value * ScalePercentageToFactor)));
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
    /// Updates unit-size fields from the original model size and current scale factors.
    /// </summary>
    private void SetSizeValuesFromScaleFactors(Vector3 scaleFactors)
    {
        SizeXNumericUpDown.Value = _originalSize.X * scaleFactors.X;
        SizeYNumericUpDown.Value = _originalSize.Y * scaleFactors.Y;
        SizeZNumericUpDown.Value = _originalSize.Z * scaleFactors.Z;
    }

    /// <summary>
    /// Gets the scale factor from the edited size axis so locked axes can share it.
    /// </summary>
    private float GetEditedScaleFactor(object? sender, Vector3 scaleFactors)
    {
        if (sender == SizeXNumericUpDown)
        {
            return scaleFactors.X;
        }

        if (sender == SizeYNumericUpDown)
        {
            return scaleFactors.Y;
        }

        if (sender == SizeZNumericUpDown)
        {
            return scaleFactors.Z;
        }

        return scaleFactors.X;
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
        return MathF.Abs(scaleFactors.X - scaleFactors.Y) <= DimensionTolerance
            && MathF.Abs(scaleFactors.X - scaleFactors.Z) <= DimensionTolerance;
    }

    /// <summary>
    /// Checks whether XAML construction has assigned every Scale option control referenced by event handlers.
    /// </summary>
    private bool AreScaleControlsReady()
    {
        return ScaleLockToggleButton != null
            && ScaleXNumericUpDown != null
            && ScaleYNumericUpDown != null
            && ScaleZNumericUpDown != null
            && SizeXNumericUpDown != null
            && SizeYNumericUpDown != null
            && SizeZNumericUpDown != null;
    }

    /// <summary>
    /// Converts an invalid original dimension into a harmless zero-size value.
    /// </summary>
    private static float SanitizeOriginalDimension(float dimension)
    {
        if (float.IsNaN(dimension) || float.IsInfinity(dimension) || dimension < 0.0f)
        {
            return 0.0f;
        }

        return dimension;
    }

    /// <summary>
    /// Gets whether a size edit can safely be converted back into a scale factor.
    /// </summary>
    private static bool CanScaleFromSize(float originalDimension)
    {
        return originalDimension > DimensionTolerance;
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
