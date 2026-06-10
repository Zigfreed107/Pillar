// ContourSupportToolOptionsControl.xaml.cs
// Owns Contour Support tool option input, validation, and debounce timing for preview updates.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Pillar.Core.Layers;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for Contour Support tool options.
/// </summary>
public partial class ContourSupportToolOptionsControl : UserControl
{
    private const int OptionsChangedDelayMilliseconds = 300;
    public const float DefaultContourSupportSpacing = ContourSupportSettings.DefaultSpacing;
    public const float DefaultCoplanarThresholdDegrees = ContourSupportSettings.DefaultCoplanarThresholdDegrees;
    public const float DefaultStartOffset = ContourSupportSettings.DefaultStartOffset;
    public const float DefaultFinalOffset = ContourSupportSettings.DefaultFinalOffset;

    private readonly DispatcherTimer _optionsChangedTimer;
    private bool _isSynchronizingOptions;

    /// <summary>
    /// Raised when an option changes and the active Contour Support preview should be rebuilt.
    /// </summary>
    public event EventHandler? OptionsChanged;

    /// <summary>
    /// Raised when the user asks to pick a new contour Z height from the model.
    /// </summary>
    public event EventHandler? PickZHeightRequested;

    /// <summary>
    /// Raised when the user accepts the current Contour Support preview.
    /// </summary>
    public event EventHandler? ApplyRequested;

    /// <summary>
    /// Raised when the user closes the current Contour Support panel without applying supports.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Raised when the user asks to delete selected supports from the active Contour Support edit.
    /// </summary>
    public event EventHandler? DeleteRequested;

    /// <summary>
    /// Creates the Contour Support options control and its preview-refresh debounce timer.
    /// </summary>
    public ContourSupportToolOptionsControl()
    {
        _isSynchronizingOptions = true;
        _optionsChangedTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(OptionsChangedDelayMilliseconds)
        };
        _optionsChangedTimer.Tick += OptionsChangedTimer_Tick;
        InitializeComponent();
        _isSynchronizingOptions = false;
    }

    /// <summary>
    /// Attempts to read the contour Z height field in millimeters.
    /// </summary>
    public bool TryGetZHeight(out float zHeight)
    {
        return TryGetFiniteFloat(ContourZHeightNumericUpDown.Value, out zHeight);
    }

    /// <summary>
    /// Attempts to read the coplanar threshold field in degrees.
    /// </summary>
    public bool TryGetCoplanarThresholdDegrees(out float threshold)
    {
        if (TryGetFiniteFloat(CoplanarThresholdNumericUpDown.Value, out threshold)
            && threshold >= 0.0f
            && threshold <= 180.0f)
        {
            return true;
        }

        threshold = DefaultCoplanarThresholdDegrees;
        return false;
    }

    /// <summary>
    /// Attempts to read the contour support spacing field in millimeters.
    /// </summary>
    public bool TryGetSpacing(out float spacing)
    {
        if (TryGetFiniteFloat(ContourSpacingNumericUpDown.Value, out spacing) && spacing > 0.0f)
        {
            return true;
        }

        spacing = DefaultContourSupportSpacing;
        return false;
    }

    /// <summary>
    /// Attempts to read the start offset field in millimeters.
    /// </summary>
    public bool TryGetStartOffset(out float startOffset)
    {
        if (TryGetFiniteFloat(StartOffsetNumericUpDown.Value, out startOffset) && startOffset >= 0.0f)
        {
            return true;
        }

        startOffset = DefaultStartOffset;
        return false;
    }

    /// <summary>
    /// Attempts to read the final offset field in millimeters.
    /// </summary>
    public bool TryGetFinalOffset(out float finalOffset)
    {
        if (TryGetFiniteFloat(FinalOffsetNumericUpDown.Value, out finalOffset) && finalOffset >= 0.0f)
        {
            return true;
        }

        finalOffset = DefaultFinalOffset;
        return false;
    }

    /// <summary>
    /// Sets the Z height field without raising live-preview refresh events.
    /// </summary>
    public void SetZHeight(float zHeight)
    {
        if (!float.IsFinite(zHeight))
        {
            zHeight = 0.0f;
        }

        SetOptionValue(ContourZHeightNumericUpDown, zHeight);
    }

    /// <summary>
    /// Sets all editable Contour Support settings without raising live-preview refresh events.
    /// </summary>
    public void SetContourSupportSettings(ContourSupportSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        _optionsChangedTimer.Stop();
        _isSynchronizingOptions = true;

        try
        {
            ContourZHeightNumericUpDown.Value = settings.ZHeight;
            CoplanarThresholdNumericUpDown.Value = settings.CoplanarThresholdDegrees;
            ContourSpacingNumericUpDown.Value = settings.Spacing;
            StartOffsetNumericUpDown.Value = settings.StartOffset;
            FinalOffsetNumericUpDown.Value = settings.FinalOffset;
        }
        finally
        {
            _isSynchronizingOptions = false;
        }
    }

    /// <summary>
    /// Enables or disables open-contour offset fields based on the current contour shape.
    /// </summary>
    public void SetContourClosed(bool isClosed)
    {
        StartOffsetNumericUpDown.IsEnabled = !isClosed;
        FinalOffsetNumericUpDown.IsEnabled = !isClosed;
    }

    /// <summary>
    /// Enables or disables the Delete button based on active support selection.
    /// </summary>
    public void SetDeleteSelectedSupportsEnabled(bool isEnabled)
    {
        DeleteSelectedSupportsButton.IsEnabled = isEnabled;
    }

    /// <summary>
    /// Schedules an option-driven preview refresh after the user pauses editing.
    /// </summary>
    private void ContourOption_ValueChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isSynchronizingOptions)
        {
            return;
        }

        RestartOptionsChangedTimer();
    }

    /// <summary>
    /// Raises the delayed option change event after typing has paused.
    /// </summary>
    private void OptionsChangedTimer_Tick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _optionsChangedTimer.Stop();
        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests a new model click for the contour Z height.
    /// </summary>
    private void PickZHeightButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _optionsChangedTimer.Stop();
        PickZHeightRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that the owning shell apply the current Contour Support preview.
    /// </summary>
    private void ApplyContourSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _optionsChangedTimer.Stop();
        ApplyRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that the owning shell close the Contour Support panel and discard transient preview state.
    /// </summary>
    private void CloseContourSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _optionsChangedTimer.Stop();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests deletion of the selected supports in the active Contour Support edit.
    /// </summary>
    private void DeleteSelectedSupportsButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _optionsChangedTimer.Stop();
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Restarts the live-preview debounce timer so contour extraction is recalculated only after edits settle.
    /// </summary>
    private void RestartOptionsChangedTimer()
    {
        _optionsChangedTimer.Stop();
        _optionsChangedTimer.Start();
    }

    /// <summary>
    /// Sets one numeric option without raising preview refresh events.
    /// </summary>
    private void SetOptionValue(Pillar.UI.Controls.NumericUpDown numericUpDown, float value)
    {
        _optionsChangedTimer.Stop();
        _isSynchronizingOptions = true;

        try
        {
            numericUpDown.Value = value;
        }
        finally
        {
            _isSynchronizingOptions = false;
        }
    }

    /// <summary>
    /// Converts a WPF double editor value into a finite float.
    /// </summary>
    private static bool TryGetFiniteFloat(double value, out float result)
    {
        if (!double.IsNaN(value)
            && !double.IsInfinity(value)
            && value >= -float.MaxValue
            && value <= float.MaxValue)
        {
            result = (float)value;
            return true;
        }

        result = 0.0f;
        return false;
    }
}
