// ToolSessionOptionsControl.xaml.cs
// Provides the reusable Finish-only options panel used by active tools before they grow dedicated settings.
using System;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for a generic active-tool options panel.
/// </summary>
public partial class ToolSessionOptionsControl : UserControl
{
    /// <summary>
    /// Creates the reusable tool-session options panel.
    /// </summary>
    public ToolSessionOptionsControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Raised when the user asks to finish the active tool session.
    /// </summary>
    public event EventHandler? FinishRequested;

    /// <summary>
    /// Sets the title and short guidance text displayed for the active tool session.
    /// </summary>
    public void SetSessionText(string title, string description)
    {
        SessionTitleTextBlock.Text = string.IsNullOrWhiteSpace(title)
            ? "Tool Options"
            : title.Trim();

        SessionDescriptionTextBlock.Text = string.IsNullOrWhiteSpace(description)
            ? "This tool is active."
            : description.Trim();
    }

    /// <summary>
    /// Requests that the owning shell finish the current tool session.
    /// </summary>
    private void FinishButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        FinishRequested?.Invoke(this, EventArgs.Empty);
    }
}
