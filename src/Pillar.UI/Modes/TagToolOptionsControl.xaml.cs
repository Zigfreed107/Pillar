// TagToolOptionsControl.xaml.cs
// Converts tag option edits and installed-font selection into validated settings.
using Pillar.Core.Tags;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for the raft Tag options panel.
/// </summary>
public partial class TagToolOptionsControl : UserControl
{
    private bool _isLoadingSettings;
    private bool _isUpdatingDimensionMinimums;
    private bool _areControlsInitialized;

    /// <summary>
    /// Creates the options panel and lists every installed WPF font family.
    /// </summary>
    public TagToolOptionsControl()
    {
        InitializeComponent();
        _areControlsInitialized = true;
        PopulateFontFamilies();
        SetSettings(new TagSettings());
    }

    public event EventHandler? OptionsChanged;
    public event EventHandler? PlaceRequested;
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Loads one durable settings snapshot and falls back to Arial when its saved font is unavailable.
    /// </summary>
    public void SetSettings(TagSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        _isLoadingSettings = true;
        TagHeightInput.Value = settings.TagHeight;
        EdgeAngleInput.Value = settings.EdgeAngleDegrees;
        BorderOffsetInput.Value = settings.BorderOffset;
        TagTextBox.Text = settings.Text;
        FontComboBox.SelectedItem = ResolveAvailableFontFamily(settings.FontFamilyName);
        FontSizeInput.Value = settings.FontSize;
        TextHeightInput.Value = settings.TextHeight;
        FlipTextButton.IsChecked = settings.IsTextFlipped;
        EnsureDimensionMinimums();
        OuterWidthInput.Value = settings.OuterWidth;
        InnerWidthInput.Value = settings.InnerWidth;
        _isLoadingSettings = false;
        SetPlacementMode(false);
    }

    /// <summary>
    /// Reads one complete validated settings snapshot from the controls.
    /// </summary>
    public TagSettings GetSettings()
    {
        string fontFamilyName = FontComboBox.SelectedItem as string
            ?? TagSettings.DefaultFontFamilyName;
        return new TagSettings(
            (float)TagHeightInput.Value,
            (float)EdgeAngleInput.Value,
            (float)BorderOffsetInput.Value,
            TagTextBox.Text,
            fontFamilyName,
            (float)FontSizeInput.Value,
            (float)TextHeightInput.Value,
            FlipTextButton.IsChecked == true,
            (float)OuterWidthInput.Value,
            (float)InnerWidthInput.Value);
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
                && string.Equals(fontFamilyName, TagSettings.DefaultFontFamilyName, StringComparison.CurrentCultureIgnoreCase))
            {
                return fontFamilyName;
            }
        }

        return FontComboBox.Items.Count > 0
            ? (string)FontComboBox.Items[0]
            : TagSettings.DefaultFontFamilyName;
    }

    /// <summary>
    /// Requests a locked-preview refresh after a numeric edit.
    /// </summary>
    private void AnyOption_Changed(object sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isUpdatingDimensionMinimums)
        {
            return;
        }

        EnsureDimensionMinimums();
        RaiseOptionsChanged();
    }

    /// <summary>
    /// Maintains the dependent Border Offset and Outer Width constraints after numeric edits.
    /// </summary>
    private void EnsureDimensionMinimums()
    {
        if (!_areControlsInitialized)
        {
            return;
        }

        _isUpdatingDimensionMinimums = true;
        double minimumBorderOffset = TagHeightInput.Value;
        BorderOffsetInput.Minimum = minimumBorderOffset;

        if (BorderOffsetInput.Value < minimumBorderOffset)
        {
            BorderOffsetInput.Value = minimumBorderOffset;
        }

        double minimumOuterWidth = FontSizeInput.Value + BorderOffsetInput.Value;
        OuterWidthInput.Minimum = minimumOuterWidth;

        if (OuterWidthInput.Value < minimumOuterWidth)
        {
            OuterWidthInput.Value = minimumOuterWidth;
        }

        _isUpdatingDimensionMinimums = false;
    }

    /// <summary>
    /// Requests a locked-preview refresh after text changes.
    /// </summary>
    private void AnyTextOption_Changed(object sender, TextChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RaiseOptionsChanged();
    }

    /// <summary>
    /// Requests a locked-preview refresh after the selected font changes.
    /// </summary>
    private void AnySelectionOption_Changed(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RaiseOptionsChanged();
    }

    /// <summary>
    /// Requests a locked-preview refresh after the text orientation is toggled.
    /// </summary>
    private void FlipTextButton_Changed(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        RaiseOptionsChanged();
    }

    /// <summary>
    /// Publishes a preview refresh unless controls are loading a settings snapshot.
    /// </summary>
    private void RaiseOptionsChanged()
    {
        if (!_isLoadingSettings)
        {
            OptionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Requests pointer-driven closest-edge placement.
    /// </summary>
    private void PlaceButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        PlaceRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that the owning shell finish the current tag session.
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
