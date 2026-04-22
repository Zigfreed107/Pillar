// LayerPanel.xaml.cs
// Converts Layer Panel WPF gestures into shell-level requests while keeping document edits in MainWindow commands.
using Pillar.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Pillar.UI.Layers;

/// <summary>
/// Interaction logic for the viewport Layer Panel overlay.
/// </summary>
public partial class LayerPanel : UserControl
{
    /// <summary>
    /// Creates the Layer Panel control.
    /// </summary>
    public LayerPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Raised when the empty-state button should run the shared model import workflow.
    /// </summary>
    public event EventHandler? ImportModelRequested;

    /// <summary>
    /// Raised when the selected imported model layer should be removed.
    /// </summary>
    public event EventHandler? RemoveModelRequested;

    /// <summary>
    /// Raised when the selected model layer should receive a new support group.
    /// </summary>
    public event EventHandler? AddSupportGroupRequested;

    /// <summary>
    /// Raised when the selected support group should be removed.
    /// </summary>
    public event EventHandler? RemoveSupportGroupRequested;

    /// <summary>
    /// Raised when a completed inline rename should be applied to a support group.
    /// </summary>
    public event EventHandler<LayerRenameRequestedEventArgs>? RenameSupportGroupRequested;

    /// <summary>
    /// Requests the shared import workflow from the owning window.
    /// </summary>
    private void ImportModelButton_Click(object sender, RoutedEventArgs e)
    {
        ImportModelRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests the shared import workflow from the toolbar model add button.
    /// </summary>
    private void AddModelButton_Click(object sender, RoutedEventArgs e)
    {
        ImportModelRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests removal of the selected imported model layer.
    /// </summary>
    private void RemoveModelButton_Click(object sender, RoutedEventArgs e)
    {
        RemoveModelRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests a new support group under the selected model layer.
    /// </summary>
    private void AddSupportGroupButton_Click(object sender, RoutedEventArgs e)
    {
        AddSupportGroupRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests removal of the selected support group layer.
    /// </summary>
    private void RemoveSupportGroupButton_Click(object sender, RoutedEventArgs e)
    {
        RemoveSupportGroupRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Keeps the view model selection synchronized with WPF's TreeView selection.
    /// </summary>
    private void LayerTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is LayerPanelViewModel layerPanelViewModel)
        {
            layerPanelViewModel.SetSelectedLayer(e.NewValue as LayerTreeItemViewModel);
        }
    }

    /// <summary>
    /// Starts inline rename editing for support group rows.
    /// </summary>
    private void RenameSupportGroupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is LayerTreeItemViewModel layer)
        {
            layer.BeginRename();
        }
    }

    /// <summary>
    /// Focuses and selects the rename text when inline editing appears.
    /// </summary>
    private void RenameTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is LayerTreeItemViewModel layer && layer.IsEditing)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }

    /// <summary>
    /// Commits inline rename editing when focus leaves the editor.
    /// </summary>
    private void RenameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitRename(sender as TextBox);
    }

    /// <summary>
    /// Commits inline rename editing on Enter and cancels it on Escape.
    /// </summary>
    private void RenameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not LayerTreeItemViewModel layer)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            layer.CancelRename();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitRename(textBox);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Converts a completed inline edit into a rename request for the owning window.
    /// </summary>
    private void CommitRename(TextBox? textBox)
    {
        if (textBox == null || textBox.DataContext is not LayerTreeItemViewModel layer || !layer.IsEditing)
        {
            return;
        }

        string requestedName = layer.EditingName;
        layer.IsEditing = false;

        if (string.Equals(layer.Name, requestedName?.Trim(), StringComparison.Ordinal))
        {
            layer.CancelRename();
            return;
        }

        RenameSupportGroupRequested?.Invoke(
            this,
            new LayerRenameRequestedEventArgs(layer.Id, layer.Name, requestedName ?? string.Empty));
    }
}

/// <summary>
/// Carries one completed support group rename request from the Layer Panel to the shell.
/// </summary>
public sealed class LayerRenameRequestedEventArgs : EventArgs
{
    /// <summary>
    /// Creates rename request data.
    /// </summary>
    public LayerRenameRequestedEventArgs(Guid supportLayerGroupId, string oldName, string newName)
    {
        SupportLayerGroupId = supportLayerGroupId;
        OldName = oldName;
        NewName = newName;
    }

    /// <summary>
    /// Gets the support group id being renamed.
    /// </summary>
    public Guid SupportLayerGroupId { get; }

    /// <summary>
    /// Gets the name displayed before the edit.
    /// </summary>
    public string OldName { get; }

    /// <summary>
    /// Gets the requested name from inline editing.
    /// </summary>
    public string NewName { get; }
}
