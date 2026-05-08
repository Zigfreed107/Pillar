// RingSupportToolOptionsControl.xaml.cs
// Owns Ring Support tool option input, validation, and debounce timing for preview updates.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for Ring Support tool options.
/// </summary>
public partial class RingSupportToolOptionsControl : UserControl
{
    private const int OptionsChangedDelayMilliseconds = 300;
    public const float DefaultRingSupportSpacing = 5.0f;

    private readonly DispatcherTimer _optionsChangedTimer;
    private bool _isSynchronizingOptions;

    /// <summary>
    /// Raised when an option changes and the active Ring Support preview should be rebuilt.
    /// </summary>
    public event EventHandler? OptionsChanged;

    /// <summary>
    /// Raised when the user accepts the current Ring Support preview.
    /// </summary>
    public event EventHandler? ApplyRequested;

    /// <summary>
    /// Raised when the user cancels the current Ring Support operation.
    /// </summary>
    public event EventHandler? CancelRequested;

    /// <summary>
    /// Creates the Ring Support options control and its preview-refresh debounce timer.
    /// </summary>
    public RingSupportToolOptionsControl()
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
    /// Attempts to read the spacing field in millimeters.
    /// </summary>
    public bool TryGetRingSupportSpacing(out float spacing)
    {
        double spacingValue = RingSupportSpacingNumericUpDown.Value;

        if (spacingValue > 0.0
            && !double.IsNaN(spacingValue)
            && !double.IsInfinity(spacingValue)
            && spacingValue <= float.MaxValue)
        {
            spacing = (float)spacingValue;
            return true;
        }

        spacing = DefaultRingSupportSpacing;
        return false;
    }

    /// <summary>
    /// Sets the spacing field without raising live-preview refresh events.
    /// </summary>
    public void SetRingSupportSpacing(float spacing)
    {
        if (float.IsNaN(spacing) || float.IsInfinity(spacing) || spacing <= 0.0f)
        {
            spacing = DefaultRingSupportSpacing;
        }

        _optionsChangedTimer.Stop();
        _isSynchronizingOptions = true;

        try
        {
            RingSupportSpacingNumericUpDown.Value = spacing;
        }
        finally
        {
            _isSynchronizingOptions = false;
        }
    }

    /// <summary>
    /// Schedules an option-driven preview refresh after the user pauses editing.
    /// </summary>
    private void RingSupportSpacingNumericUpDown_ValueChanged(object? sender, EventArgs e)
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
    /// Requests that the owning shell apply the current Ring Support preview.
    /// </summary>
    private void ApplyRingSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _optionsChangedTimer.Stop();
        ApplyRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that the owning shell cancel the current Ring Support operation and discard transient preview state.
    /// </summary>
    private void CancelRingSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _optionsChangedTimer.Stop();
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Restarts the live-preview debounce timer so projected markers are recalculated only after edits settle.
    /// </summary>
    private void RestartOptionsChangedTimer()
    {
        _optionsChangedTimer.Stop();
        _optionsChangedTimer.Start();
    }
}
