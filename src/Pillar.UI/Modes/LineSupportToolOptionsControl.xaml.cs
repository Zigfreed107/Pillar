// LineSupportToolOptionsControl.xaml.cs
// Owns Line Support tool option input, validation, and debounce timing for preview updates.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Pillar.Core.Layers;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for Line Support tool options.
/// </summary>
public partial class LineSupportToolOptionsControl : UserControl
{
    private const int OptionsChangedDelayMilliseconds = 300;
    public const float DefaultLineSupportSpacing = 5.0f;
    public const bool DefaultPlaceSupportsAtBends = true;

    private readonly DispatcherTimer _optionsChangedTimer;
    private bool _isSynchronizingOptions;

    /// <summary>
    /// Raised when an option changes and the active Line Support preview should be rebuilt.
    /// </summary>
    public event EventHandler? OptionsChanged;

    /// <summary>
    /// Raised when the user asks to launch reusable face selection.
    /// </summary>
    public event EventHandler? SelectFacesRequested;

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
        UpdateSurfaceTargetControls();
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
    /// Gets whether generated Line Support guide points should include every clicked polyline vertex.
    /// </summary>
    public bool GetPlaceSupportsAtBends()
    {
        return PlaceSupportsAtBendsCheckBox.IsChecked == true;
    }

    /// <summary>
    /// Sets the bend placement option without raising live-preview refresh events.
    /// </summary>
    public void SetPlaceSupportsAtBends(bool placeSupportsAtBends)
    {
        _optionsChangedTimer.Stop();
        _isSynchronizingOptions = true;

        try
        {
            PlaceSupportsAtBendsCheckBox.IsChecked = placeSupportsAtBends;
        }
        finally
        {
            _isSynchronizingOptions = false;
        }
    }

    /// <summary>
    /// Gets how generated Line Support points choose between surfaces that overlap in XY.
    /// </summary>
    public LineSupportSurfaceTargetMode GetSurfaceTargetMode()
    {
        if (SurfaceTargetComboBox.SelectedIndex == 2)
        {
            return LineSupportSurfaceTargetMode.SelectedFacesOnly;
        }

        if (SurfaceTargetComboBox.SelectedIndex == 1)
        {
            return LineSupportSurfaceTargetMode.NearestToLine;
        }

        return LineSupportSurfaceTargetMode.FirstReachable;
    }

    /// <summary>
    /// Sets the surface-targeting option without raising live-preview refresh events.
    /// </summary>
    public void SetSurfaceTargetMode(LineSupportSurfaceTargetMode surfaceTargetMode)
    {
        _optionsChangedTimer.Stop();
        _isSynchronizingOptions = true;

        try
        {
            SurfaceTargetComboBox.SelectedIndex = surfaceTargetMode switch
            {
                LineSupportSurfaceTargetMode.NearestToLine => 1,
                LineSupportSurfaceTargetMode.SelectedFacesOnly => 2,
                _ => 0
            };
            UpdateSurfaceTargetControls();
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
    /// Schedules a preview refresh when the user changes bend placement behavior.
    /// </summary>
    private void PlaceSupportsAtBendsCheckBox_Changed(object sender, RoutedEventArgs e)
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
    /// Schedules a preview refresh when the surface-targeting policy changes.
    /// </summary>
    private void SurfaceTargetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (SelectFacesButton != null)
        {
            UpdateSurfaceTargetControls();
        }

        if (_isSynchronizingOptions)
        {
            return;
        }

        RestartOptionsChangedTimer();
    }

    /// <summary>
    /// Requests a reusable face-selection session for Selected Faces Only targeting.
    /// </summary>
    private void SelectFacesButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _optionsChangedTimer.Stop();
        SelectFacesRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Enables face selection only while the selected-faces targeting policy is active.
    /// </summary>
    private void UpdateSurfaceTargetControls()
    {
        SelectFacesButton.IsEnabled = GetSurfaceTargetMode() == LineSupportSurfaceTargetMode.SelectedFacesOnly;
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
