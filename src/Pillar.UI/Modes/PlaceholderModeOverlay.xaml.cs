// PlaceholderModeOverlay.xaml.cs
// Provides a reusable overlay for planned workspace modes that are not active features yet.
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for an unavailable mode placeholder overlay.
/// </summary>
public partial class PlaceholderModeOverlay : UserControl
{
    public static readonly DependencyProperty ModeNameProperty =
        DependencyProperty.Register(
            nameof(ModeName),
            typeof(string),
            typeof(PlaceholderModeOverlay),
            new PropertyMetadata("Mode"));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message),
            typeof(string),
            typeof(PlaceholderModeOverlay),
            new PropertyMetadata("This mode is planned but not available yet."));

    /// <summary>
    /// Creates a placeholder overlay for an unavailable mode.
    /// </summary>
    public PlaceholderModeOverlay()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the mode name displayed by the placeholder panel.
    /// </summary>
    public string ModeName
    {
        get { return (string)GetValue(ModeNameProperty); }
        set { SetValue(ModeNameProperty, value); }
    }

    /// <summary>
    /// Gets or sets the short explanation displayed by the placeholder panel.
    /// </summary>
    public string Message
    {
        get { return (string)GetValue(MessageProperty); }
        set { SetValue(MessageProperty, value); }
    }
}
