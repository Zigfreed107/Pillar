// ManualSupportModeOverlay.xaml.cs
// Provides the code-behind shell for the Manual Support mode overlay UserControl.
using Pillar.Core.Tools;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Pillar.UI.Modes;

/// <summary>
/// Describes a Manual Support operation selection made from the overlay.
/// </summary>
public sealed class ManualSupportOperationChangedEventArgs : EventArgs
{
    /// <summary>
    /// Creates event data for a Manual Support operation selection change.
    /// </summary>
    public ManualSupportOperationChangedEventArgs(ManualSupportOperationKind operationKind)
    {
        OperationKind = operationKind;
    }

    /// <summary>
    /// Gets the selected Manual Support operation.
    /// </summary>
    public ManualSupportOperationKind OperationKind { get; }
}

/// <summary>
/// Interaction logic for the Manual Support mode overlay.
/// </summary>
public partial class ManualSupportModeOverlay : UserControl
{
    private bool _isSynchronizingButtons;

    /// <summary>
    /// Creates the Manual Support mode overlay.
    /// </summary>
    public ManualSupportModeOverlay()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Raised when the user chooses a Manual Support operation button.
    /// </summary>
    public event EventHandler<ManualSupportOperationChangedEventArgs>? OperationChanged;

    /// <summary>
    /// Selects the operation associated with a checked operation button.
    /// </summary>
    private void OperationButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_isSynchronizingButtons)
        {
            return;
        }

        SelectOperation(GetOperationKind(sender));
    }

    /// <summary>
    /// Clears the active operation when the user untoggles the currently selected button.
    /// </summary>
    private void OperationButton_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_isSynchronizingButtons || IsAnyOperationButtonChecked())
        {
            return;
        }

        SelectOperation(ManualSupportOperationKind.None);
    }

    /// <summary>
    /// Publishes the selected operation after keeping the toggle buttons mutually exclusive.
    /// </summary>
    private void SelectOperation(ManualSupportOperationKind operationKind)
    {
        SynchronizeOperationButtons(operationKind);
        OperationChanged?.Invoke(this, new ManualSupportOperationChangedEventArgs(operationKind));
    }

    /// <summary>
    /// Updates button checked states while suppressing duplicate checked and unchecked events.
    /// </summary>
    private void SynchronizeOperationButtons(ManualSupportOperationKind operationKind)
    {
        _isSynchronizingButtons = true;
        PointOperationButton.IsChecked = operationKind == ManualSupportOperationKind.Point;
        LineOperationButton.IsChecked = operationKind == ManualSupportOperationKind.Line;
        CircleOperationButton.IsChecked = operationKind == ManualSupportOperationKind.Circle;
        _isSynchronizingButtons = false;
    }

    /// <summary>
    /// Checks whether any operation button is currently toggled down.
    /// </summary>
    private bool IsAnyOperationButtonChecked()
    {
        return IsToggleButtonChecked(PointOperationButton)
            || IsToggleButtonChecked(LineOperationButton)
            || IsToggleButtonChecked(CircleOperationButton);
    }

    /// <summary>
    /// Converts one overlay button into its Manual Support operation identifier.
    /// </summary>
    private ManualSupportOperationKind GetOperationKind(object sender)
    {
        if (ReferenceEquals(sender, PointOperationButton))
        {
            return ManualSupportOperationKind.Point;
        }

        if (ReferenceEquals(sender, LineOperationButton))
        {
            return ManualSupportOperationKind.Line;
        }

        if (ReferenceEquals(sender, CircleOperationButton))
        {
            return ManualSupportOperationKind.Circle;
        }

        return ManualSupportOperationKind.None;
    }

    /// <summary>
    /// Reads nullable ToggleButton checked state as a simple Boolean.
    /// </summary>
    private static bool IsToggleButtonChecked(ToggleButton toggleButton)
    {
        return toggleButton.IsChecked == true;
    }
}
