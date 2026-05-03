// ToolOptionsPanel.xaml.cs
// Provides the code-behind for the active-tool options overlay without owning tool behavior or document state.
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for the active-tool options overlay.
/// </summary>
public partial class ToolOptionsPanel : UserControl
{
    private const string RingSupportToolName = "Ring Support";
    private const float RingSupportSpacingStep = 0.25f;
    private const int RingSupportOptionsChangedDelayMilliseconds = 300;
    public const float DefaultRingSupportSpacing = 5.0f;
    private readonly DispatcherTimer _ringSupportOptionsChangedTimer;
    private bool _isSynchronizingRingSupportOptions;

    /// <summary>
    /// Raised when a Ring Support option changes and the active preview should be rebuilt.
    /// </summary>
    public event EventHandler? RingSupportOptionsChanged;

    /// <summary>
    /// Raised when the user accepts the current Ring Support preview.
    /// </summary>
    public event EventHandler? RingSupportApplyRequested;

    /// <summary>
    /// Raised when the user cancels the current Ring Support operation.
    /// </summary>
    public event EventHandler? RingSupportCancelRequested;

    /// <summary>
    /// Creates the Tool Options Panel overlay.
    /// </summary>
    public ToolOptionsPanel()
    {
        _ringSupportOptionsChangedTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(RingSupportOptionsChangedDelayMilliseconds)
        };
        _ringSupportOptionsChangedTimer.Tick += RingSupportOptionsChangedTimer_Tick;
        InitializeComponent();
    }

    /// <summary>
    /// Updates the mock panel copy for the selected tool.
    /// </summary>
    public void SetSelectedTool(string selectedToolName)
    {
        SelectedToolNameTextBlock.Text = selectedToolName;

        if (string.Equals(selectedToolName, RingSupportToolName, StringComparison.Ordinal))
        {
            SettingsSummaryTextBlock.Text = "Ring Support options";
            RingSupportOptionsGrid.Visibility = Visibility.Visible;
            return;
        }

        SettingsSummaryTextBlock.Text = $"{selectedToolName} does not have options wired yet.";
        RingSupportOptionsGrid.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Attempts to read the Ring Support spacing field in millimeters.
    /// </summary>
    public bool TryGetRingSupportSpacing(out float spacing)
    {
        string text = RingSupportSpacingTextBox.Text.Trim();

        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out spacing)
            && spacing > 0.0f
            && !float.IsNaN(spacing)
            && !float.IsInfinity(spacing))
        {
            return true;
        }

        spacing = DefaultRingSupportSpacing;
        return false;
    }

    /// <summary>
    /// Sets the Ring Support spacing field without raising live-preview refresh events.
    /// </summary>
    public void SetRingSupportSpacing(float spacing)
    {
        if (float.IsNaN(spacing) || float.IsInfinity(spacing) || spacing <= 0.0f)
        {
            spacing = DefaultRingSupportSpacing;
        }

        _ringSupportOptionsChangedTimer.Stop();
        _isSynchronizingRingSupportOptions = true;

        try
        {
            RingSupportSpacingTextBox.Text = spacing.ToString("0.00", CultureInfo.InvariantCulture);
        }
        finally
        {
            _isSynchronizingRingSupportOptions = false;
        }
    }

    /// <summary>
    /// Decreases the Ring Support spacing spinner by one step.
    /// </summary>
    private void DecreaseRingSupportSpacingButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        StepRingSupportSpacing(-RingSupportSpacingStep);
    }

    /// <summary>
    /// Increases the Ring Support spacing spinner by one step.
    /// </summary>
    private void IncreaseRingSupportSpacingButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        StepRingSupportSpacing(RingSupportSpacingStep);
    }

    /// <summary>
    /// Schedules an option-driven preview refresh after the user pauses editing.
    /// </summary>
    private void RingSupportSpacingTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isSynchronizingRingSupportOptions)
        {
            return;
        }

        RestartRingSupportOptionsChangedTimer();
    }

    /// <summary>
    /// Raises the delayed Ring Support option change event after typing has paused.
    /// </summary>
    private void RingSupportOptionsChangedTimer_Tick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ringSupportOptionsChangedTimer.Stop();
        RingSupportOptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that the owning shell apply the current Ring Support preview.
    /// </summary>
    private void ApplyRingSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _ringSupportOptionsChangedTimer.Stop();
        RingSupportApplyRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that the owning shell cancel the current Ring Support operation and discard transient preview state.
    /// </summary>
    private void CancelRingSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _ringSupportOptionsChangedTimer.Stop();
        RingSupportCancelRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Restarts the live-preview debounce timer so expensive projected markers are recalculated only after edits settle.
    /// </summary>
    private void RestartRingSupportOptionsChangedTimer()
    {
        _ringSupportOptionsChangedTimer.Stop();
        _ringSupportOptionsChangedTimer.Start();
    }

    /// <summary>
    /// Applies a spinner delta while keeping the spacing finite and positive.
    /// </summary>
    private void StepRingSupportSpacing(float delta)
    {
        float currentSpacing;

        if (!TryGetRingSupportSpacing(out currentSpacing))
        {
            currentSpacing = DefaultRingSupportSpacing;
        }

        float nextSpacing = MathF.Max(RingSupportSpacingStep, currentSpacing + delta);
        RingSupportSpacingTextBox.Text = nextSpacing.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
