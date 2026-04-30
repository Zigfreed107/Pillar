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
    public const float DefaultCircleSupportSpacing = 5.0f;

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
}
