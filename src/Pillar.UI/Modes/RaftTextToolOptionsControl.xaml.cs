// RaftTextToolOptionsControl.xaml.cs
// Converts raft text option edits and installed-font selection into validated settings.
using Pillar.Core.RaftTexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for the Raft Text options panel.
/// </summary>
public partial class RaftTextToolOptionsControl : UserControl
{
    private bool _isLoadingSettings;

    /// <summary>
    /// Creates the options panel and lists every installed WPF font family.
    /// </summary>
    public RaftTextToolOptionsControl()
    {
        InitializeComponent();
        PopulateFontFamilies();
        SetSettings(new RaftTextSettings());
    }

    public event EventHandler? OptionsChanged;
    public event EventHandler? PlaceRequested;
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Loads one durable settings snapshot and falls back to Arial when its saved font is unavailable.
    /// </summary>
    public void SetSettings(RaftTextSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        _isLoadingSettings = true;
        RaftTextBox.Text = settings.Text;
        FontComboBox.SelectedItem = ResolveAvailableFontFamily(settings.FontFamilyName);
        FontSizeInput.Value = settings.FontSize;
        TextHeightInput.Value = settings.TextHeight;
        OrientationInput.Value = settings.OrientationDegrees;
        _isLoadingSettings = false;
        SetPlacementMode(false);
    }

    /// <summary>
    /// Reads one complete validated settings snapshot from the controls.
    /// </summary>
    public RaftTextSettings GetSettings()
    {
        string fontFamilyName = FontComboBox.SelectedItem as string
            ?? RaftTextSettings.DefaultFontFamilyName;
        return new RaftTextSettings(
            text: RaftTextBox.Text,
            fontFamilyName: fontFamilyName,
            fontSize: (float)FontSizeInput.Value,
            textHeight: (float)TextHeightInput.Value,
            borderOffset: RaftTextSettings.DefaultBorderOffset,
            orientationDegrees: (float)OrientationInput.Value);
    }

    /// <summary>
    /// Switches between editable controls and the pointer-placement instruction.
    /// </summary>
    public void SetPlacementMode(bool isPlacing)
    {
        OptionsPanel.Visibility = isPlacing ? Visibility.Collapsed : Visibility.Visible;
        PlacementInstructionsPanel.Visibility = isPlacing ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Populates a stable alphabetic list from fonts installed on the current computer.
    /// </summary>
    private void PopulateFontFamilies()
    {
        List<string> fontFamilyNames = Fonts.SystemFontFamilies
            .Select((FontFamily family) => family.Source)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy((string name) => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        FontComboBox.ItemsSource = fontFamilyNames;
    }

    /// <summary>
    /// Resolves a saved font name or the required Arial fallback.
    /// </summary>
    private string ResolveAvailableFontFamily(string requestedFontFamilyName)
    {
        foreach (object item in FontComboBox.Items)
        {
            if (item is string fontFamilyName
                && string.Equals(fontFamilyName, requestedFontFamilyName, StringComparison.CurrentCultureIgnoreCase))
            {
                return fontFamilyName;
            }
        }

        foreach (object item in FontComboBox.Items)
        {
            if (item is string fontFamilyName
                && string.Equals(fontFamilyName, RaftTextSettings.DefaultFontFamilyName, StringComparison.CurrentCultureIgnoreCase))
            {
                return fontFamilyName;
            }
        }

        return FontComboBox.Items.Count > 0
            ? (string)FontComboBox.Items[0]
            : RaftTextSettings.DefaultFontFamilyName;
    }

    /// <summary>
    /// Publishes numeric changes after initialization completes.
    /// </summary>
    private void AnyOption_Changed(object sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        RaiseOptionsChanged();
    }

    /// <summary>
    /// Publishes text edits after initialization completes.
    /// </summary>
    private void AnyTextOption_Changed(object sender, TextChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RaiseOptionsChanged();
    }

    /// <summary>
    /// Publishes font changes after initialization completes.
    /// </summary>
    private void AnySelectionOption_Changed(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RaiseOptionsChanged();
    }

    /// <summary>
    /// Raises a single settings-change notification.
    /// </summary>
    private void RaiseOptionsChanged()
    {
        if (!_isLoadingSettings)
        {
            OptionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Requests pointer placement from the owning shell.
    /// </summary>
    private void PlaceButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        PlaceRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests completion from the owning shell.
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
