// DirectEditToolOptionsControl.xaml.cs
// Publishes Direct Edit angle-highlighting changes and close requests to the shell.
using System;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for Direct Edit tool options.
/// </summary>
public partial class DirectEditToolOptionsControl : UserControl
{
    /// <summary>
    /// Creates the Direct Edit options control.
    /// </summary>
    public DirectEditToolOptionsControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Raised when the support head and branch highlight threshold changes.
    /// </summary>
    public event EventHandler? HighlightAngleChanged;

    /// <summary>
    /// Raised when the user asks to move selected support bases to the build plate.
    /// </summary>
    public event EventHandler? MoveBaseToBuildPlateRequested;

    /// <summary>
    /// Raised when the user asks to connect selected support bases to the model.
    /// </summary>
    public event EventHandler? ConnectBaseToModelRequested;

    /// <summary>
    /// Raised when the user closes the Direct Edit session.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Gets or sets the current angle threshold in degrees from the XY plane.
    /// </summary>
    public double HighlightAngleDegrees
    {
        get { return HighlightAngleControl.Value; }
        set { HighlightAngleControl.Value = value; }
    }

    /// <summary>
    /// Publishes a validated numeric angle change.
    /// </summary>
    private void HighlightAngleControl_ValueChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        HighlightAngleChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Publishes the selected-support build-plate conversion request.
    /// </summary>
    private void MoveBaseToBuildPlateButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        MoveBaseToBuildPlateRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Publishes the selected-support model-connection request.
    /// </summary>
    private void ConnectBaseToModelButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ConnectBaseToModelRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Publishes the close request to the shell.
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
