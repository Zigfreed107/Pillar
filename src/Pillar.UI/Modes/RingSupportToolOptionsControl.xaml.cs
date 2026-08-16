// RingSupportToolOptionsControl.xaml.cs
// Owns Ring Support tool option input, validation, and debounce timing for preview updates.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Pillar.Core.Layers;
using Pillar.Core.Supports;

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
    /// Raised when the user asks to launch reusable face selection.
    /// </summary>
    public event EventHandler? SelectFacesRequested;

    /// <summary>
    /// Raised when the user accepts the current Ring Support preview.
    /// </summary>
    public event EventHandler? ApplyRequested;

    /// <summary>
    /// Raised when the user closes the current Ring Support panel without applying supports.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Raised when the user asks to delete selected supports from the active Ring Support edit.
    /// </summary>
    public event EventHandler? DeleteRequested;

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
        UpdateSurfaceTargetControls();
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
    /// Gets the selected support-base surface preference.
    /// </summary>
    public SupportBaseGenerationMode GetSupportBaseGenerationMode()
    {
        return SupportBaseGenerationOptions.GetGenerationMode();
    }

    /// <summary>
    /// Sets the support-base surface preference without raising a preview refresh.
    /// </summary>
    public void SetSupportBaseGenerationMode(SupportBaseGenerationMode generationMode)
    {
        _optionsChangedTimer.Stop();
        _isSynchronizingOptions = true;

        try
        {
            SupportBaseGenerationOptions.SetGenerationMode(generationMode);
        }
        finally
        {
            _isSynchronizingOptions = false;
        }
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
    /// Gets how generated Ring Support points choose target surfaces.
    /// </summary>
    public RingSupportSurfaceTargetMode GetSurfaceTargetMode()
    {
        return SurfaceTargetComboBox.SelectedIndex == 1
            ? RingSupportSurfaceTargetMode.SelectedFacesOnly
            : RingSupportSurfaceTargetMode.FirstReachable;
    }

    /// <summary>
    /// Sets the surface-targeting option without raising live-preview refresh events.
    /// </summary>
    public void SetSurfaceTargetMode(RingSupportSurfaceTargetMode surfaceTargetMode)
    {
        _optionsChangedTimer.Stop();
        _isSynchronizingOptions = true;

        try
        {
            SurfaceTargetComboBox.SelectedIndex = surfaceTargetMode == RingSupportSurfaceTargetMode.SelectedFacesOnly
                ? 1
                : 0;
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
    /// Refreshes the preview when the support-base generation preference changes.
    /// </summary>
    private void SupportBaseGenerationOptions_Changed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (!_isSynchronizingOptions)
        {
            RestartOptionsChangedTimer();
        }
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
        SelectFacesButton.IsEnabled = GetSurfaceTargetMode() == RingSupportSurfaceTargetMode.SelectedFacesOnly;
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
    /// Requests that the owning shell close the Ring Support panel and discard transient preview state.
    /// </summary>
    private void CloseRingSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _optionsChangedTimer.Stop();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests deletion of the selected supports in the active Ring Support edit.
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
