// MainWindow.SelectionAndProperties.cs
// Keeps shell-level selection feedback and properties-panel synchronization separate from viewport routing, mode switching, and file workflows.
using Pillar.Commands;
using Pillar.Core.Entities;
using Pillar.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;

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
        _layerPanelViewModel.SetSelectedModelCount(GetSelectedMeshEntityCount());

        if (_scene.SelectionManager.SelectedCount == 1)
        {
            Guid? selectedId = GetSingleSelectedEntityId();

            if (selectedId.HasValue)
            {
                _viewModel.SetSelectedEntity(FindEntityById(selectedId.Value));
            }

            _viewModel.SetStatusText("Object selected");
            if (!_isSynchronizingLayerAndViewportSelection)
            {
                SynchronizeLayerPanelSelectionFromViewportSelection();
            }
            return;
        }

        if (_scene.SelectionManager.SelectedCount > 1)
        {
            _viewModel.SetMultipleSelection(_scene.SelectionManager.SelectedCount);
            _viewModel.SetStatusText($"{_scene.SelectionManager.SelectedCount} objects selected");
            if (!_isSynchronizingLayerAndViewportSelection)
            {
                SynchronizeLayerPanelSelectionFromViewportSelection();
            }
            return;
        }

        _viewModel.SetSelectedEntity(null);
        _viewModel.SetStatusText(_activeToolStatusText);
        if (!_isSynchronizingLayerAndViewportSelection)
        {
            SynchronizeLayerPanelSelectionFromViewportSelection();
        }
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
    /// Counts how many selected entities are imported mesh models.
    /// </summary>
    private int GetSelectedMeshEntityCount()
    {
        int selectedMeshCount = 0;

        foreach (Guid selectedId in _scene.SelectionManager.SelectedEntityIds)
        {
            if (FindEntityById(selectedId) is MeshEntity)
            {
                selectedMeshCount++;
            }
        }

        return selectedMeshCount;
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

    /// <summary>
    /// Applies layer-tree selection changes to the viewport selection manager so panel picks show scene highlights.
    /// </summary>
    private void LayerPanelViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;

        if (!string.Equals(e.PropertyName, nameof(LayerPanelViewModel.SelectedLayer), StringComparison.Ordinal))
        {
            return;
        }

        if (_isSynchronizingLayerAndViewportSelection)
        {
            return;
        }

        SynchronizeViewportSelectionFromLayerPanel();
    }

    /// <summary>
    /// Pushes the currently selected layer row into the scene selection manager.
    /// </summary>
    private void SynchronizeViewportSelectionFromLayerPanel()
    {
        _isSynchronizingLayerAndViewportSelection = true;

        try
        {
            LayerTreeItemViewModel? selectedLayer = _layerPanelViewModel.SelectedLayer;

            if (selectedLayer == null)
            {
                _scene.SelectionManager.ClearSelection();
                return;
            }

            if (selectedLayer.Kind == LayerTreeItemKind.Model)
            {
                CadEntity? selectedEntity = FindEntityById(selectedLayer.ModelEntityId);

                if (selectedEntity is MeshEntity selectedMesh)
                {
                    _scene.SelectionManager.SelectSingle(selectedMesh);
                    return;
                }
            }

            _scene.SelectionManager.ClearSelection();
        }
        finally
        {
            _isSynchronizingLayerAndViewportSelection = false;
        }
    }

    /// <summary>
    /// Mirrors viewport selection back into the layer tree so both selection surfaces stay synchronized.
    /// </summary>
    private void SynchronizeLayerPanelSelectionFromViewportSelection()
    {
        _isSynchronizingLayerAndViewportSelection = true;

        try
        {
            if (_scene.SelectionManager.SelectedCount != 1)
            {
                _layerPanelViewModel.ClearSelectedLayer();
                return;
            }

            Guid? selectedId = GetSingleSelectedEntityId();

            if (!selectedId.HasValue)
            {
                _layerPanelViewModel.ClearSelectedLayer();
                return;
            }

            CadEntity? selectedEntity = FindEntityById(selectedId.Value);

            if (selectedEntity is MeshEntity selectedMesh)
            {
                _layerPanelViewModel.SelectModelLayer(selectedMesh.Id);
                return;
            }

            if (selectedEntity is SupportEntity selectedSupport)
            {
                _layerPanelViewModel.SelectSupportGroupLayer(selectedSupport.SupportLayerGroupId);
                return;
            }

            _layerPanelViewModel.ClearSelectedLayer();
        }
        finally
        {
            _isSynchronizingLayerAndViewportSelection = false;
        }
    }
}
