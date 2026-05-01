// MainWindow.Commands.cs
// Handles shell-level keyboard shortcuts and command-history UI so document commands remain centralized without crowding setup and interaction code.
using Pillar.Commands;
using Pillar.UI.Modes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Pillar.UI;

public partial class MainWindow
{
    /// <summary>
    /// Creates a new blank document after optionally saving the current document.
    /// </summary>
    private void NewProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ApplyDocumentFileResult(_documentFileService.New());
    }

    /// <summary>
    /// Opens a saved Graphite project file and replaces the current document after confirmation.
    /// </summary>
    private void OpenProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ApplyDocumentFileResult(_documentFileService.Open());
    }

    /// <summary>
    /// Saves the current document to a Graphite project file selected by the user.
    /// </summary>
    private void SaveProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ApplyDocumentFileResult(_documentFileService.Save());
    }

    /// <summary>
    /// Handles workspace keyboard shortcuts that cancel transient tool state.
    /// </summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (IsControlShortcut(e, Key.Z))
        {
            if (IsKeyboardFocusInsideEditableControl())
            {
                return;
            }

            UndoLastCommand();
            e.Handled = true;
            return;
        }

        if (IsControlShortcut(e, Key.Y))
        {
            if (IsKeyboardFocusInsideEditableControl())
            {
                return;
            }

            RedoLastCommand();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape)
        {
            return;
        }

        _toolManager.CancelActiveTool();
        SetActiveMode(WorkspaceModeId.Select);
        e.Handled = true;
    }

    /// <summary>
    /// Undoes the most recent document command from the toolbar.
    /// </summary>
    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        UndoLastCommand();
    }

    /// <summary>
    /// Redoes the most recent undone document command from the toolbar.
    /// </summary>
    private void RedoButton_Click(object sender, RoutedEventArgs e)
    {
        RedoLastCommand();
    }

    /// <summary>
    /// Executes undo and writes the applied command name to the status bar.
    /// </summary>
    private void UndoLastCommand()
    {
        ICadCommand? command = _commandRunner.Undo();

        if (command == null)
        {
            return;
        }

        _layerPanelViewModel.RefreshFromDocument();
        _viewModel.SetStatusText($"Undid {command.DisplayName}");
    }

    /// <summary>
    /// Executes redo and writes the applied command name to the status bar.
    /// </summary>
    private void RedoLastCommand()
    {
        ICadCommand? command = _commandRunner.Redo();

        if (command == null)
        {
            return;
        }

        _layerPanelViewModel.RefreshFromDocument();
        _viewModel.SetStatusText($"Redid {command.DisplayName}");
    }

    /// <summary>
    /// Updates the toolbar buttons from the central command history state.
    /// </summary>
    private void UpdateUndoRedoButtonState()
    {
        UndoButton.IsEnabled = _commandRunner.CanUndo;
        RedoButton.IsEnabled = _commandRunner.CanRedo;
    }

    /// <summary>
    /// Checks whether the current key event matches a control-key shortcut.
    /// </summary>
    private static bool IsControlShortcut(KeyEventArgs e, Key shortcutKey)
    {
        return e.Key == shortcutKey
            && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
    }

    /// <summary>
    /// Checks whether keyboard focus is currently inside a control that owns its own text undo and redo.
    /// </summary>
    private static bool IsKeyboardFocusInsideEditableControl()
    {
        DependencyObject? focusedElement = Keyboard.FocusedElement as DependencyObject;

        while (focusedElement != null)
        {
            if (focusedElement is TextBoxBase || focusedElement is PasswordBox)
            {
                return true;
            }

            if (focusedElement is ComboBox comboBox && comboBox.IsEditable)
            {
                return true;
            }

            focusedElement = GetUiParent(focusedElement);
        }

        return false;
    }

    /// <summary>
    /// Gets the logical or visual parent used when walking from the focused element back to the window.
    /// </summary>
    private static DependencyObject? GetUiParent(DependencyObject element)
    {
        DependencyObject? logicalParent = LogicalTreeHelper.GetParent(element);

        if (logicalParent != null)
        {
            return logicalParent;
        }

        if (element is Visual || element is Visual3D)
        {
            return VisualTreeHelper.GetParent(element);
        }

        return null;
    }
}
