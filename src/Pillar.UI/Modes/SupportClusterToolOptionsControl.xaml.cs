// SupportClusterToolOptionsControl.xaml.cs
// Converts Cluster Supports tool option edits into validated renderer-independent modifier settings.
using Pillar.Core.Layers;
using Pillar.Core.Supports;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for Cluster Supports tool options.
/// </summary>
public partial class SupportClusterToolOptionsControl : UserControl
{
    /// <summary>
    /// Creates the Cluster Supports options control.
    /// </summary>
    public SupportClusterToolOptionsControl()
    {
        InitializeComponent();
        SetClusterSettings(SupportClusterModifierSettings.CreateDefault(), false);
    }

    /// <summary>
    /// Raised when options change and the shell may refresh preview diagnostics.
    /// </summary>
    public event EventHandler? OptionsChanged;

    /// <summary>
    /// Raised when the user applies the current settings to the selected supports.
    /// </summary>
    public event EventHandler? ApplyToSelectedRequested;

    /// <summary>
    /// Raised when the user applies the current settings to the whole support layer.
    /// </summary>
    public event EventHandler? ApplyToAllRequested;

    /// <summary>
    /// Raised when the user removes the modifier currently being edited.
    /// </summary>
    public event EventHandler? RemoveAllRequested;

    /// <summary>
    /// Raised when the user requests that selected clusters become individual supports again.
    /// </summary>
    public event EventHandler? UnclusterSelectedRequested;

    /// <summary>
    /// Raised when the user closes the tool options.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Gets whether live preview is enabled.
    /// </summary>
    public bool IsPreviewEnabled
    {
        get { return PreviewCheckBox.IsChecked == true; }
    }

    /// <summary>
    /// Restores settings into the visible controls.
    /// </summary>
    public void SetClusterSettings(SupportClusterModifierSettings settings, bool isEditingExistingModifier)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        MaximumClusterRadiusTextBox.Text = settings.MaximumClusterRadius.ToString("0.##", CultureInfo.InvariantCulture);
        MinimumSupportsTextBox.Text = settings.MinimumSupportsPerCluster.ToString(CultureInfo.InvariantCulture);
        MaximumSupportsTextBox.Text = settings.MaximumSupportsPerCluster.ToString(CultureInfo.InvariantCulture);
        MaximumBranchAngleTextBox.Text = settings.MaximumBranchAngleFromVerticalDegrees.ToString("0.##", CultureInfo.InvariantCulture);
        StemSizingComboBox.SelectedIndex = settings.StemSizingMode == SupportClusterStemSizingMode.Manual ? 1 : 0;
        CentralStemBottomDiameterTextBox.Text = settings.ManualCentralStemBottomDiameter.ToString("0.##", CultureInfo.InvariantCulture);
        CentralStemTopDiameterTextBox.Text = settings.ManualCentralStemTopDiameter.ToString("0.##", CultureInfo.InvariantCulture);
        ClusterBranchDiameterTextBox.Text = settings.ClusterBranchDiameter.ToString("0.##", CultureInfo.InvariantCulture);
        RemoveAllButton.IsEnabled = isEditingExistingModifier;
        UpdateManualSizingEnabled();
    }

    /// <summary>
    /// Writes calculated automatic diameters into the disabled manual fields.
    /// </summary>
    public void SetAutomaticDiameters(float bottomDiameter, float topDiameter)
    {
        if (StemSizingComboBox.SelectedIndex == 1)
        {
            return;
        }

        CentralStemBottomDiameterTextBox.Text = bottomDiameter.ToString("0.##", CultureInfo.InvariantCulture);
        CentralStemTopDiameterTextBox.Text = topDiameter.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Enables or disables the Apply to Selected action from current viewport selection state.
    /// </summary>
    public void SetApplyToSelectedEnabled(bool isEnabled)
    {
        ApplyToSelectedButton.IsEnabled = isEnabled;
    }

    /// <summary>
    /// Enables or disables the Apply to All action from the selected support layer state.
    /// </summary>
    public void SetApplyToAllEnabled(bool isEnabled)
    {
        ApplyToAllButton.IsEnabled = isEnabled;
    }

    /// <summary>
    /// Enables or disables uncluster support actions from current viewport selection state.
    /// </summary>
    public void SetUnclusterSelectedEnabled(bool isEnabled)
    {
        UnclusterSelectedButton.IsEnabled = isEnabled;
    }

    /// <summary>
    /// Updates visible preview and result diagnostics.
    /// </summary>
    public void SetStatusText(string statusText)
    {
        StatusTextBlock.Text = statusText;
    }

    /// <summary>
    /// Attempts to build validated Cluster modifier settings from the controls.
    /// </summary>
    public bool TryGetClusterSettings(out SupportClusterModifierSettings settings, out string errorMessage)
    {
        settings = SupportClusterModifierSettings.CreateDefault();
        errorMessage = string.Empty;

        if (!TryReadFloat(MaximumClusterRadiusTextBox, "Maximum Cluster Radius", out float maximumClusterRadius)
            || !TryReadInt(MinimumSupportsTextBox, "Minimum Supports Per Cluster", out int minimumSupports)
            || !TryReadInt(MaximumSupportsTextBox, "Maximum Supports Per Cluster", out int maximumSupports)
            || !TryReadFloat(MaximumBranchAngleTextBox, "Maximum Branch Angle From Vertical", out float maximumBranchAngle)
            || !TryReadFloat(CentralStemBottomDiameterTextBox, "Central Stem Bottom Diameter", out float bottomDiameter)
            || !TryReadFloat(CentralStemTopDiameterTextBox, "Central Stem Top Diameter", out float topDiameter)
            || !TryReadFloat(ClusterBranchDiameterTextBox, "Cluster Branch Diameter", out float branchDiameter))
        {
            errorMessage = "Cluster settings contain a value that is not a valid number.";
            return false;
        }

        SupportClusterStemSizingMode sizingMode = StemSizingComboBox.SelectedIndex == 1
            ? SupportClusterStemSizingMode.Manual
            : SupportClusterStemSizingMode.Automatic;

        try
        {
            settings = new SupportClusterModifierSettings(
                maximumClusterRadius,
                minimumSupports,
                maximumSupports,
                maximumBranchAngle,
                sizingMode,
                bottomDiameter,
                topDiameter,
                branchDiameter);
            return true;
        }
        catch (ArgumentException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Parses one float textbox using invariant culture.
    /// </summary>
    private static bool TryReadFloat(TextBox textBox, string fieldName, out float value)
    {
        _ = fieldName;
        return float.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Parses one integer textbox using invariant culture.
    /// </summary>
    private static bool TryReadInt(TextBox textBox, string fieldName, out int value)
    {
        _ = fieldName;
        return int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Enables manual diameter editing only when Manual sizing is selected.
    /// </summary>
    private void UpdateManualSizingEnabled()
    {
        if (StemSizingComboBox == null
            || CentralStemBottomDiameterTextBox == null
            || CentralStemTopDiameterTextBox == null
            || ClusterBranchDiameterTextBox == null)
        {
            return;
        }

        bool isManual = StemSizingComboBox.SelectedIndex == 1;
        CentralStemBottomDiameterTextBox.IsEnabled = isManual;
        CentralStemTopDiameterTextBox.IsEnabled = isManual;
    }

    /// <summary>
    /// Publishes a generic option-change notification.
    /// </summary>
    private void AnyOption_Changed(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Updates manual sizing state when the sizing mode changes.
    /// </summary>
    private void StemSizingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        UpdateManualSizingEnabled();
        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Forwards Apply to Selected clicks to the owning shell.
    /// </summary>
    private void ApplyToSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyToSelectedRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Forwards Apply to All clicks to the owning shell.
    /// </summary>
    private void ApplyToAllButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyToAllRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Forwards Uncluster Selected clicks to the owning shell.
    /// </summary>
    private void UnclusterSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        UnclusterSelectedRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Forwards Remove All clicks to the owning shell.
    /// </summary>
    private void RemoveAllButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        RemoveAllRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Forwards Close clicks to the owning shell.
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
