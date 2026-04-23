// MainWindow.SelectionAndProperties.cs
// Keeps shell-level selection feedback and properties-panel synchronization separate from viewport routing, mode switching, and file workflows.
using Pillar.Commands;
using Pillar.Core.Entities;
using System;
using System.Collections.Generic;

namespace Pillar.UI;

public partial class MainWindow
{
    /// <summary>
    /// Updates shell state when the domain selection changes.
    /// </summary>
    private void OnSelectionChanged(IEnumerable<Guid> addedIds, IEnumerable<Guid> removedIds)
    {
        _ = addedIds;
        _ = removedIds;

        if (_scene.SelectionManager.SelectedCount == 1)
        {
            Guid? selectedId = GetSingleSelectedEntityId();

            if (selectedId.HasValue)
            {
                _viewModel.SetSelectedEntity(FindEntityById(selectedId.Value));
            }

            _viewModel.SetStatusText("Object selected");
            return;
        }

        if (_scene.SelectionManager.SelectedCount > 1)
        {
            _viewModel.SetMultipleSelection(_scene.SelectionManager.SelectedCount);
            _viewModel.SetStatusText($"{_scene.SelectionManager.SelectedCount} objects selected");
            return;
        }

        _viewModel.SetSelectedEntity(null);
        _viewModel.SetStatusText(_activeToolStatusText);
    }

    /// <summary>
    /// Finds a document entity by id for shell-level selection display.
    /// </summary>
    private CadEntity? FindEntityById(Guid id)
    {
        foreach (CadEntity entity in _document.Entities)
        {
            if (entity.Id == id)
            {
                return entity;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the only selected id when selection contains exactly one entity.
    /// </summary>
    private Guid? GetSingleSelectedEntityId()
    {
        foreach (Guid selectedId in _scene.SelectionManager.SelectedEntityIds)
        {
            return selectedId;
        }

        return null;
    }

    /// <summary>
    /// Commits the selected entity name as one undoable edit instead of per keystroke.
    /// </summary>
    private void CommitSelectedEntityNameEdit()
    {
        if (_scene.SelectionManager.SelectedCount != 1)
        {
            return;
        }

        Guid? selectedId = GetSingleSelectedEntityId();

        if (!selectedId.HasValue)
        {
            return;
        }

        CadEntity? selectedEntity = FindEntityById(selectedId.Value);

        if (selectedEntity == null)
        {
            RefreshPropertiesPanelFromSelection();
            return;
        }

        string oldName = NormalizeEntityName(selectedEntity.Name);
        string newName = NormalizeEntityName(_viewModel.SelectedEntityName);

        if (string.Equals(oldName, newName, StringComparison.Ordinal))
        {
            _viewModel.SetSelectedEntity(selectedEntity);
            return;
        }

        _commandRunner.Execute(new RenameEntityCommand(selectedEntity, oldName, newName));
        _viewModel.SetSelectedEntity(selectedEntity);
        _layerPanelViewModel.RefreshFromDocument();
        _viewModel.SetStatusText("Renamed entity");
    }

    /// <summary>
    /// Refreshes the properties panel after undo or redo without changing the command status text.
    /// </summary>
    private void RefreshPropertiesPanelFromSelection()
    {
        if (_scene.SelectionManager.SelectedCount == 1)
        {
            Guid? selectedId = GetSingleSelectedEntityId();

            if (selectedId.HasValue)
            {
                _viewModel.SetSelectedEntity(FindEntityById(selectedId.Value));
                return;
            }
        }

        if (_scene.SelectionManager.SelectedCount > 1)
        {
            _viewModel.SetMultipleSelection(_scene.SelectionManager.SelectedCount);
            return;
        }

        _viewModel.SetSelectedEntity(null);
    }

    /// <summary>
    /// Normalizes user-entered entity names before comparing or applying rename commands.
    /// </summary>
    private static string NormalizeEntityName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Entity";
        }

        return name.Trim();
    }

    /// <summary>
    /// Normalizes user-entered support group names before comparing or applying rename commands.
    /// </summary>
    private static string NormalizeSupportGroupName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Supports Group";
        }

        return name.Trim();
    }
}
