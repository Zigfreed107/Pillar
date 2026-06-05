// LineSupportToolOptionsControl.xaml.cs
// Owns Line Support tool option input, validation, and debounce timing for preview updates.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for Line Support tool options.
/// </summary>
public partial class LineSupportToolOptionsControl : UserControl
{
    private const int OptionsChangedDelayMilliseconds = 300;
    public const float DefaultLineSupportSpacing = 5.0f;

    private readonly DispatcherTimer _optionsChangedTimer;
    private bool _isSynchronizingOptions;

    /// <summary>
    /// Raised when an option changes and the active Line Support preview should be rebuilt.
    /// </summary>
    public event EventHandler? OptionsChanged;

    /// <summary>
    /// Raised when the user accepts the current Line Support preview.
    /// </summary>
    public event EventHandler? ApplyRequested;

    /// <summary>
    /// Raised when the user closes the current Line Support panel without applying supports.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Raised when the user asks to delete selected supports from the active Line Support edit.
    /// </summary>
    public event EventHandler? DeleteRequested;

    /// <summary>
    /// Creates the Line Support options control and its preview-refresh debounce timer.
    /// </summary>
    public LineSupportToolOptionsControl()
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
    public bool TryGetLineSupportSpacing(out float spacing)
    {
        double spacingValue = LineSupportSpacingNumericUpDown.Value;

        if (spacingValue > 0.0
            && !double.IsNaN(spacingValue)
            && !double.IsInfinity(spacingValue)
            && spacingValue <= float.MaxValue)
        {
            spacing = (float)spacingValue;
            return true;
        }

        spacing = DefaultLineSupportSpacing;
        return false;
    }

    /// <summary>
    /// Sets the spacing field without raising live-preview refresh events.
    /// </summary>
    public void SetLineSupportSpacing(float spacing)
    {
        if (float.IsNaN(spacing) || float.IsInfinity(spacing) || spacing <= 0.0f)
        {
            spacing = DefaultLineSupportSpacing;
        }

        _optionsChangedTimer.Stop();
        _isSynchronizingOptions = true;

        try
        {
            LineSupportSpacingNumericUpDown.Value = spacing;
        }
        finally
        {
            _isSynchronizingOptions = false;
        }
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
    private void LineSupportSpacingNumericUpDown_ValueChanged(object? sender, EventArgs e)
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
    /// Requests that the owning shell apply the current Line Support preview.
    /// </summary>
    private void ApplyLineSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _optionsChangedTimer.Stop();
        ApplyRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that the owning shell close the Line Support panel and discard transient preview state.
    /// </summary>
    private void CloseLineSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _optionsChangedTimer.Stop();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests deletion of the selected supports in the active Line Support edit.
    /// </summary>
    private void DeleteSelectedSupportsButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _optionsChangedTimer.Stop();
        DeleteRequested?.Invoke(this, EventArgs.Empty);
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
