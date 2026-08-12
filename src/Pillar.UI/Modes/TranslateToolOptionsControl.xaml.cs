// TranslateToolOptionsControl.xaml.cs
// Forwards Transform Translate actions to the shell that owns selection and command history.
using System;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for the Transform Translate options panel.
/// </summary>
public partial class TranslateToolOptionsControl : UserControl
{
    /// <summary>
    /// Creates the Transform Translate options control.
    /// </summary>
    public TranslateToolOptionsControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Raised when the user asks to place the selected model on the build plate.
    /// </summary>
    public event EventHandler? MoveToPlateRequested;

    /// <summary>
    /// Raised when the user asks to close the Transform Translate options.
    /// </summary>
    public event EventHandler? FinishRequested;

    /// <summary>
    /// Forwards the Move to Plate button request to the owning shell.
    /// </summary>
    private void MoveToPlateButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        MoveToPlateRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Forwards the Finish button request to the owning shell.
    /// </summary>
    private void FinishButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        FinishRequested?.Invoke(this, EventArgs.Empty);
    }
}
