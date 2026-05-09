// SupportPresetEditorWindow.xaml.cs
// Implements the floating support preset editor and persists changes through SupportPresetService.
using Pillar.Core.Supports;
using Pillar.UI.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for the support preset editor window.
/// </summary>
public partial class SupportPresetEditorWindow : Window
{
    private readonly SupportPresetService _supportPresetService;
    private bool _isSynchronizingSelection;

    /// <summary>
    /// Creates the editor window for the supplied support preset service.
    /// </summary>
    public SupportPresetEditorWindow(SupportPresetService supportPresetService)
    {
        _supportPresetService = supportPresetService ?? throw new ArgumentNullException(nameof(supportPresetService));
        InitializeComponent();
        PresetComboBox.ItemsSource = _supportPresetService.Presets;
        SelectPreset(_supportPresetService.SelectedPreset);
    }

    /// <summary>
    /// Selects one preset in the editor and loads its dimensions into the controls.
    /// </summary>
    private void SelectPreset(SupportPreset preset)
    {
        _isSynchronizingSelection = true;

        try
        {
            PresetComboBox.SelectedItem = preset;
            PresetComboBox.Text = preset.Name;
            LoadProfile(preset.Profile);
        }
        finally
        {
            _isSynchronizingSelection = false;
        }
    }

    /// <summary>
    /// Loads one support profile into the numeric editor controls.
    /// </summary>
    private void LoadProfile(SupportProfile profile)
    {
        BaseBottomRadiusInput.Value = profile.BaseBottomRadius;
        BaseHeightInput.Value = profile.BaseHeight;
        StemBottomDiameterInput.Value = profile.StemBottomDiameter;
        StemTopDiameterInput.Value = profile.StemTopDiameter;
        HeadHeightInput.Value = profile.HeadHeight;
        HeadPenetrationDepthInput.Value = profile.HeadPenetrationDepth;
        HeadTopDiameterInput.Value = profile.HeadTopDiameter;
    }

    /// <summary>
    /// Builds a support profile from the current numeric editor values.
    /// </summary>
    private SupportProfile CreateProfileFromControls()
    {
        return new SupportProfile(
            (float)BaseBottomRadiusInput.Value,
            (float)BaseHeightInput.Value,
            (float)StemBottomDiameterInput.Value,
            (float)StemTopDiameterInput.Value,
            (float)HeadHeightInput.Value,
            (float)HeadPenetrationDepthInput.Value,
            (float)HeadTopDiameterInput.Value);
    }

    /// <summary>
    /// Saves the current dimensions under the combo box text or selected preset name.
    /// </summary>
    private SupportPreset SaveCurrentPreset()
    {
        string presetName = PresetComboBox.Text;

        if (PresetComboBox.SelectedItem is SupportPreset selectedPreset && string.IsNullOrWhiteSpace(presetName))
        {
            presetName = selectedPreset.Name;
        }

        SupportPreset savedPreset = _supportPresetService.SavePreset(presetName, CreateProfileFromControls());
        SelectPreset(savedPreset);
        return savedPreset;
    }

    /// <summary>
    /// Loads values when the user selects an existing preset.
    /// </summary>
    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isSynchronizingSelection)
        {
            return;
        }

        if (PresetComboBox.SelectedItem is SupportPreset preset)
        {
            _supportPresetService.SelectPreset(preset);
            SelectPreset(preset);
        }
    }

    /// <summary>
    /// Saves the current editor values without closing the window.
    /// </summary>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        SaveCurrentPreset();
    }

    /// <summary>
    /// Saves the current editor values and closes the floating window.
    /// </summary>
    private void SaveAndCloseButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        SaveCurrentPreset();
        Close();
    }
}
