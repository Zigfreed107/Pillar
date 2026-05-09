// SupportPresetPanel.xaml.cs
// Wires the compact support preset overlay to the preset service while keeping support tools independent from WPF controls.
using Pillar.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for the support preset overlay panel.
/// </summary>
public partial class SupportPresetPanel : UserControl
{
    private bool _isSynchronizingSelection;

    /// <summary>
    /// Creates the support preset panel.
    /// </summary>
    public SupportPresetPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Raised when the user selects a different support preset.
    /// </summary>
    public event EventHandler<SupportPresetSelectedEventArgs>? PresetSelected;

    /// <summary>
    /// Raised when the user asks to open the advanced support preset editor.
    /// </summary>
    public event EventHandler? AdvancedRequested;

    /// <summary>
    /// Binds the preset list displayed by the combo box.
    /// </summary>
    public void SetPresets(ObservableCollection<SupportPreset> presets)
    {
        PresetComboBox.ItemsSource = presets ?? throw new ArgumentNullException(nameof(presets));
    }

    /// <summary>
    /// Selects the active preset without re-raising user selection events.
    /// </summary>
    public void SelectPreset(SupportPreset preset)
    {
        _isSynchronizingSelection = true;

        try
        {
            PresetComboBox.SelectedItem = preset;
        }
        finally
        {
            _isSynchronizingSelection = false;
        }
    }

    /// <summary>
    /// Forwards user preset selection changes to the shell.
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
            PresetSelected?.Invoke(this, new SupportPresetSelectedEventArgs(preset));
        }
    }

    /// <summary>
    /// Requests the floating advanced preset editor window.
    /// </summary>
    private void AdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        AdvancedRequested?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Carries the support preset selected in the compact overlay panel.
/// </summary>
public sealed class SupportPresetSelectedEventArgs : EventArgs
{
    /// <summary>
    /// Creates event data for one selected preset.
    /// </summary>
    public SupportPresetSelectedEventArgs(SupportPreset preset)
    {
        Preset = preset ?? throw new ArgumentNullException(nameof(preset));
    }

    /// <summary>
    /// Gets the selected support preset.
    /// </summary>
    public SupportPreset Preset { get; }
}
