// ToolOptionsPanel.xaml.cs
// Provides the code-behind for the active-tool options overlay without owning tool behavior or document state.
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for the active-tool options overlay.
/// </summary>
public partial class ToolOptionsPanel : UserControl
{
    private const string CircleSupportToolName = "Circle Support";
    private const float CircleSupportSpacingStep = 0.25f;
    public const float DefaultCircleSupportSpacing = 5.0f;

    /// <summary>
    /// Raised when a Circle Support option changes and the active preview should be rebuilt.
    /// </summary>
    public event EventHandler? CircleSupportOptionsChanged;

    /// <summary>
    /// Raised when the user accepts the current Circle Support preview.
    /// </summary>
    public event EventHandler? CircleSupportApplyRequested;

    /// <summary>
    /// Creates the Tool Options Panel overlay.
    /// </summary>
    public ToolOptionsPanel()
    {
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
    /// Notifies the owner that option-driven previews should be refreshed.
    /// </summary>
    private void CircleSupportSpacingTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        CircleSupportOptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that the owning shell apply the current Circle Support preview.
    /// </summary>
    private void ApplyCircleSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        CircleSupportApplyRequested?.Invoke(this, EventArgs.Empty);
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
