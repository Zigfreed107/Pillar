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
        SetClusterSettings(SupportClusterModifierSettings.CreateDefault(), SupportModifierScope.WholeLayer, false);
    }

    /// <summary>
    /// Raised when options change and the shell may refresh preview diagnostics.
    /// </summary>
    public event EventHandler? OptionsChanged;

    /// <summary>
    /// Raised when the user applies the current settings.
    /// </summary>
    public event EventHandler? ApplyRequested;

    /// <summary>
    /// Raised when the user removes the modifier currently being edited.
    /// </summary>
    public event EventHandler? RemoveAllRequested;

    /// <summary>
    /// Raised when the user closes the tool options.
    /// </summary>
    /// <summary>
    /// Raised when the user requests that selected clusters become individual supports again.
    /// </summary>
    public event EventHandler? UnclusterSelectedRequested;

    public event EventHandler? CloseRequested;

    /// <summary>
    /// Gets whether live preview is enabled.
    /// </summary>
    public bool IsPreviewEnabled
    {
        get { return PreviewCheckBox.IsChecked == true; }
    }

    /// <summary>
    /// Gets the selected modifier scope.
    /// </summary>
    public SupportModifierScope SelectedScope
    {
        get
        {
            return ScopeComboBox.SelectedIndex == 1
                ? SupportModifierScope.Selection
                : SupportModifierScope.WholeLayer;
        }
    }

    /// <summary>
    /// Restores settings into the visible controls.
    /// </summary>
    public void SetClusterSettings(SupportClusterModifierSettings settings, SupportModifierScope scope, bool isEditingExistingModifier)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        ScopeComboBox.SelectedIndex = scope == SupportModifierScope.Selection ? 1 : 0;
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
    /// Updates visible preview and result diagnostics.
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
    /// Forwards Apply clicks to the owning shell.
    /// </summary>
    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Forwards Remove All clicks to the owning shell.
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


