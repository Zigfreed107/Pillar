// MainWindow.Rafts.cs
// Owns procedural raft target resolution, live preview lifecycle, and Apply/Cancel behavior.
using Pillar.Commands;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Rafts;
using Pillar.Geometry.Rafts;
using Pillar.UI.Layers;
using Pillar.UI.Modes;
using Pillar.ViewModels;
using System;
using System.Collections.Generic;

namespace Pillar.UI;

public partial class MainWindow
{
    /// <summary>
    /// Resolves the model eligible for raft generation from viewer selection first, then layer context.
    /// </summary>
    private Guid? ResolveRaftTargetModelEntityId()
    {
        Guid? targetModelId = null;
        int selectedEntityCount = 0;
        bool hasSelectedModel = false;
        bool hasSelectedSupport = false;

        foreach (Guid selectedId in _scene.SelectionManager.SelectedEntityIds)
        {
            selectedEntityCount++;
            CadEntity? entity = FindEntityById(selectedId);

            if (entity is MeshEntity selectedModel)
            {
                if (selectedEntityCount != 1 || hasSelectedSupport) return null;
                hasSelectedModel = true;
                targetModelId = selectedModel.Id;
                continue;
            }

            if (entity is SupportEntity selectedSupport)
            {
                if (hasSelectedModel) return null;
                hasSelectedSupport = true;
                SupportLayerGroup? group = _document.FindSupportLayerGroupById(selectedSupport.SupportLayerGroupId);
                if (group == null || (targetModelId.HasValue && targetModelId.Value != group.ModelEntityId)) return null;
                targetModelId = group.ModelEntityId;
                continue;
            }

            return null;
        }

        if (selectedEntityCount > 0)
        {
            return targetModelId;
        }

        LayerTreeItemViewModel? selectedLayer = _layerPanelViewModel.SelectedLayer;
        if (selectedLayer == null) return null;

        return selectedLayer.Kind switch
        {
            LayerTreeItemKind.Model => selectedLayer.ModelEntityId,
            LayerTreeItemKind.SupportGroup => selectedLayer.ModelEntityId,
            _ => null
        };
    }

    /// <summary>
    /// Refreshes the Raft button enablement after selection changes.
    /// </summary>
    private void RefreshRaftTargetForSelection()
    {
        _layerPanelViewModel.SetRaftTargetModelEntityId(ResolveRaftTargetModelEntityId());
    }

    /// <summary>
    /// Starts new raft generation for the currently eligible model.
    /// </summary>
    private void ShowRaftToolForCurrentSelection()
    {
        Guid? modelEntityId = ResolveRaftTargetModelEntityId();
        if (!modelEntityId.HasValue)
        {
            _viewModel.SetStatusText("Select one model or support layers belonging to one model.");
            return;
        }

        StartRaftTool(modelEntityId.Value, _document.FindRaftForModel(modelEntityId.Value));
    }

    /// <summary>
    /// Opens an existing raft from its layer row.
    /// </summary>
    private void LayerPanel_EditRaftRequested(object? sender, LayerRaftEditRequestedEventArgs e)
    {
        _ = sender;
        RaftEntity? raft = FindEntityById(e.RaftEntityId) as RaftEntity;

        if (raft == null)
        {
            _layerPanelViewModel.RefreshFromDocument();
            return;
        }

        StartRaftTool(raft.ModelEntityId, raft);
    }

    /// <summary>
    /// Captures original state and creates the first live raft preview.
    /// </summary>
    private void StartRaftTool(Guid modelEntityId, RaftEntity? originalRaft)
    {
        if (GetSupportEntitiesForModel(modelEntityId).Count == 0)
        {
            RefreshRaftTargetForSelection();
            _viewModel.SetStatusText("A model needs at least one support before a raft can be applied.");
            return;
        }

        CancelRaftToolSession();
        _activeRaftModelEntityId = modelEntityId;
        _originalRaft = originalRaft;
        _previewRaft = null;
        _raftSessionIsVisible = originalRaft == null || _layerPanelViewModel.GetRaftLayerVisibility(originalRaft.Id);
        _raftToolOptionsControl.SetSettings(originalRaft?.Settings ?? new RaftSettings());
        ShowToolOptionsControl(_raftToolOptionsControl, ToolSessionPanelSet.None);
        RegenerateRaftPreview();
        _viewModel.SetStatusText(originalRaft == null ? "Previewing new raft" : "Editing raft");
    }

    /// <summary>
    /// Regenerates the transient entity whenever one setting changes.
    /// </summary>
    private void RaftToolOptionsControl_OptionsChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        RegenerateRaftPreview();
    }

    /// <summary>
    /// Builds and swaps one preview without adding an undo-history entry.
    /// </summary>
    private void RegenerateRaftPreview()
    {
        if (!_activeRaftModelEntityId.HasValue) return;
        MeshEntity? model = FindEntityById(_activeRaftModelEntityId.Value) as MeshEntity;
        if (model == null)
        {
            CancelRaftToolSession();
            return;
        }

        List<SupportEntity> supports = GetSupportEntitiesForModel(model.Id);
        if (supports.Count == 0)
        {
            CancelRaftToolSession();
            _toolSessionOverlayCoordinator.EndSession();
            RefreshRaftTargetForSelection();
            _viewModel.SetStatusText("Raft editing stopped because the model no longer has supports.");
            return;
        }

        RaftSettings settings = _raftToolOptionsControl.GetSettings();
        SupportLayerColor color = _previewRaft?.Color ?? _originalRaft?.Color ?? SupportLayerColorGenerator.CreateRandom();
        RaftMeshData meshData = RaftMeshBuilder.Build(supports, settings);
        RaftEntity preview = new RaftEntity(model.Id, settings, meshData.Positions, meshData.TriangleIndices, color);

        using IDisposable batch = _document.BeginEntityBatchUpdate();
        if (_previewRaft != null) _document.RemoveEntity(_previewRaft);
        else if (_originalRaft != null) _document.RemoveEntity(_originalRaft);
        _document.AddEntity(preview);
        _previewRaft = preview;
        _layerPanelViewModel.SetRaftLayerVisibility(preview.Id, _raftSessionIsVisible);
        _scene.SetRaftLayerVisibility(preview.Id, _raftSessionIsVisible);
    }

    /// <summary>
    /// Commits the current preview as one undoable add or replacement.
    /// </summary>
    private void RaftToolOptionsControl_ApplyRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        if (_previewRaft == null) return;

        RaftEntity finalRaft = _previewRaft;
        RaftEntity? originalRaft = _originalRaft;
        bool isVisible = _raftSessionIsVisible;
        using (IDisposable batch = _document.BeginEntityBatchUpdate())
        {
            _document.RemoveEntity(finalRaft);
            if (originalRaft != null) _document.AddEntity(originalRaft);
        }

        ClearRaftSessionFields();
        _commandRunner.Execute(new ReplaceRaftCommand(_document, originalRaft, finalRaft));
        _layerPanelViewModel.SetRaftLayerVisibility(finalRaft.Id, isVisible);
        _scene.SetRaftLayerVisibility(finalRaft.Id, isVisible);
        _toolSessionOverlayCoordinator.EndSession();
        _layerPanelViewModel.SelectModelLayer(finalRaft.ModelEntityId);
        RefreshRaftTargetForSelection();
        _viewModel.SetStatusText(originalRaft == null ? "Added raft" : "Updated raft");
    }

    /// <summary>
    /// Restores the exact original raft and exits without changing undo history.
    /// </summary>
    private void RaftToolOptionsControl_CancelRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        CancelRaftToolSession();
        _toolSessionOverlayCoordinator.EndSession();
        _viewModel.SetStatusText("Raft changes cancelled");
    }

    /// <summary>
    /// Drops a transient preview and reinstates any original entity.
    /// </summary>
    private void CancelRaftToolSession()
    {
        if (!_activeRaftModelEntityId.HasValue) return;

        using IDisposable batch = _document.BeginEntityBatchUpdate();
        if (_previewRaft != null) _document.RemoveEntity(_previewRaft);
        if (_originalRaft != null && _document.FindRaftForModel(_originalRaft.ModelEntityId) == null)
        {
            _document.AddEntity(_originalRaft);
            _layerPanelViewModel.SetRaftLayerVisibility(_originalRaft.Id, _raftSessionIsVisible);
            _scene.SetRaftLayerVisibility(_originalRaft.Id, _raftSessionIsVisible);
        }
        ClearRaftSessionFields();
    }

    /// <summary>
    /// Clears shell-owned references after Apply or Cancel.
    /// </summary>
    private void ClearRaftSessionFields()
    {
        _activeRaftModelEntityId = null;
        _originalRaft = null;
        _previewRaft = null;
        _raftSessionIsVisible = true;
    }

    /// <summary>
    /// Gets whether the raft options panel currently owns a live preview.
    /// </summary>
    private bool IsRaftToolActive()
    {
        return _activeRaftModelEntityId.HasValue;
    }
}
