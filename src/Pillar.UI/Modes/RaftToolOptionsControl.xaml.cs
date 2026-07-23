// RaftToolOptionsControl.xaml.cs
// Converts raft option edits into validated renderer-independent settings snapshots.
using Pillar.Core.Rafts;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for the procedural Raft options panel.
/// </summary>
public partial class RaftToolOptionsControl : UserControl
{
    private bool _isLoadingSettings;

    /// <summary>
    /// Creates the options panel with default Footprint settings.
    /// </summary>
    public RaftToolOptionsControl()
    {
        InitializeComponent();
        SetSettings(new RaftSettings());
    }

    public event EventHandler? OptionsChanged;
    public event EventHandler? ApplyRequested;
    public event EventHandler? CancelRequested;

    /// <summary>
    /// Loads an existing settings snapshot without requesting preview generation for every field.
    /// </summary>
    public void SetSettings(RaftSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        _isLoadingSettings = true;
        RaftTypeComboBox.SelectedIndex = (int)settings.Type;
        FootprintRaftHeightInput.Value = settings.RaftHeight;
        FeetRaftHeightInput.Value = settings.RaftHeight;
        LipHeightInput.Value = settings.LipHeight;
        LipWidthInput.Value = settings.LipWidth;
        FootprintOffsetInput.Value = settings.FootprintOffset;
        RaftThicknessInput.Value = settings.RaftThickness;
        LineThicknessInput.Value = settings.LineThickness;
        MaxSideLengthInput.Value = settings.MaxSideLength;
        FootSizeInput.Value = settings.FootSize;
        EdgeAngleInput.Value = settings.EdgeAngleDegrees;
        _isLoadingSettings = false;
        UpdateTypeVisibility();
    }

    /// <summary>
    /// Reads one complete validated settings snapshot from the controls.
    /// </summary>
    public RaftSettings GetSettings()
    {
        RaftType type = (RaftType)Math.Clamp(RaftTypeComboBox.SelectedIndex, 0, 2);
        float raftHeight = type == RaftType.Feet ? (float)FeetRaftHeightInput.Value : (float)FootprintRaftHeightInput.Value;
        return new RaftSettings(
            type,
            raftHeight,
            (float)LipHeightInput.Value,
            (float)LipWidthInput.Value,
            (float)FootprintOffsetInput.Value,
            (float)RaftThicknessInput.Value,
            (float)LineThicknessInput.Value,
            (float)FootSizeInput.Value,
            (float)EdgeAngleInput.Value,
            (float)MaxSideLengthInput.Value);
    }

    /// <summary>
    /// Shows only controls relevant to the selected raft type.
    /// </summary>
    private void UpdateTypeVisibility()
    {
        int selectedIndex = Math.Clamp(RaftTypeComboBox.SelectedIndex, 0, 2);
        FootprintOptionsPanel.Visibility = selectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        MeshOptionsPanel.Visibility = selectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        FeetOptionsPanel.Visibility = selectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Updates type-specific controls and requests a live preview refresh.
    /// </summary>
    private void RaftTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (FootprintOptionsPanel == null) return;
        UpdateTypeVisibility();
        RaiseOptionsChanged();
    }

    /// <summary>
    /// Requests a live preview refresh after a numeric edit.
    /// </summary>
    private void AnyOption_Changed(object sender, EventArgs e)
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
        if (!_isLoadingSettings) OptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that the owning shell commit the preview.
    /// </summary>
    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that the owning shell restore the pre-session raft.
    /// </summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
