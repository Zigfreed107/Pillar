// MainWindow.TransformTranslate.cs
// Hosts the Transform Translate session while Core owns bounds math and Rendering owns transient axis handles.
using Pillar.Commands;
using Pillar.Core.Entities;
using Pillar.Core.Supports;
using Pillar.Geometry.Supports;
using Pillar.UI.Modes;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.UI;

public partial class MainWindow
{
    private const string TransformTranslateToolName = "Translate";
    private const string TransformMoveToPlateActionName = "Move to Plate";
    private const float TransformTranslatePositionTolerance = 0.0001f;

    private Guid? _activeTransformTranslateModelId;
    private Vector3 _activeTransformTranslateImportSpaceOrigin;
    private Vector3 _activeTransformTranslateOriginalWorldOrigin;
    private MeshTranslationLimits _activeTransformTranslateLimits;
    private Transform3DData _activeTransformTranslateOriginalTransform;
    private Transform3DData _activeTransformTranslatePreviewTransform;
    private bool _isTransformTranslateToolActive;
    private bool _hasPendingTransformTranslatePreview;

    /// <summary>
    /// Opens Transform Translate options and persistent direct-manipulation arrows for the selected model.
    /// </summary>
    private void ShowTransformTranslateTool()
    {
        MeshEntity? selectedMesh = GetSelectedTransformMesh();

        if (selectedMesh == null)
        {
            ClearTransformTranslateToolState();
            HideToolOptionsHostOnly();
            _viewModel.SetStatusText("Select one imported model before translating.");
            return;
        }

        ClearTransformTranslateToolState();
        _activeTransformTranslateModelId = selectedMesh.Id;
        _activeTransformTranslateImportSpaceOrigin = MeshTranslationTransform.CalculateImportSpaceOrigin(selectedMesh);
        _activeTransformTranslateOriginalWorldOrigin = MeshTranslationTransform.CalculateWorldOrigin(
            selectedMesh,
            _activeTransformTranslateImportSpaceOrigin);
        _activeTransformTranslateLimits = MeshTranslationTransform.CreateLimits(
            selectedMesh,
            _activeTransformTranslateImportSpaceOrigin,
            _printableVolumeDefinition.XDistance,
            _printableVolumeDefinition.YDistance);
        _activeTransformTranslateOriginalTransform = selectedMesh.UserTransform;
        _activeTransformTranslatePreviewTransform = selectedMesh.UserTransform;
        _isTransformTranslateToolActive = true;
        _hasPendingTransformTranslatePreview = false;

        Vector3 displayedOrigin = _activeTransformTranslateOriginalWorldOrigin;

        if (MeshTranslationTransform.TryCreateUserTransformForWorldOrigin(
                _activeTransformTranslateOriginalTransform,
                _activeTransformTranslateOriginalWorldOrigin,
                displayedOrigin,
                _activeTransformTranslateLimits,
                out Transform3DData constrainedTransform,
                out Vector3 constrainedOrigin)
            && constrainedTransform != selectedMesh.UserTransform)
        {
            selectedMesh.UserTransform = constrainedTransform;
            _activeTransformTranslatePreviewTransform = constrainedTransform;
            _hasPendingTransformTranslatePreview = true;
            displayedOrigin = constrainedOrigin;
        }

        _translateToolOptionsControl.SetPositionAndLimits(displayedOrigin, _activeTransformTranslateLimits);
        ShowToolOptionsControl(_translateToolOptionsControl, ToolSessionPanelSet.None);
        _toolManager.SetTool(_modelTranslateTool);
        _modelTranslateTool.Begin(
            selectedMesh,
            _activeTransformTranslateImportSpaceOrigin,
            _activeTransformTranslateLimits,
            displayedOrigin);

        _activeToolStatusText = "Transform translate tool active";

        if (!_activeTransformTranslateLimits.CanFitPrintableArea)
        {
            _viewModel.SetStatusText("The selected model is too large to fit inside the printable XY area at its current orientation.");
            _viewModel.SetToolPanelText("Translation is unavailable because the model footprint exceeds the printable area.");
            return;
        }

        _viewModel.SetStatusText(_activeToolStatusText);
        _viewModel.SetToolPanelText(_activeToolStatusText);
    }

    /// <summary>
    /// Applies one numeric absolute-origin edit as a live constrained preview.
    /// </summary>
    private void TranslateToolOptionsControl_PositionChanged(object? sender, TranslateToolPositionChangedEventArgs e)
    {
        _ = sender;
        TryApplyActiveTransformTranslatePosition(e.Position, "Updated model origin position");
    }

    /// <summary>
    /// Aligns the model origin with world X/Y zero while preserving its current Z position.
    /// </summary>
    private void TranslateToolOptionsControl_MoveToOriginRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        MeshEntity? selectedMesh = GetActiveTransformTranslateMesh();

        if (selectedMesh == null)
        {
            HandleUnavailableActiveTransformTranslateMesh();
            return;
        }

        Vector3 currentOrigin = MeshTranslationTransform.CalculateWorldOrigin(
            selectedMesh,
            _activeTransformTranslateImportSpaceOrigin);
        Vector3 requestedOrigin = new Vector3(0.0f, 0.0f, currentOrigin.Z);
        Vector3 constrainedOrigin = _activeTransformTranslateLimits.ClampOrigin(requestedOrigin);

        if (!ArePositionsClose(requestedOrigin, constrainedOrigin))
        {
            _viewModel.SetStatusText("The model origin cannot be aligned to X/Y zero without crossing the printable boundary.");
            return;
        }

        TryApplyActiveTransformTranslatePosition(requestedOrigin, "Moved model origin to X 0, Y 0");
    }

    /// <summary>
    /// Places the selected model on the build plate from the Translate options panel.
    /// </summary>
    private void TranslateToolOptionsControl_MoveToPlateRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        MeshEntity? selectedMesh = GetActiveTransformTranslateMesh();

        if (selectedMesh == null)
        {
            HandleUnavailableActiveTransformTranslateMesh();
            return;
        }

        Vector3 currentOrigin = MeshTranslationTransform.CalculateWorldOrigin(
            selectedMesh,
            _activeTransformTranslateImportSpaceOrigin);

        if (!IsOriginInsidePrintableXY(currentOrigin, _activeTransformTranslateLimits))
        {
            _viewModel.SetStatusText("Move to Plate cannot preserve X/Y because the model is outside the printable boundary.");
            return;
        }

        Vector3 requestedOrigin = new Vector3(
            currentOrigin.X,
            currentOrigin.Y,
            _activeTransformTranslateLimits.MinimumOriginZ);
        TryApplyActiveTransformTranslatePosition(requestedOrigin, "Moved model to plate");
    }

    /// <summary>
    /// Applies a constrained transform produced by direct arrow dragging.
    /// </summary>
    private void ModelTranslateTool_PreviewTransformRequested(Transform3DData transform, Vector3 constrainedWorldOrigin)
    {
        ApplyActiveTransformTranslatePreview(transform, constrainedWorldOrigin);
        _viewModel.SetStatusText(CreateTranslateStatusText(constrainedWorldOrigin));
    }

    /// <summary>
    /// Commits the complete Translate session and closes its options panel.
    /// </summary>
    private void TranslateToolOptionsControl_FinishRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        CommitActiveTransformTranslatePreview();
        HideToolOptionsOverlay();
        _viewModel.SetStatusText("Finished translating model");
    }

    /// <summary>
    /// Refreshes or closes the active Translate session when the selected model changes.
    /// </summary>
    private void RefreshTransformTranslateToolForSelection()
    {
        if (!_isTransformTranslateToolActive)
        {
            return;
        }

        MeshEntity? selectedMesh = GetSelectedTransformMesh();

        if (selectedMesh == null)
        {
            HideToolOptionsOverlay();
            return;
        }

        if (!_activeTransformTranslateModelId.HasValue
            || _activeTransformTranslateModelId.Value != selectedMesh.Id)
        {
            ClearTransformTranslateToolState();
            ShowTransformTranslateTool();
            return;
        }

        Vector3 worldOrigin = MeshTranslationTransform.CalculateWorldOrigin(
            selectedMesh,
            _activeTransformTranslateImportSpaceOrigin);
        _translateToolOptionsControl.SetPosition(worldOrigin);
        _modelTranslateTool.UpdatePreview(worldOrigin);
    }

    /// <summary>
    /// Restores any uncommitted preview, hides gizmos, and returns viewport input to the active mode tool.
    /// </summary>
    private void ClearTransformTranslateToolState()
    {
        if (!_isTransformTranslateToolActive && !_activeTransformTranslateModelId.HasValue)
        {
            _modelTranslateTool.Cancel();
            return;
        }

        RevertPendingTransformTranslatePreview();
        _isTransformTranslateToolActive = false;
        _activeTransformTranslateModelId = null;
        _activeTransformTranslateImportSpaceOrigin = Vector3.Zero;
        _activeTransformTranslateOriginalWorldOrigin = Vector3.Zero;
        _activeTransformTranslateLimits = default;
        _activeTransformTranslateOriginalTransform = Transform3DData.Identity;
        _activeTransformTranslatePreviewTransform = Transform3DData.Identity;
        _hasPendingTransformTranslatePreview = false;
        _modelTranslateTool.Cancel();

        if (ReferenceEquals(_toolManager.ActiveTool, _modelTranslateTool))
        {
            RestoreViewportToolForActiveMode();
        }
    }

    /// <summary>
    /// Converts the live Translate preview into one undoable transform and support-regeneration command.
    /// </summary>
    private void CommitActiveTransformTranslatePreview()
    {
        if (!_isTransformTranslateToolActive
            || !_activeTransformTranslateModelId.HasValue
            || !_hasPendingTransformTranslatePreview
            || _activeTransformTranslateOriginalTransform == _activeTransformTranslatePreviewTransform)
        {
            return;
        }

        MeshEntity? selectedMesh = FindEntityById(_activeTransformTranslateModelId.Value) as MeshEntity;

        if (selectedMesh == null)
        {
            _hasPendingTransformTranslatePreview = false;
            return;
        }

        Transform3DData oldTransform = _activeTransformTranslateOriginalTransform;
        Transform3DData newTransform = _activeTransformTranslatePreviewTransform;
        IReadOnlyList<SupportGroupRegeneration> supportRegenerations = SupportGroupTransformRegenerator.CreateRegenerations(
            _document,
            selectedMesh,
            oldTransform,
            newTransform);
        selectedMesh.UserTransform = oldTransform;

        // Mark the preview committed before support replacement can re-enter selection refresh paths.
        _activeTransformTranslateOriginalTransform = newTransform;
        _activeTransformTranslatePreviewTransform = newTransform;
        _hasPendingTransformTranslatePreview = false;
        _commandRunner.Execute(new SetMeshUserTransformCommand(
            _document,
            selectedMesh,
            oldTransform,
            newTransform,
            supportRegenerations,
            "Translate Model"));
    }

    /// <summary>
    /// Restores the exact session-start transform when the Translate panel is abandoned.
    /// </summary>
    private void RevertPendingTransformTranslatePreview()
    {
        if (!_hasPendingTransformTranslatePreview || !_activeTransformTranslateModelId.HasValue)
        {
            return;
        }

        MeshEntity? selectedMesh = FindEntityById(_activeTransformTranslateModelId.Value) as MeshEntity;

        if (selectedMesh != null)
        {
            selectedMesh.UserTransform = _activeTransformTranslateOriginalTransform;
        }

        _activeTransformTranslatePreviewTransform = _activeTransformTranslateOriginalTransform;
        _hasPendingTransformTranslatePreview = false;
    }

    /// <summary>
    /// Creates and applies a preview from one requested absolute model-origin position.
    /// </summary>
    private bool TryApplyActiveTransformTranslatePosition(Vector3 requestedOrigin, string statusText)
    {
        MeshEntity? selectedMesh = GetActiveTransformTranslateMesh();

        if (selectedMesh == null)
        {
            HandleUnavailableActiveTransformTranslateMesh();
            return false;
        }

        if (!MeshTranslationTransform.TryCreateUserTransformForWorldOrigin(
                _activeTransformTranslateOriginalTransform,
                _activeTransformTranslateOriginalWorldOrigin,
                requestedOrigin,
                _activeTransformTranslateLimits,
                out Transform3DData transform,
                out Vector3 constrainedOrigin))
        {
            _translateToolOptionsControl.SetPosition(MeshTranslationTransform.CalculateWorldOrigin(
                selectedMesh,
                _activeTransformTranslateImportSpaceOrigin));
            _viewModel.SetStatusText("The model cannot be translated because its footprint exceeds the printable area.");
            return false;
        }

        ApplyActiveTransformTranslatePreview(transform, constrainedOrigin);
        _viewModel.SetStatusText($"{statusText}. {CreateTranslateStatusText(constrainedOrigin)}");
        return true;
    }

    /// <summary>
    /// Updates the model, options, and gizmo from one already-constrained preview transform.
    /// </summary>
    private void ApplyActiveTransformTranslatePreview(Transform3DData transform, Vector3 constrainedWorldOrigin)
    {
        MeshEntity? selectedMesh = GetActiveTransformTranslateMesh();

        if (selectedMesh == null)
        {
            return;
        }

        selectedMesh.UserTransform = transform;
        _activeTransformTranslatePreviewTransform = transform;
        _hasPendingTransformTranslatePreview = _activeTransformTranslateOriginalTransform != transform;
        _translateToolOptionsControl.SetPosition(constrainedWorldOrigin);
        _modelTranslateTool.UpdatePreview(constrainedWorldOrigin);
    }

    /// <summary>
    /// Gets the active Translate target only while it still exists and remains selected.
    /// </summary>
    private MeshEntity? GetActiveTransformTranslateMesh()
    {
        if (!_activeTransformTranslateModelId.HasValue)
        {
            return null;
        }

        MeshEntity? mesh = FindEntityById(_activeTransformTranslateModelId.Value) as MeshEntity;
        MeshEntity? selectedMesh = GetSelectedTransformMesh();
        return mesh != null && selectedMesh != null && mesh.Id == selectedMesh.Id ? mesh : null;
    }

    /// <summary>
    /// Resets a stale Translate session and attempts to start one for the current selection.
    /// </summary>
    private void HandleUnavailableActiveTransformTranslateMesh()
    {
        ClearTransformTranslateToolState();
        ShowTransformTranslateTool();
    }

    /// <summary>
    /// Moves the selected model's lowest transformed vertex to Z zero as one undoable document command.
    /// </summary>
    private void MoveSelectedTransformMeshToPlate()
    {
        MeshEntity? selectedMesh = GetSelectedTransformMesh();

        if (selectedMesh == null)
        {
            _viewModel.SetStatusText("Select one imported model before moving it to the plate.");
            return;
        }

        Vector3 importSpaceOrigin = MeshTranslationTransform.CalculateImportSpaceOrigin(selectedMesh);
        MeshTranslationLimits limits = MeshTranslationTransform.CreateLimits(
            selectedMesh,
            importSpaceOrigin,
            _printableVolumeDefinition.XDistance,
            _printableVolumeDefinition.YDistance);
        Vector3 currentOrigin = MeshTranslationTransform.CalculateWorldOrigin(selectedMesh, importSpaceOrigin);

        if (!limits.CanFitPrintableArea || !IsOriginInsidePrintableXY(currentOrigin, limits))
        {
            _viewModel.SetStatusText("Move to Plate cannot preserve X/Y because the model is outside the printable boundary.");
            return;
        }

        Transform3DData oldTransform = selectedMesh.UserTransform;
        Transform3DData newTransform = MeshPlatePlacementTransform.CreateUserTransformForMoveToPlate(selectedMesh);

        if (oldTransform == newTransform)
        {
            _viewModel.SetStatusText("The selected model is already on the plate.");
            return;
        }

        IReadOnlyList<SupportGroupRegeneration> supportRegenerations = SupportGroupTransformRegenerator.CreateRegenerations(
            _document,
            selectedMesh,
            oldTransform,
            newTransform);

        _commandRunner.Execute(new SetMeshUserTransformCommand(
            _document,
            selectedMesh,
            oldTransform,
            newTransform,
            supportRegenerations,
            "Move Model to Plate"));

        _viewModel.SetStatusText("Moved model to plate");
        _viewModel.SetToolPanelText("Selected model is resting at Z 0");
    }

    /// <summary>
    /// Tests whether an absolute origin lies within both printable movement ranges.
    /// </summary>
    private static bool IsOriginInsidePrintableXY(Vector3 origin, MeshTranslationLimits limits)
    {
        return limits.CanFitPrintableArea
            && origin.X >= limits.MinimumOriginX - TransformTranslatePositionTolerance
            && origin.X <= limits.MaximumOriginX + TransformTranslatePositionTolerance
            && origin.Y >= limits.MinimumOriginY - TransformTranslatePositionTolerance
            && origin.Y <= limits.MaximumOriginY + TransformTranslatePositionTolerance;
    }

    /// <summary>
    /// Compares absolute positions with a small transform-input tolerance.
    /// </summary>
    private static bool ArePositionsClose(Vector3 left, Vector3 right)
    {
        return Vector3.DistanceSquared(left, right)
            <= TransformTranslatePositionTolerance * TransformTranslatePositionTolerance;
    }

    /// <summary>
    /// Formats the absolute position shown by the options panel.
    /// </summary>
    private static string CreateTranslateStatusText(Vector3 worldOrigin)
    {
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "Origin X {0:0.###}, Y {1:0.###}, Z {2:0.###}",
            worldOrigin.X,
            worldOrigin.Y,
            worldOrigin.Z);
    }
}
