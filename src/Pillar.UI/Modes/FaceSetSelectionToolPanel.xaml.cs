// FaceSetSelectionToolPanel.xaml.cs
// Raises face-set selection UI intents while keeping panel controls independent from viewport hit-testing.
using Pillar.Rendering.Tools;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.Modes;

/// <summary>
/// Floating panel for editing a temporary mesh-face selection set.
/// </summary>
public partial class FaceSetSelectionToolPanel : UserControl
{
    private bool _isSynchronizingControls;

    /// <summary>
    /// Creates the panel and initializes the default add/select state.
    /// </summary>
    public FaceSetSelectionToolPanel()
    {
        InitializeComponent();
        SetToolKind(FaceSetSelectionToolKind.Select);
        SetModifier(FaceSetSelectionModifier.Add);
    }

    /// <summary>
    /// Raised when the user chooses one face selection operation.
    /// </summary>
    public event Action<FaceSetSelectionToolKind>? ToolKindChanged;

    /// <summary>
    /// Raised when the user chooses add or remove selection behavior.
    /// </summary>
    public event Action<FaceSetSelectionModifier>? ModifierChanged;

    /// <summary>
    /// Raised when the coplanar threshold changes.
    /// </summary>
    public event Action<double>? CoplanarThresholdChanged;

    /// <summary>
    /// Raised when the user requests an undo of the temporary face selection.
    /// </summary>
    public event Action? UndoRequested;

    /// <summary>
    /// Raised when the user requests a redo of the temporary face selection.
    /// </summary>
    public event Action? RedoRequested;

    /// <summary>
    /// Raised when the user clears the temporary selection.
    /// </summary>
    public event Action? ClearRequested;

    /// <summary>
    /// Raised when the user accepts the temporary selection.
    /// </summary>
    public event Action? Accepted;

    /// <summary>
    /// Gets or sets the numeric coplanar threshold shown in the angle-select flyout.
    /// </summary>
    public double CoplanarThresholdDegrees
    {
        get { return CoplanarThresholdNumericUpDown.Value; }
        set { CoplanarThresholdNumericUpDown.Value = Math.Clamp(value, 0.0, 180.0); }
    }

    /// <summary>
    /// Mirrors the active operation into the linked tool buttons.
    /// </summary>
    public void SetToolKind(FaceSetSelectionToolKind toolKind)
    {
        _isSynchronizingControls = true;

        try
        {
            SelectToolButton.IsChecked = toolKind == FaceSetSelectionToolKind.Select;
            LineSelectToolButton.IsChecked = toolKind == FaceSetSelectionToolKind.LineSelect;
            AngleSelectToolButton.IsChecked = toolKind == FaceSetSelectionToolKind.AngleSelect;
            ToolPromptTextBlock.Text = GetPromptText(toolKind);
        }
        finally
        {
            _isSynchronizingControls = false;
        }
    }

    /// <summary>
    /// Mirrors the active add/remove behavior into the linked modifier buttons.
    /// </summary>
    public void SetModifier(FaceSetSelectionModifier modifier)
    {
        _isSynchronizingControls = true;

        try
        {
            AddModifierButton.IsChecked = modifier == FaceSetSelectionModifier.Add;
            RemoveModifierButton.IsChecked = modifier == FaceSetSelectionModifier.Remove;
        }
        finally
        {
            _isSynchronizingControls = false;
        }
    }

    /// <summary>
    /// Updates the face count and undo button availability.
    /// </summary>
    public void UpdateState(int selectedFaceCount, bool canUndo, bool canRedo)
    {
        SelectionCountTextBlock.Text = selectedFaceCount == 1
            ? "1 face"
            : $"{selectedFaceCount} faces";
        UndoFaceSelectionButton.IsEnabled = canUndo;
        RedoFaceSelectionButton.IsEnabled = canRedo;
    }

    /// <summary>
    /// Selects the direct face-pick operation.
    /// </summary>
    private void SelectToolButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        PublishToolKind(FaceSetSelectionToolKind.Select);
    }

    /// <summary>
    /// Selects the polyline screen-crossing operation.
    /// </summary>
    private void LineSelectToolButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        PublishToolKind(FaceSetSelectionToolKind.LineSelect);
    }

    /// <summary>
    /// Selects the connected coplanar angle-grow operation.
    /// </summary>
    private void AngleSelectToolButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        PublishToolKind(FaceSetSelectionToolKind.AngleSelect);
    }

    /// <summary>
    /// Opens or closes the angle-select settings flyout.
    /// </summary>
    private void AngleSelectSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        AngleSelectSettingsPopup.IsOpen = !AngleSelectSettingsPopup.IsOpen;
        AngleSelectSettingsButton.IsChecked = AngleSelectSettingsPopup.IsOpen;
    }

    /// <summary>
    /// Keeps the flyout arrow visual state synchronized when the popup closes externally.
    /// </summary>
    private void AngleSelectSettingsPopup_Closed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        AngleSelectSettingsButton.IsChecked = false;
    }

    /// <summary>
    /// Publishes numeric threshold changes to the active tool session.
    /// </summary>
    private void CoplanarThresholdNumericUpDown_ValueChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isSynchronizingControls)
        {
            return;
        }

        CoplanarThresholdChanged?.Invoke(CoplanarThresholdDegrees);
    }

    /// <summary>
    /// Selects additive modification for candidate faces.
    /// </summary>
    private void AddModifierButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        PublishModifier(FaceSetSelectionModifier.Add);
    }

    /// <summary>
    /// Selects subtractive modification for candidate faces.
    /// </summary>
    private void RemoveModifierButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        PublishModifier(FaceSetSelectionModifier.Remove);
    }

    /// <summary>
    /// Requests clearing the temporary selection.
    /// </summary>
    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ClearRequested?.Invoke();
    }

    /// <summary>
    /// Requests session-local undo.
    /// </summary>
    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        UndoRequested?.Invoke();
    }

    /// <summary>
    /// Requests session-local redo.
    /// </summary>
    private void RedoButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        RedoRequested?.Invoke();
    }

    /// <summary>
    /// Accepts the selection and closes the helper through the launcher.
    /// </summary>
    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        Accepted?.Invoke();
    }

    /// <summary>
    /// Applies and publishes a tool kind without allowing all linked buttons to become unchecked.
    /// </summary>
    private void PublishToolKind(FaceSetSelectionToolKind toolKind)
    {
        if (_isSynchronizingControls)
        {
            return;
        }

        SetToolKind(toolKind);
        ToolKindChanged?.Invoke(toolKind);
    }

    /// <summary>
    /// Applies and publishes a modifier without allowing both linked modifier buttons to become unchecked.
    /// </summary>
    private void PublishModifier(FaceSetSelectionModifier modifier)
    {
        if (_isSynchronizingControls)
        {
            return;
        }

        SetModifier(modifier);
        ModifierChanged?.Invoke(modifier);
    }

    /// <summary>
    /// Gets the short workflow prompt shown at the bottom of the panel for the active operation.
    /// </summary>
    private static string GetPromptText(FaceSetSelectionToolKind toolKind)
    {
        if (toolKind == FaceSetSelectionToolKind.LineSelect)
        {
            return "Press ESC to finish";
        }

        return "Click on a face";
    }
}
