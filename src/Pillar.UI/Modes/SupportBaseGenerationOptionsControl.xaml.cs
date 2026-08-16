// SupportBaseGenerationOptionsControl.xaml.cs
// Maps the shared support-base generation combo box to the renderer-independent placement preference.
using Pillar.Core.Supports;
using System;
using System.Windows.Controls;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for the shared support-base generation preference control.
/// </summary>
public partial class SupportBaseGenerationOptionsControl : UserControl
{
    /// <summary>
    /// Creates the selector with build-plate-only placement selected to preserve current behavior.
    /// </summary>
    public SupportBaseGenerationOptionsControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Raised when the user selects a different support-base surface preference.
    /// </summary>
    public event EventHandler? GenerationModeChanged;

    /// <summary>
    /// Gets the selected support-base surface preference.
    /// </summary>
    public SupportBaseGenerationMode GetGenerationMode()
    {
        return GenerationModeComboBox.SelectedIndex switch
        {
            1 => SupportBaseGenerationMode.ModelOnly,
            2 => SupportBaseGenerationMode.BuildPlateThenModel,
            3 => SupportBaseGenerationMode.ModelThenBuildPlate,
            _ => SupportBaseGenerationMode.BuildPlateOnly
        };
    }

    /// <summary>
    /// Selects a support-base surface preference.
    /// </summary>
    public void SetGenerationMode(SupportBaseGenerationMode generationMode)
    {
        GenerationModeComboBox.SelectedIndex = generationMode switch
        {
            SupportBaseGenerationMode.ModelOnly => 1,
            SupportBaseGenerationMode.BuildPlateThenModel => 2,
            SupportBaseGenerationMode.ModelThenBuildPlate => 3,
            _ => 0
        };
    }

    /// <summary>
    /// Reports a user-visible generation-mode selection change.
    /// </summary>
    private void GenerationModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        GenerationModeChanged?.Invoke(this, EventArgs.Empty);
    }
}
