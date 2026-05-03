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
    private const string CircleSupportToolName = "Circle Support";
    private const float CircleSupportSpacingStep = 0.25f;
    private const int CircleSupportOptionsChangedDelayMilliseconds = 300;
    public const float DefaultCircleSupportSpacing = 5.0f;
    private readonly DispatcherTimer _circleSupportOptionsChangedTimer;
    private bool _isSynchronizingCircleSupportOptions;

    /// <summary>
    /// Raised when a Circle Support option changes and the active preview should be rebuilt.
    /// </summary>
    public event EventHandler? CircleSupportOptionsChanged;

    /// <summary>
    /// Raised when the user accepts the current Circle Support preview.
    /// </summary>
    public event EventHandler? CircleSupportApplyRequested;

    /// <summary>
    /// Raised when the user cancels the current Circle Support operation.
    /// </summary>
    public event EventHandler? CircleSupportCancelRequested;

    /// <summary>
    /// Creates the Tool Options Panel overlay.
    /// </summary>
    public ToolOptionsPanel()
    {
        _circleSupportOptionsChangedTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(CircleSupportOptionsChangedDelayMilliseconds)
        };
        _circleSupportOptionsChangedTimer.Tick += CircleSupportOptionsChangedTimer_Tick;
        InitializeComponent();
    }

    /// <summary>
    /// Updates the mock panel copy for the selected tool.
    /// </summary>
    public void SetSelectedTool(string selectedToolName)
    {
        SelectedToolNameTextBlock.Text = selectedToolName;

        if (string.Equals(selectedToolName, CircleSupportToolName, StringComparison.Ordinal))
        {
            SettingsSummaryTextBlock.Text = "Circle Support options";
            CircleSupportOptionsGrid.Visibility = Visibility.Visible;
            return;
        }

        SettingsSummaryTextBlock.Text = $"{selectedToolName} does not have options wired yet.";
        CircleSupportOptionsGrid.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Attempts to read the Circle Support spacing field in millimeters.
    /// </summary>
    public bool TryGetCircleSupportSpacing(out float spacing)
    {
        string text = CircleSupportSpacingTextBox.Text.Trim();

        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out spacing)
            && spacing > 0.0f
            && !float.IsNaN(spacing)
            && !float.IsInfinity(spacing))
        {
            return true;
        }

        spacing = DefaultCircleSupportSpacing;
        return false;
    }

    /// <summary>
    /// Sets the Circle Support spacing field without raising live-preview refresh events.
    /// </summary>
    public void SetCircleSupportSpacing(float spacing)
    {
        if (float.IsNaN(spacing) || float.IsInfinity(spacing) || spacing <= 0.0f)
        {
            spacing = DefaultCircleSupportSpacing;
        }

        _circleSupportOptionsChangedTimer.Stop();
        _isSynchronizingCircleSupportOptions = true;

        try
        {
            CircleSupportSpacingTextBox.Text = spacing.ToString("0.00", CultureInfo.InvariantCulture);
        }
        finally
        {
            _isSynchronizingCircleSupportOptions = false;
        }
    }

    /// <summary>
    /// Decreases the Circle Support spacing spinner by one step.
    /// </summary>
    private void DecreaseCircleSupportSpacingButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        StepCircleSupportSpacing(-CircleSupportSpacingStep);
    }

    /// <summary>
    /// Increases the Circle Support spacing spinner by one step.
    /// </summary>
    private void IncreaseCircleSupportSpacingButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        StepCircleSupportSpacing(CircleSupportSpacingStep);
    }

    /// <summary>
    /// Schedules an option-driven preview refresh after the user pauses editing.
    /// </summary>
    private void CircleSupportSpacingTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isSynchronizingCircleSupportOptions)
        {
            return;
        }

        RestartCircleSupportOptionsChangedTimer();
    }

    /// <summary>
    /// Raises the delayed Circle Support option change event after typing has paused.
    /// </summary>
    private void CircleSupportOptionsChangedTimer_Tick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _circleSupportOptionsChangedTimer.Stop();
        CircleSupportOptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that the owning shell apply the current Circle Support preview.
    /// </summary>
    private void ApplyCircleSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _circleSupportOptionsChangedTimer.Stop();
        CircleSupportApplyRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that the owning shell cancel the current Circle Support operation and discard transient preview state.
    /// </summary>
    private void CancelCircleSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _circleSupportOptionsChangedTimer.Stop();
        CircleSupportCancelRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Restarts the live-preview debounce timer so expensive projected markers are recalculated only after edits settle.
    /// </summary>
    private void RestartCircleSupportOptionsChangedTimer()
    {
        _circleSupportOptionsChangedTimer.Stop();
        _circleSupportOptionsChangedTimer.Start();
    }

    /// <summary>
    /// Applies a spinner delta while keeping the spacing finite and positive.
    /// </summary>
    private void StepCircleSupportSpacing(float delta)
    {
        float currentSpacing;

        if (!TryGetCircleSupportSpacing(out currentSpacing))
        {
            currentSpacing = DefaultCircleSupportSpacing;
        }

        float nextSpacing = MathF.Max(CircleSupportSpacingStep, currentSpacing + delta);
        CircleSupportSpacingTextBox.Text = nextSpacing.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
