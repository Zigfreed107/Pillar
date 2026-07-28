// MainWindow.Tags.cs
// Owns raft-tag option state, pointer placement, transient preview lifecycle, and command commits.
using Pillar.Commands;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Tags;
using Pillar.Geometry.Tags;
using Pillar.Rendering.Tools;
using Pillar.UI.Layers;
using Pillar.UI.Modes;
using Pillar.UI.Tags;
using System;

namespace Pillar.UI;

public partial class MainWindow
{
    private TagToolOptionsControl _tagToolOptionsControl = null!;
    private TagTool _tagTool = null!;
    private Guid? _activeTagModelEntityId;
    private TagEntity? _originalTag;
    private TagPlacement? _lockedTagPlacement;
    private TagMeshData? _tagPreviewMesh;
    private SupportLayerColor _tagSessionColor;
    private bool _tagSessionIsVisible = true;

    /// <summary>
    /// Starts a new tag for the model resolved from the current model or support selection.
    /// </summary>
    private void ShowTagToolForCurrentSelection()
    {
        Guid? modelEntityId = ResolveRaftTargetModelEntityId();

        if (!modelEntityId.HasValue || _document.FindRaftForModel(modelEntityId.Value) == null)
        {
            _viewModel.SetStatusText("Select a model with a raft.");
            return;
        }

        StartTagTool(modelEntityId.Value, null);
    }

    /// <summary>
    /// Opens an existing tag from its nested raft layer row.
    /// </summary>
    private void LayerPanel_EditTagRequested(object? sender, LayerTagEditRequestedEventArgs e)
    {
        _ = sender;
        TagEntity? tag = FindEntityById(e.TagEntityId) as TagEntity;

        if (tag == null || _document.FindRaftForModel(tag.ModelEntityId) == null)
        {
            _layerPanelViewModel.RefreshFromDocument();
            return;
        }

        StartTagTool(tag.ModelEntityId, tag);
    }

    /// <summary>
    /// Captures original state and opens editable controls without committing document changes.
    /// </summary>
    private void StartTagTool(Guid modelEntityId, TagEntity? originalTag)
    {
        RaftEntity? raft = _document.FindRaftForModel(modelEntityId);

        if (raft == null)
        {
            RefreshRaftTargetForSelection();
            _viewModel.SetStatusText("Select a model with a raft.");
            return;
        }

        CancelRaftToolSession();
        CancelTagToolSession();
        _toolManager.SetTool(_selectTool);
        _activeTagModelEntityId = modelEntityId;
        _originalTag = originalTag;
        _lockedTagPlacement = originalTag == null
            ? null
            : new TagPlacement(originalTag.AttachmentPoint, originalTag.Tangent);
        _tagPreviewMesh = null;
        _tagSessionColor = originalTag?.Color ?? SupportLayerColorGenerator.CreateRandom();
        _tagSessionIsVisible = originalTag == null || _layerPanelViewModel.GetTagLayerVisibility(originalTag.Id);

        if (originalTag != null)
        {
            _document.RemoveEntity(originalTag);
        }

        _tagToolOptionsControl.SetSettings(originalTag?.Settings ?? new TagSettings());
        ShowToolOptionsControl(_tagToolOptionsControl, ToolSessionPanelSet.None);

        if (_lockedTagPlacement.HasValue)
        {
            RegenerateLockedTagPreview();
        }

        _viewModel.SetStatusText(originalTag == null ? "Configure the tag, then click Place." : "Editing tag.");
    }

    /// <summary>
    /// Updates locked tag geometry and its eventual layer name after any option change.
    /// </summary>
    private void TagToolOptionsControl_OptionsChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_lockedTagPlacement.HasValue)
        {
            RegenerateLockedTagPreview();
        }
    }

    /// <summary>
    /// Hides controls and routes pointer movement into closest-edge placement.
    /// </summary>
    private void TagToolOptionsControl_PlaceRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (!_activeTagModelEntityId.HasValue)
        {
            return;
        }

        RaftEntity? raft = _document.FindRaftForModel(_activeTagModelEntityId.Value);

        if (raft == null)
        {
            CancelTagToolSession();
            _toolSessionOverlayCoordinator.EndSession();
            RestoreViewportToolForActiveMode();
            _viewModel.SetStatusText("Tag placement stopped because the raft no longer exists.");
            return;
        }

        TagSettings settings = _tagToolOptionsControl.GetSettings();
        TagTextMeshData textMesh = CreateTagTextMesh(settings);
        _lockedTagPlacement = null;
        _tagPreviewMesh = null;
        _tagToolOptionsControl.SetPlacementMode(true);
        _tagTool.BeginPlacement(raft, settings, textMesh, _tagSessionColor);
        _toolManager.SetTool(_tagTool);
        _viewModel.SetStatusText("Move the pointer around the raft edge and click to place the tag.");
    }

    /// <summary>
    /// Locks the accepted edge placement, restores controls, and renders an opaque preview.
    /// </summary>
    private void TagTool_PlacementAccepted(TagPlacement placement)
    {
        if (!_activeTagModelEntityId.HasValue)
        {
            return;
        }

        _lockedTagPlacement = placement;
        _tagToolOptionsControl.SetPlacementMode(false);

        // Restoring viewport control cancels the placement tool and hides its
        // transient preview, so rebuild the locked opaque preview afterwards.
        RestoreViewportToolForActiveMode();
        RegenerateLockedTagPreview();
        _viewModel.SetStatusText("Tag placed. Adjust settings or click Close to finish.");
    }

    /// <summary>
    /// Rebuilds the opaque locked body after placement or an option edit.
    /// </summary>
    private void RegenerateLockedTagPreview()
    {
        if (!_lockedTagPlacement.HasValue)
        {
            return;
        }

        TagSettings settings = _tagToolOptionsControl.GetSettings();
        TagTextMeshData textMesh = CreateTagTextMesh(settings);
        _tagPreviewMesh = TagMeshBuilder.Build(settings, textMesh, _lockedTagPlacement.Value);
        _scene.ShowTagPreview(_tagPreviewMesh, _tagSessionColor, 1.0f);
    }

    /// <summary>
    /// Shapes the selected installed font once and builds reusable local-space solid text.
    /// </summary>
    private static TagTextMeshData CreateTagTextMesh(TagSettings settings)
    {
        TagTextOutlineData outline = WpfTagTextOutlineFactory.Create(settings);
        return TagTextMeshBuilder.Build(settings, outline);
    }

    /// <summary>
    /// Commits a locked tag as one undoable add or replacement, or restores an unplaced edit.
    /// </summary>
    private void TagToolOptionsControl_CloseRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (!_activeTagModelEntityId.HasValue)
        {
            return;
        }

        if (!_lockedTagPlacement.HasValue || _tagPreviewMesh == null)
        {
            CancelTagToolSession();
            _toolSessionOverlayCoordinator.EndSession();
            RestoreViewportToolForActiveMode();
            _viewModel.SetStatusText("Tag closed without a placement.");
            return;
        }

        Guid modelEntityId = _activeTagModelEntityId.Value;
        TagEntity? originalTag = _originalTag;
        TagSettings settings = _tagToolOptionsControl.GetSettings();
        TagPlacement placement = _lockedTagPlacement.Value;
        TagEntity finalTag = new TagEntity(
            modelEntityId,
            settings,
            placement.AttachmentPoint,
            placement.Tangent,
            _tagPreviewMesh.Positions,
            _tagPreviewMesh.TriangleIndices,
            _tagSessionColor);
        bool isVisible = _tagSessionIsVisible;

        _tagTool.Cancel();
        _scene.HideTagPreview();

        if (originalTag != null)
        {
            _document.AddEntity(originalTag);
        }

        ClearTagSessionFields();
        _commandRunner.Execute(new ReplaceTagCommand(_document, originalTag, finalTag));
        _layerPanelViewModel.SetTagLayerVisibility(finalTag.Id, isVisible);
        _scene.SetTagLayerVisibility(finalTag.Id, isVisible);
        _toolSessionOverlayCoordinator.EndSession();
        _layerPanelViewModel.SelectTagLayer(finalTag.Id);
        RestoreViewportToolForActiveMode();
        RefreshRaftTargetForSelection();
        _viewModel.SetStatusText(originalTag == null ? "Added tag." : "Updated tag.");
    }

    /// <summary>
    /// Restores an edited original and drops all transient tag state without adding undo history.
    /// </summary>
    private void CancelTagToolSession()
    {
        if (!_activeTagModelEntityId.HasValue)
        {
            return;
        }

        bool wasPlacementToolActive = ReferenceEquals(_toolManager.ActiveTool, _tagTool);
        _tagTool.Cancel();
        _scene.HideTagPreview();
        _tagToolOptionsControl.SetPlacementMode(false);

        if (_originalTag != null && FindEntityById(_originalTag.Id) == null)
        {
            _document.AddEntity(_originalTag);
            _layerPanelViewModel.SetTagLayerVisibility(_originalTag.Id, _tagSessionIsVisible);
            _scene.SetTagLayerVisibility(_originalTag.Id, _tagSessionIsVisible);
        }

        ClearTagSessionFields();

        if (wasPlacementToolActive)
        {
            RestoreViewportToolForActiveMode();
        }
    }

    /// <summary>
    /// Clears shell-owned references after completing or canceling a Tag tool session.
    /// </summary>
    private void ClearTagSessionFields()
    {
        _activeTagModelEntityId = null;
        _originalTag = null;
        _lockedTagPlacement = null;
        _tagPreviewMesh = null;
        _tagSessionIsVisible = true;
    }

    /// <summary>
    /// Gets whether the Tag options panel currently owns a transient edit session.
    /// </summary>
    private bool IsTagToolActive()
    {
        return _activeTagModelEntityId.HasValue;
    }
}
