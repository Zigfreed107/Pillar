// AreaSupportToolOptionsControl.xaml.cs
// Owns Area Support tool option input, validation, and debounce timing for preview updates.
using Pillar.Core.Layers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for Area Support tool options.
/// </summary>
public partial class AreaSupportToolOptionsControl : UserControl
{
    private const int OptionsChangedDelayMilliseconds = 300;
    public const float DefaultAreaSupportSpacing = AreaSupportSettings.DefaultSpacing;
    public const float DefaultAreaSupportBoundaryOffset = AreaSupportSettings.DefaultBoundaryOffset;
    public const float DefaultAreaSupportOffsetSpacing = AreaSupportSettings.DefaultOffsetSpacing;
    public const float DefaultAreaSupportBoundarySpacing = AreaSupportSettings.DefaultBoundarySpacing;
    public const float DefaultConcaveCornerAngleDegrees = AreaSupportSettings.DefaultConcaveCornerAngleDegrees;
    public const float DefaultMinimumThinRegionThickness = AreaSupportSettings.DefaultMinimumThinRegionThickness;
    public const AreaSupportFillMode DefaultFillMode = AreaSupportSettings.DefaultFillMode;
    public const int DefaultAdditionalOffsetCount = AreaSupportSettings.DefaultAdditionalOffsetCount;

    private readonly DispatcherTimer _optionsChangedTimer;
    private bool _isSynchronizingOptions;

    /// <summary>
    /// Raised when an option changes and the active Area Support preview should be rebuilt.
    /// </summary>
    public event EventHandler? OptionsChanged;

    /// <summary>
    /// Raised when the user asks to launch reusable face selection.
    /// </summary>
    public event EventHandler? SelectFacesRequested;

    /// <summary>
    /// Raised when the user accepts the current Area Support preview.
    /// </summary>
    public event EventHandler? ApplyRequested;

    /// <summary>
    /// Raised when the user closes the current Area Support panel without applying supports.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Raised when the user asks to delete selected supports from the active Area Support edit.
    /// </summary>
    public event EventHandler? DeleteRequested;

    /// <summary>
    /// Creates the Area Support options control and its preview-refresh debounce timer.
    /// </summary>
    public AreaSupportToolOptionsControl()
    {
        _isSynchronizingOptions = true;
        _optionsChangedTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(OptionsChangedDelayMilliseconds)
        };
        _optionsChangedTimer.Tick += OptionsChangedTimer_Tick;
        InitializeComponent();
        AdditionalOffsetCountNumericUpDown.Maximum = AreaSupportSettings.MaximumAdditionalOffsetCount;
        AdditionalOffsetCountNumericUpDown.Value = DefaultAdditionalOffsetCount;
        UpdateFillModeControlVisibility();
        _isSynchronizingOptions = false;
    }

    /// <summary>
    /// Attempts to read the Area Support spacing field in millimeters.
    /// </summary>
    public bool TryGetSpacing(out float spacing)
    {
        if (TryGetFiniteFloat(AreaSpacingNumericUpDown.Value, out spacing) && spacing > 0.0f)
        {
            return true;
        }

        spacing = DefaultAreaSupportSpacing;
        return false;
    }

    /// <summary>
    /// Attempts to read the Area Support boundary offset field in millimeters.
    /// </summary>
    public bool TryGetBoundaryOffset(out float boundaryOffset)
    {
        if (TryGetFiniteFloat(BoundaryOffsetNumericUpDown.Value, out boundaryOffset) && boundaryOffset > 0.0f)
        {
            return true;
        }

        boundaryOffset = DefaultAreaSupportBoundaryOffset;
        return false;
    }

    /// <summary>
    /// Attempts to read the spacing between successive Boundary Offsets contours in millimeters.
    /// </summary>
    public bool TryGetOffsetSpacing(out float offsetSpacing)
    {
        if (TryGetFiniteFloat(OffsetSpacingNumericUpDown.Value, out offsetSpacing) && offsetSpacing > 0.0f)
        {
            return true;
        }

        offsetSpacing = DefaultAreaSupportOffsetSpacing;
        return false;
    }

    /// <summary>
    /// Attempts to read the Area Support boundary spacing field in millimeters.
    /// </summary>
    public bool TryGetBoundarySpacing(out float boundarySpacing)
    {
        if (TryGetFiniteFloat(BoundarySpacingNumericUpDown.Value, out boundarySpacing) && boundarySpacing > 0.0f)
        {
            return true;
        }

        boundarySpacing = DefaultAreaSupportBoundarySpacing;
        return false;
    }

    /// <summary>
    /// Attempts to read the concave corner threshold field in degrees.
    /// </summary>
    public bool TryGetConcaveCornerAngleDegrees(out float angleDegrees)
    {
        if (TryGetFiniteFloat(ConcaveCornerAngleNumericUpDown.Value, out angleDegrees)
            && angleDegrees >= 0.0f
            && angleDegrees <= 180.0f)
        {
            return true;
        }

        angleDegrees = DefaultConcaveCornerAngleDegrees;
        return false;
    }

    /// <summary>
    /// Gets whether collapsed thin regions should receive centreline fallback supports.
    /// </summary>
    public bool GetSupportThinRegions()
    {
        return SupportThinRegionsCheckBox.IsChecked == true;
    }

    /// <summary>
    /// Attempts to read the minimum local thickness required for thin-region fallback supports.
    /// </summary>
    public bool TryGetMinimumThinRegionThickness(out float minimumThickness)
    {
        if (TryGetFiniteFloat(MinimumThinRegionThicknessNumericUpDown.Value, out minimumThickness) && minimumThickness >= 0.0f)
        {
            return true;
        }

        minimumThickness = DefaultMinimumThinRegionThickness;
        return false;
    }

    /// <summary>
    /// Gets the selected interior support distribution strategy.
    /// </summary>
    public AreaSupportFillMode GetFillMode()
    {
        return BoundaryOffsetsFillRadioButton.IsChecked == true
            ? AreaSupportFillMode.BoundaryOffsets
            : AreaSupportFillMode.HexGrid;
    }

    /// <summary>
    /// Attempts to read how many inward rings should follow the original offset boundary.
    /// </summary>
    public bool TryGetAdditionalOffsetCount(out int additionalOffsetCount)
    {
        double value = AdditionalOffsetCountNumericUpDown.Value;

        if (!double.IsNaN(value)
            && !double.IsInfinity(value)
            && value >= 0.0
            && value <= AreaSupportSettings.MaximumAdditionalOffsetCount)
        {
            additionalOffsetCount = (int)Math.Round(value);
            return true;
        }

        additionalOffsetCount = DefaultAdditionalOffsetCount;
        return false;
    }

    /// <summary>
    /// Gets whether support spacing circles should be displayed in the preview.
    /// </summary>
    public bool GetShowSupportSpacing()
    {
        return ShowSupportSpacingToggleButton.IsChecked == true;
    }

    /// <summary>
    /// Sets all editable Area Support settings without raising live-preview refresh events.
    /// </summary>
    public void SetAreaSupportSettings(AreaSupportSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        _optionsChangedTimer.Stop();
        _isSynchronizingOptions = true;

        try
        {
            AreaSpacingNumericUpDown.Value = settings.Spacing;
            BoundaryOffsetNumericUpDown.Value = settings.BoundaryOffset;
            BoundarySpacingNumericUpDown.Value = settings.BoundarySpacing;
            ConcaveCornerAngleNumericUpDown.Value = settings.ConcaveCornerAngleDegrees;
            SupportThinRegionsCheckBox.IsChecked = settings.SupportThinRegions;
            MinimumThinRegionThicknessNumericUpDown.Value = settings.MinimumThinRegionThickness;
            HexGridFillRadioButton.IsChecked = settings.FillMode == AreaSupportFillMode.HexGrid;
            BoundaryOffsetsFillRadioButton.IsChecked = settings.FillMode == AreaSupportFillMode.BoundaryOffsets;
            OffsetSpacingNumericUpDown.Value = settings.OffsetSpacing;
            AdditionalOffsetCountNumericUpDown.Value = settings.AdditionalOffsetCount;
            UpdateFillModeControlVisibility();
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
    private void AreaOption_ValueChanged(object? sender, EventArgs e)
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
    /// Requests a reusable face-selection session for the Area Support source faces.
    /// </summary>
    private void SelectFacesButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _optionsChangedTimer.Stop();
        SelectFacesRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that preview spacing circles be shown or hidden.
    /// </summary>
    private void ShowSupportSpacingToggleButton_Changed(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isSynchronizingOptions)
        {
            return;
        }

        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Enables or disables thin-region thickness editing and refreshes the preview.
    /// </summary>
    private void SupportThinRegionsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (MinimumThinRegionThicknessNumericUpDown != null)
        {
            MinimumThinRegionThicknessNumericUpDown.IsEnabled = SupportThinRegionsCheckBox.IsChecked == true;
        }

        if (_isSynchronizingOptions)
        {
            return;
        }

        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Switches mode-specific controls and refreshes the generated preview.
    /// </summary>
    private void FillModeRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (SupportThinRegionsCheckBox == null || OffsetSpacingNumericUpDown == null || AdditionalOffsetCountNumericUpDown == null)
        {
            return;
        }

        UpdateFillModeControlVisibility();

        if (_isSynchronizingOptions)
        {
            return;
        }

        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Shows only the controls that affect the selected fill strategy.
    /// </summary>
    private void UpdateFillModeControlVisibility()
    {
        bool isHexGrid = HexGridFillRadioButton.IsChecked == true;
        Visibility hexVisibility = isHexGrid ? Visibility.Visible : Visibility.Collapsed;
        Visibility boundaryOffsetVisibility = isHexGrid ? Visibility.Collapsed : Visibility.Visible;

        SupportThinRegionsCheckBox.Visibility = hexVisibility;
        MinimumThinRegionThicknessLabel.Visibility = hexVisibility;
        MinimumThinRegionThicknessNumericUpDown.Visibility = hexVisibility;
        MinimumThinRegionThicknessUnitLabel.Visibility = hexVisibility;
        MinimumThinRegionThicknessNumericUpDown.IsEnabled = isHexGrid && SupportThinRegionsCheckBox.IsChecked == true;
        OffsetSpacingLabel.Visibility = boundaryOffsetVisibility;
        OffsetSpacingNumericUpDown.Visibility = boundaryOffsetVisibility;
        OffsetSpacingUnitLabel.Visibility = boundaryOffsetVisibility;
        AdditionalOffsetCountLabel.Visibility = boundaryOffsetVisibility;
        AdditionalOffsetCountNumericUpDown.Visibility = boundaryOffsetVisibility;
    }

    /// <summary>
    /// Requests that the owning shell apply the current Area Support preview.
    /// </summary>
    private void ApplyAreaSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _optionsChangedTimer.Stop();
        ApplyRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that the owning shell close the Area Support panel and discard transient preview state.
    /// </summary>
    private void CloseAreaSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _optionsChangedTimer.Stop();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests deletion of the selected supports in the active Area Support edit.
    /// </summary>
    private void DeleteSelectedSupportsButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _optionsChangedTimer.Stop();
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Restarts the live-preview debounce timer so area support generation is recalculated only after edits settle.
    /// </summary>
    private void RestartOptionsChangedTimer()
    {
        _optionsChangedTimer.Stop();
        _optionsChangedTimer.Start();
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
