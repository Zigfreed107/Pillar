// SupportBracingToolOptionsControl.xaml.cs
// Converts Brace and Buttress tool option edits into validated renderer-independent modifier settings.
using Pillar.Core.Layers;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for Brace and Buttress support editing options.
/// </summary>
public partial class SupportBracingToolOptionsControl : UserControl
{
    /// <summary>
    /// Creates the support bracing options control.
    /// </summary>
    public SupportBracingToolOptionsControl()
    {
        InitializeComponent();
        SetBraceSettings(SupportBraceModifierSettings.CreateDefault(), false);
        SetButtressSettings(SupportButtressModifierSettings.CreateDefault(), false);
    }

    public event EventHandler? OptionsChanged;
    public event EventHandler? BraceSelectedRequested;
    public event EventHandler? BraceAllRequested;
    public event EventHandler? RemoveBracingFromSelectedRequested;
    public event EventHandler? ButtressSelectedRequested;
    public event EventHandler? ButtressAllRequested;
    public event EventHandler? RemoveButtressingFromSelectedRequested;
    public event EventHandler? RemoveAllBracingRequested;
    public event EventHandler? RemoveAllButtressesRequested;
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Gets the modifier kind whose removal actions are currently enabled.
    /// </summary>
    public SupportModifierKind? EditingModifierKind { get; private set; }

    /// <summary>
    /// Restores brace settings into the visible controls.
    /// </summary>
    public void SetBraceSettings(SupportBraceModifierSettings settings, bool isEditingExistingModifier)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        MinimumBraceAngleTextBox.Text = settings.MinimumBraceAngleDegrees.ToString("0.##", CultureInfo.InvariantCulture);
        MaximumBraceAngleTextBox.Text = settings.MaximumBraceAngleDegrees.ToString("0.##", CultureInfo.InvariantCulture);
        MaximumBraceLengthTextBox.Text = settings.MaximumBraceLength.ToString("0.##", CultureInfo.InvariantCulture);
        BraceDiameterTextBox.Text = settings.BraceDiameter.ToString("0.##", CultureInfo.InvariantCulture);
        if (isEditingExistingModifier)
        {
            SetEditingModifierKind(SupportModifierKind.Brace);
        }
        else if (EditingModifierKind == SupportModifierKind.Brace)
        {
            SetEditingModifierKind(null);
        }
    }

    /// <summary>
    /// Restores buttress settings into the visible controls.
    /// </summary>
    public void SetButtressSettings(SupportButtressModifierSettings settings, bool isEditingExistingModifier)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        MinimumButtressHeightTextBox.Text = settings.MinimumButtressHeight.ToString("0.##", CultureInfo.InvariantCulture);
        ButtressSpacingTextBox.Text = settings.ButtressSpacing.ToString("0.##", CultureInfo.InvariantCulture);
        if (isEditingExistingModifier)
        {
            SetEditingModifierKind(SupportModifierKind.Buttress);
        }
        else if (EditingModifierKind == SupportModifierKind.Buttress)
        {
            SetEditingModifierKind(null);
        }
    }

    /// <summary>
    /// Updates modifier-only actions without replacing the values currently entered in the panel.
    /// </summary>
    public void SetEditingModifierKind(SupportModifierKind? modifierKind)
    {
        EditingModifierKind = modifierKind;
        RemoveBracingFromSelectedButton.IsEnabled = modifierKind == SupportModifierKind.Brace;
        RemoveButtressingFromSelectedButton.IsEnabled = modifierKind == SupportModifierKind.Buttress;
    }

    /// <summary>
    /// Enables modifier-only actions for every operation kind stored in the active tool session.
    /// </summary>
    public void SetEditingModifierKinds(bool hasBraceActions, bool hasButtressActions)
    {
        EditingModifierKind = hasButtressActions
            ? SupportModifierKind.Buttress
            : hasBraceActions
                ? SupportModifierKind.Brace
                : null;
        RemoveBracingFromSelectedButton.IsEnabled = hasBraceActions;
        RemoveButtressingFromSelectedButton.IsEnabled = hasButtressActions;
    }
    /// <summary>
    /// Enables or disables selection-based bracing actions.
    /// </summary>
    public void SetBraceSelectedEnabled(bool isEnabled)
    {
        BraceSelectedButton.IsEnabled = isEnabled;
    }

    /// <summary>
    /// Enables or disables all-target bracing actions.
    /// </summary>
    public void SetBraceAllEnabled(bool isEnabled)
    {
        BraceAllButton.IsEnabled = isEnabled;
    }

    /// <summary>
    /// Enables or disables selection-based buttress actions.
    /// </summary>
    public void SetButtressSelectedEnabled(bool isEnabled)
    {
        ButtressSelectedButton.IsEnabled = isEnabled;
    }

    /// <summary>
    /// Enables or disables all-target buttress actions.
    /// </summary>
    public void SetButtressAllEnabled(bool isEnabled)
    {
        ButtressAllButton.IsEnabled = isEnabled;
    }

    /// <summary>
    /// Enables or disables removing every Brace modifier in the active support layer.
    /// </summary>
    public void SetRemoveAllBracingEnabled(bool isEnabled)
    {
        RemoveAllBracingButton.IsEnabled = isEnabled;
    }

    /// <summary>
    /// Enables or disables removing every Buttress modifier in the active support layer.
    /// </summary>
    public void SetRemoveAllButtressesEnabled(bool isEnabled)
    {
        RemoveAllButtressesButton.IsEnabled = isEnabled;
    }

    /// <summary>
    /// Updates visible diagnostics.
    /// </summary>
    public void SetStatusText(string statusText)
    {
        StatusTextBlock.Text = statusText;
    }

    /// <summary>
    /// Attempts to build validated Brace modifier settings from the controls.
    /// </summary>
    public bool TryGetBraceSettings(out SupportBraceModifierSettings settings, out string errorMessage)
    {
        settings = SupportBraceModifierSettings.CreateDefault();
        errorMessage = string.Empty;

        if (!TryReadFloat(MinimumBraceAngleTextBox, out float minimumAngle)
            || !TryReadFloat(MaximumBraceAngleTextBox, out float maximumAngle)
            || !TryReadFloat(MaximumBraceLengthTextBox, out float maximumLength)
            || !TryReadFloat(BraceDiameterTextBox, out float diameter))
        {
            errorMessage = "Brace settings contain a value that is not a valid number.";
            return false;
        }

        try
        {
            settings = new SupportBraceModifierSettings(minimumAngle, maximumAngle, maximumLength, diameter);
            return true;
        }
        catch (ArgumentException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Attempts to build validated Buttress modifier settings from the controls.
    /// </summary>
    public bool TryGetButtressSettings(out SupportButtressModifierSettings settings, out string errorMessage)
    {
        settings = SupportButtressModifierSettings.CreateDefault();
        errorMessage = string.Empty;

        if (!TryReadFloat(MinimumButtressHeightTextBox, out float minimumHeight)
            || !TryReadFloat(ButtressSpacingTextBox, out float spacing))
        {
            errorMessage = "Buttress settings contain a value that is not a valid number.";
            return false;
        }

        try
        {
            settings = new SupportButtressModifierSettings(minimumHeight, spacing);
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
    private static bool TryReadFloat(TextBox textBox, out float value)
    {
        return float.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private void AnyOption_Changed(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BraceSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        BraceSelectedRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BraceAllButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        BraceAllRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveBracingFromSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        RemoveBracingFromSelectedRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ButtressSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ButtressSelectedRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ButtressAllButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ButtressAllRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveButtressingFromSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        RemoveButtressingFromSelectedRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveAllBracingButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        RemoveAllBracingRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveAllButtressesButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        RemoveAllButtressesRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}