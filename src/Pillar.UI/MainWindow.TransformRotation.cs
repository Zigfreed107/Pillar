// MainWindow.TransformRotation.cs
// Hosts the Transform Rotate session while keeping rotation math in Core and guide drawing in Rendering.
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
    private const string TransformRotationToolName = "Rotate";
    private const float RotationGuideDiameterModelSizeFactor = 1.2f;

    private Guid? _activeTransformRotationModelId;
    private Vector3 _activeTransformRotationImportSpaceOrigin;
    private Transform3DData _activeTransformRotationOriginalTransform;
    private Transform3DData _activeTransformRotationInputBaselineTransform;
    private Transform3DData _activeTransformRotationPreviewTransform;
    private float _activeTransformRotationGuideRadius;
    private bool _isTransformRotationToolActive;
    private bool _hasPendingTransformRotationPreview;
    private RotationCoordinateSpace _activeTransformRotationCoordinateSpace;

    /// <summary>
    /// Starts a zero-delta rotation session for the single selected imported model.
    /// </summary>
    private void ShowTransformRotationTool()
    {
        MeshEntity? selectedMesh = GetSelectedTransformMesh();

        if (selectedMesh == null)
        {
            ClearTransformRotationToolState();
            HideToolOptionsHostOnly();
            _viewModel.SetStatusText("Select one imported model before rotating.");
            return;
        }

        _activeTransformRotationModelId = selectedMesh.Id;
        _activeTransformRotationImportSpaceOrigin = MeshScaleTransform.CalculateImportSpaceOrigin(selectedMesh);
        _activeTransformRotationOriginalTransform = selectedMesh.UserTransform;
        _activeTransformRotationInputBaselineTransform = selectedMesh.UserTransform;
        _activeTransformRotationPreviewTransform = selectedMesh.UserTransform;
        _activeTransformRotationGuideRadius = CalculateRotationGuideRadius(selectedMesh);
        _isTransformRotationToolActive = true;
        _hasPendingTransformRotationPreview = false;
        _activeTransformRotationCoordinateSpace = RotationCoordinateSpace.World;
        _rotationToolOptionsControl.SetSessionOptions(_activeTransformRotationCoordinateSpace);
        ShowToolOptionsControl(_rotationToolOptionsControl, ToolSessionPanelSet.None);
        ShowRotationOriginPreview();
        _activeToolStatusText = "Transform rotate tool active";
        _viewModel.SetStatusText(_activeToolStatusText);
        _viewModel.SetToolPanelText(_activeToolStatusText);
    }

    /// <summary>
    /// Applies numeric session deltas as a live preview without adding undo history.
    /// </summary>
    private void RotationToolOptionsControl_OptionsChanged(object? sender, RotationToolOptionsChangedEventArgs e)
    {
        _ = sender;
        MeshEntity? selectedMesh = GetActiveTransformRotationMesh();

        if (selectedMesh == null)
        {
            ClearTransformRotationToolState();
            ShowTransformRotationTool();
            return;
        }

        Transform3DData newTransform = MeshRotationTransform.CreateUserTransformForRotation(
            _activeTransformRotationInputBaselineTransform,
            e.RotationDegrees,
            _activeTransformRotationImportSpaceOrigin,
            _activeTransformRotationCoordinateSpace);
        selectedMesh.UserTransform = newTransform;
        _activeTransformRotationPreviewTransform = newTransform;
        _hasPendingTransformRotationPreview = _activeTransformRotationOriginalTransform != newTransform;
        ShowRotationOriginPreview();
        _viewModel.SetStatusText(CreateRotationStatusText(e.RotationDegrees, _activeTransformRotationCoordinateSpace));
    }

    /// <summary>
    /// Preserves the current preview as a temporary baseline when the coordinate-space toggle changes.
    /// </summary>
    private void RotationToolOptionsControl_CoordinateSpaceChanged(object? sender, RotationCoordinateSpaceChangedEventArgs e)
    {
        _ = sender;

        if (!_isTransformRotationToolActive)
        {
            return;
        }

        _activeTransformRotationInputBaselineTransform = _activeTransformRotationPreviewTransform;
        _activeTransformRotationCoordinateSpace = e.CoordinateSpace;
        ShowRotationOriginPreview();
        _viewModel.SetStatusText($"Rotation space changed to {e.CoordinateSpace}. Rotation inputs reset to zero.");
    }

    /// <summary>
    /// Removes all user rotation while retaining scale and the model pivot position.
    /// </summary>
    private void RotationToolOptionsControl_ResetRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        MeshEntity? selectedMesh = GetActiveTransformRotationMesh();

        if (selectedMesh == null)
        {
            ClearTransformRotationToolState();
            ShowTransformRotationTool();
            return;
        }

        Transform3DData resetTransform = MeshRotationTransform.CreateUserTransformForOriginalOrientation(
            selectedMesh.UserTransform,
            _activeTransformRotationImportSpaceOrigin);
        selectedMesh.UserTransform = resetTransform;
        _activeTransformRotationInputBaselineTransform = resetTransform;
        _activeTransformRotationPreviewTransform = resetTransform;
        _hasPendingTransformRotationPreview = _activeTransformRotationOriginalTransform != resetTransform;
        ShowRotationOriginPreview();
        _viewModel.SetStatusText($"Reset model to its imported orientation. {_activeTransformRotationCoordinateSpace} space remains active.");
    }

    /// <summary>
    /// Commits the preview as one undoable command and closes the tool.
    /// </summary>
    private void RotationToolOptionsControl_FinishRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        CommitActiveTransformRotationPreview();
        HideToolOptionsOverlay();
        _viewModel.SetStatusText("Finished rotating model");
    }

    /// <summary>
    /// Discards the preview and closes the tool without creating undo history.
    /// </summary>
    private void RotationToolOptionsControl_CancelRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        HideToolOptionsOverlay();
        _viewModel.SetStatusText("Cancelled model rotation");
    }

    /// <summary>
    /// Refreshes or closes the active session when viewport or Layer Panel selection changes.
    /// </summary>
    private void RefreshTransformRotationToolForSelection()
    {
        if (!_isTransformRotationToolActive)
        {
            return;
        }

        MeshEntity? selectedMesh = GetSelectedTransformMesh();

        if (selectedMesh == null)
        {
            HideToolOptionsOverlay();
            return;
        }

        if (!_activeTransformRotationModelId.HasValue || _activeTransformRotationModelId.Value != selectedMesh.Id)
        {
            ClearTransformRotationToolState();
            ShowTransformRotationTool();
            return;
        }

        ShowRotationOriginPreview();
    }

    /// <summary>
    /// Restores any uncommitted preview and clears all transient rotation session state.
    /// </summary>
    private void ClearTransformRotationToolState()
    {
        RevertPendingTransformRotationPreview();
        _isTransformRotationToolActive = false;
        _activeTransformRotationModelId = null;
        _activeTransformRotationImportSpaceOrigin = Vector3.Zero;
        _activeTransformRotationOriginalTransform = Transform3DData.Identity;
        _activeTransformRotationInputBaselineTransform = Transform3DData.Identity;
        _activeTransformRotationPreviewTransform = Transform3DData.Identity;
        _activeTransformRotationGuideRadius = 0.0f;
        _hasPendingTransformRotationPreview = false;
        _activeTransformRotationCoordinateSpace = RotationCoordinateSpace.World;
        _scene.HideRotationOriginPreview();
    }

    /// <summary>
    /// Converts the live preview into one command and regenerates attached support groups once.
    /// </summary>
    private void CommitActiveTransformRotationPreview()
    {
        if (!_isTransformRotationToolActive || !_activeTransformRotationModelId.HasValue
            || !_hasPendingTransformRotationPreview
            || _activeTransformRotationOriginalTransform == _activeTransformRotationPreviewTransform)
        {
            return;
        }

        MeshEntity? selectedMesh = FindEntityById(_activeTransformRotationModelId.Value) as MeshEntity;

        if (selectedMesh == null)
        {
            _hasPendingTransformRotationPreview = false;
            return;
        }

        Transform3DData oldTransform = _activeTransformRotationOriginalTransform;
        Transform3DData newTransform = _activeTransformRotationPreviewTransform;
        IReadOnlyList<SupportGroupRegeneration> supportRegenerations = SupportGroupTransformRegenerator.CreateRegenerations(
            _document,
            selectedMesh,
            oldTransform,
            newTransform);
        selectedMesh.UserTransform = oldTransform;

        // Mark committed before support collection changes can re-enter Layer Panel refresh code.
        _activeTransformRotationOriginalTransform = newTransform;
        _activeTransformRotationPreviewTransform = newTransform;
        _hasPendingTransformRotationPreview = false;
        _commandRunner.Execute(new SetMeshUserTransformCommand(
            _document,
            selectedMesh,
            oldTransform,
            newTransform,
            supportRegenerations,
            "Rotate Model"));
    }

    /// <summary>
    /// Restores the exact session-start transform when a preview is abandoned.
    /// </summary>
    private void RevertPendingTransformRotationPreview()
    {
        if (!_hasPendingTransformRotationPreview || !_activeTransformRotationModelId.HasValue)
        {
            return;
        }

        MeshEntity? selectedMesh = FindEntityById(_activeTransformRotationModelId.Value) as MeshEntity;

        if (selectedMesh != null)
        {
            selectedMesh.UserTransform = _activeTransformRotationOriginalTransform;
        }

        _activeTransformRotationInputBaselineTransform = _activeTransformRotationOriginalTransform;
        _activeTransformRotationPreviewTransform = _activeTransformRotationOriginalTransform;
        _hasPendingTransformRotationPreview = false;
    }

    /// <summary>
    /// Gets the active target only while it still exists and remains selected.
    /// </summary>
    private MeshEntity? GetActiveTransformRotationMesh()
    {
        if (!_activeTransformRotationModelId.HasValue)
        {
            return null;
        }

        MeshEntity? mesh = FindEntityById(_activeTransformRotationModelId.Value) as MeshEntity;
        MeshEntity? selectedMesh = GetSelectedTransformMesh();
        return mesh != null && selectedMesh != null && selectedMesh.Id == mesh.Id ? mesh : null;
    }

    /// <summary>
    /// Shows fixed world guides or model-oriented local guides at the stable rotation pivot.
    /// </summary>
    private void ShowRotationOriginPreview()
    {
        Vector3 worldOrigin = MeshRotationTransform.CalculateWorldOrigin(
            _activeTransformRotationPreviewTransform,
            _activeTransformRotationImportSpaceOrigin);
        Quaternion guideOrientation = _activeTransformRotationCoordinateSpace == RotationCoordinateSpace.Local
            ? _activeTransformRotationPreviewTransform.Rotation
            : Quaternion.Identity;
        _scene.ShowRotationOriginPreview(worldOrigin, _activeTransformRotationGuideRadius, guideOrientation);
    }

    /// <summary>
    /// Calculates a radius whose diameter is 1.2 times the largest starting bounds dimension.
    /// </summary>
    private static float CalculateRotationGuideRadius(MeshEntity mesh)
    {
        (Vector3 Min, Vector3 Max) bounds = mesh.GetBounds();
        Vector3 size = bounds.Max - bounds.Min;
        float largestDimension = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
        float radius = largestDimension * RotationGuideDiameterModelSizeFactor * 0.5f;
        return radius > 0.0f && !float.IsNaN(radius) && !float.IsInfinity(radius) ? radius : 1.0f;
    }

    /// <summary>
    /// Formats live-preview feedback in the same axis order as the panel.
    /// </summary>
    private static string CreateRotationStatusText(Vector3 rotationDegrees, RotationCoordinateSpace coordinateSpace)
    {
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "Rotating model in {0} space by X {1:0.##} degrees, Y {2:0.##} degrees, Z {3:0.##} degrees",
            coordinateSpace,
            rotationDegrees.X,
            rotationDegrees.Y,
            rotationDegrees.Z);
    }
}
