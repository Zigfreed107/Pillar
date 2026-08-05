// MainWindow.RaftTexts.cs
// Owns raft-text option state, pointer placement, transient preview lifecycle, and command commits.
using Pillar.Commands;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.RaftTexts;
using Pillar.Geometry.RaftTexts;
using Pillar.Geometry.Tags;
using Pillar.Rendering.Tools;
using Pillar.UI.Layers;
using Pillar.UI.Modes;
using Pillar.UI.Tags;
using System;
using System.Numerics;

namespace Pillar.UI;

public partial class MainWindow
{
    private RaftTextToolOptionsControl _raftTextToolOptionsControl = null!;
    private RaftTextTool _raftTextTool = null!;
    private Guid? _activeRaftTextModelEntityId;
    private RaftTextEntity? _originalRaftText;
    private Vector3? _lockedRaftTextPlacement;
    private RaftTextMeshData? _raftTextPreviewMesh;
    private SupportLayerColor _raftTextSessionColor;
    private bool _raftTextSessionIsVisible = true;

    /// <summary>
    /// Starts new raft text for the model resolved from the current model or support selection.
    /// </summary>
    private void ShowRaftTextToolForCurrentSelection()
    {
        Guid? modelEntityId = ResolveRaftTargetModelEntityId();

        if (!modelEntityId.HasValue || _document.FindRaftForModel(modelEntityId.Value) == null)
        {
            _viewModel.SetStatusText("Select a model with a raft.");
            return;
        }

        StartRaftTextTool(modelEntityId.Value, null);
    }

    /// <summary>
    /// Opens existing raft text from its nested raft layer row.
    /// </summary>
    private void LayerPanel_EditRaftTextRequested(object? sender, LayerRaftTextEditRequestedEventArgs e)
    {
        _ = sender;
        RaftTextEntity? raftText = FindEntityById(e.RaftTextEntityId) as RaftTextEntity;

        if (raftText == null || _document.FindRaftForModel(raftText.ModelEntityId) == null)
        {
            _layerPanelViewModel.RefreshFromDocument();
            return;
        }

        StartRaftTextTool(raftText.ModelEntityId, raftText);
    }

    /// <summary>
    /// Captures original state and opens editable controls without committing document changes.
    /// </summary>
    private void StartRaftTextTool(Guid modelEntityId, RaftTextEntity? originalRaftText)
    {
        if (_document.FindRaftForModel(modelEntityId) == null)
        {
            RefreshRaftTargetForSelection();
            _viewModel.SetStatusText("Select a model with a raft.");
            return;
        }

        CancelRaftToolSession();
        CancelTagToolSession();
        CancelRaftTextToolSession();
        _toolManager.SetTool(_selectTool);
        _activeRaftTextModelEntityId = modelEntityId;
        _originalRaftText = originalRaftText;
        _lockedRaftTextPlacement = originalRaftText?.Placement;
        _raftTextPreviewMesh = null;
        _raftTextSessionColor = originalRaftText?.Color ?? SupportLayerColorGenerator.CreateRandom();
        _raftTextSessionIsVisible = originalRaftText == null
            || _layerPanelViewModel.GetRaftTextLayerVisibility(originalRaftText.Id);

        if (originalRaftText != null)
        {
            _document.RemoveEntity(originalRaftText);
        }

        _raftTextToolOptionsControl.SetSettings(originalRaftText?.Settings ?? new RaftTextSettings());
        ShowToolOptionsControl(_raftTextToolOptionsControl, ToolSessionPanelSet.None);

        if (_lockedRaftTextPlacement.HasValue)
        {
            RegenerateLockedRaftTextPreview();
        }

        _viewModel.SetStatusText(originalRaftText == null
            ? "Configure the raft text, then click Place."
            : "Editing raft text.");
    }

    /// <summary>
    /// Updates locked text geometry after any option change.
    /// </summary>
    private void RaftTextToolOptionsControl_OptionsChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_lockedRaftTextPlacement.HasValue)
        {
            RegenerateLockedRaftTextPreview();
        }
    }

    /// <summary>
    /// Hides controls and routes pointer movement into interior raft placement.
    /// </summary>
    private void RaftTextToolOptionsControl_PlaceRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (!_activeRaftTextModelEntityId.HasValue)
        {
            return;
        }

        RaftEntity? raft = _document.FindRaftForModel(_activeRaftTextModelEntityId.Value);

        if (raft == null)
        {
            CancelRaftTextToolSession();
            _toolSessionOverlayCoordinator.EndSession();
            RestoreViewportToolForActiveMode();
            _viewModel.SetStatusText("Raft text placement stopped because the raft no longer exists.");
            return;
        }

        RaftTextSettings settings = _raftTextToolOptionsControl.GetSettings();
        RaftTextMeshData localMesh = CreateLocalRaftTextMesh(settings);
        _lockedRaftTextPlacement = null;
        _raftTextPreviewMesh = null;
        _raftTextToolOptionsControl.SetPlacementMode(true);
        _raftTextTool.BeginPlacement(raft, settings, localMesh, _raftTextSessionColor);
        _toolManager.SetTool(_raftTextTool);
        _viewModel.SetStatusText("Move the pointer over the raft and click to place the text.");
    }

    /// <summary>
    /// Locks the accepted placement, restores controls, and renders an opaque preview.
    /// </summary>
    private void RaftTextTool_PlacementAccepted(Vector3 placement)
    {
        if (!_activeRaftTextModelEntityId.HasValue)
        {
            return;
        }

        _lockedRaftTextPlacement = placement;
        _raftTextToolOptionsControl.SetPlacementMode(false);
        RestoreViewportToolForActiveMode();
        RegenerateLockedRaftTextPreview();
        _viewModel.SetStatusText("Raft text placed. Adjust settings or click Close to finish.");
    }

    /// <summary>
    /// Rebuilds the opaque locked text after placement or an option edit.
    /// </summary>
    private void RegenerateLockedRaftTextPreview()
    {
        if (!_lockedRaftTextPlacement.HasValue || !_activeRaftTextModelEntityId.HasValue)
        {
            return;
        }

        RaftEntity? raft = _document.FindRaftForModel(_activeRaftTextModelEntityId.Value);

        if (raft == null)
        {
            _raftTextPreviewMesh = null;
            _scene.HideRaftTextPreview();
            return;
        }

        RaftTextSettings settings = _raftTextToolOptionsControl.GetSettings();
        RaftTextMeshData localMesh = CreateLocalRaftTextMesh(settings);
        RaftTextPlacementPlanner planner = new RaftTextPlacementPlanner(raft, localMesh, settings.BorderOffset);
        Vector3 currentPlacement = _lockedRaftTextPlacement.Value;

        if (!planner.TryFindPlacement(
                new Vector2(currentPlacement.X, currentPlacement.Y),
                out Vector3 restrictedPlacement))
        {
            _raftTextPreviewMesh = null;
            _scene.HideRaftTextPreview();
            _viewModel.SetStatusText("The current text does not fit within this raft.");
            return;
        }

        _lockedRaftTextPlacement = restrictedPlacement;
        _raftTextPreviewMesh = RaftTextMeshBuilder.Place(localMesh, restrictedPlacement);
        _scene.ShowRaftTextPreview(_raftTextPreviewMesh, _raftTextSessionColor, 1.0f);
    }

    /// <summary>
    /// Shapes the selected installed font and builds local solid raft text.
    /// </summary>
    private static RaftTextMeshData CreateLocalRaftTextMesh(RaftTextSettings settings)
    {
        TagTextOutlineData outline = WpfTagTextOutlineFactory.Create(
            settings.Text,
            settings.FontFamilyName,
            settings.FontSize,
            RaftTextSettings.DefaultFontFamilyName);
        return RaftTextMeshBuilder.BuildLocal(settings, outline);
    }

    /// <summary>
    /// Commits locked text as one undoable add or replacement, or restores an unplaced edit.
    /// </summary>
    private void RaftTextToolOptionsControl_CloseRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (!_activeRaftTextModelEntityId.HasValue)
        {
            return;
        }

        if (!_lockedRaftTextPlacement.HasValue || _raftTextPreviewMesh == null)
        {
            CancelRaftTextToolSession();
            _toolSessionOverlayCoordinator.EndSession();
            RestoreViewportToolForActiveMode();
            _viewModel.SetStatusText("Raft text closed without a placement.");
            return;
        }

        Guid modelEntityId = _activeRaftTextModelEntityId.Value;
        RaftTextEntity? originalRaftText = _originalRaftText;
        RaftTextSettings settings = _raftTextToolOptionsControl.GetSettings();
        RaftTextEntity finalRaftText = new RaftTextEntity(
            modelEntityId,
            settings,
            _lockedRaftTextPlacement.Value,
            _raftTextPreviewMesh.Positions,
            _raftTextPreviewMesh.TriangleIndices,
            _raftTextSessionColor);
        bool isVisible = _raftTextSessionIsVisible;

        _raftTextTool.Cancel();
        _scene.HideRaftTextPreview();

        if (originalRaftText != null)
        {
            _document.AddEntity(originalRaftText);
        }

        ClearRaftTextSessionFields();
        _commandRunner.Execute(new ReplaceRaftTextCommand(_document, originalRaftText, finalRaftText));
        _layerPanelViewModel.SetRaftTextLayerVisibility(finalRaftText.Id, isVisible);
        _scene.SetRaftTextLayerVisibility(finalRaftText.Id, isVisible);
        _toolSessionOverlayCoordinator.EndSession();
        _layerPanelViewModel.SelectRaftTextLayer(finalRaftText.Id);
        RestoreViewportToolForActiveMode();
        RefreshRaftTargetForSelection();
        _viewModel.SetStatusText(originalRaftText == null ? "Added raft text." : "Updated raft text.");
    }

    /// <summary>
    /// Restores an edited original and drops transient state without adding undo history.
    /// </summary>
    private void CancelRaftTextToolSession()
    {
        if (!_activeRaftTextModelEntityId.HasValue)
        {
            return;
        }

        bool wasPlacementToolActive = ReferenceEquals(_toolManager.ActiveTool, _raftTextTool);
        _raftTextTool.Cancel();
        _scene.HideRaftTextPreview();
        _raftTextToolOptionsControl.SetPlacementMode(false);

        if (_originalRaftText != null && FindEntityById(_originalRaftText.Id) == null)
        {
            _document.AddEntity(_originalRaftText);
            _layerPanelViewModel.SetRaftTextLayerVisibility(_originalRaftText.Id, _raftTextSessionIsVisible);
            _scene.SetRaftTextLayerVisibility(_originalRaftText.Id, _raftTextSessionIsVisible);
        }

        ClearRaftTextSessionFields();

        if (wasPlacementToolActive)
        {
            RestoreViewportToolForActiveMode();
        }
    }

    /// <summary>
    /// Clears shell-owned references after completing or canceling a Raft Text session.
    /// </summary>
    private void ClearRaftTextSessionFields()
    {
        _activeRaftTextModelEntityId = null;
        _originalRaftText = null;
        _lockedRaftTextPlacement = null;
        _raftTextPreviewMesh = null;
        _raftTextSessionIsVisible = true;
    }

    /// <summary>
    /// Gets whether the Raft Text options panel owns a transient edit session.
    /// </summary>
    private bool IsRaftTextToolActive()
    {
        return _activeRaftTextModelEntityId.HasValue;
    }
}
