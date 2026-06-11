// MainWindow.TransformScale.cs
// Hosts shell wiring for the Transform Scale tool while keeping transform math in Core and preview drawing in Rendering.
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
    private const string TransformScaleToolName = "Scale";
    private const float MinimumScaleOriginPreviewRadius = 0.25f;
    private const float MaximumScaleOriginPreviewRadius = 10.0f;
    private const float ScaleOriginPreviewModelSizeFactor = 0.05f;

    private Guid? _activeTransformScaleModelId;
    private Vector3 _activeTransformScaleImportSpaceOrigin;
    private Transform3DData _activeTransformScaleOriginalTransform;
    private Transform3DData _activeTransformScalePreviewTransform;
    private bool _isTransformScaleToolActive;
    private bool _hasPendingTransformScalePreview;

    /// <summary>
    /// Opens Transform Scale options for the selected model and shows the scale-origin preview.
    /// </summary>
    private void ShowTransformScaleTool()
    {
        MeshEntity? selectedMesh = GetSelectedTransformScaleMesh();

        if (selectedMesh == null)
        {
            ClearTransformScaleToolState();
            HideToolOptionsHostOnly();
            _viewModel.SetStatusText("Select one imported model before scaling.");
            return;
        }

        _activeTransformScaleModelId = selectedMesh.Id;
        _activeTransformScaleImportSpaceOrigin = MeshScaleTransform.CalculateImportSpaceOrigin(selectedMesh);
        _activeTransformScaleOriginalTransform = selectedMesh.UserTransform;
        _activeTransformScalePreviewTransform = selectedMesh.UserTransform;
        _isTransformScaleToolActive = true;
        _hasPendingTransformScalePreview = false;

        _scaleToolOptionsControl.SetOriginalSize(MeshScaleTransform.CalculateImportSpaceSize(selectedMesh));
        _scaleToolOptionsControl.SetScaleFactors(selectedMesh.UserTransform.Scale);
        ShowToolOptionsControl(_scaleToolOptionsControl, ToolSessionPanelSet.None);
        ShowScaleOriginPreview(selectedMesh);

        _activeToolStatusText = "Transform scale tool active";
        _viewModel.SetStatusText(_activeToolStatusText);
        _viewModel.SetToolPanelText(_activeToolStatusText);
    }

    /// <summary>
    /// Applies one scale edit from the options panel as a live preview without creating undo history.
    /// </summary>
    private void ScaleToolOptionsControl_OptionsChanged(object? sender, ScaleToolOptionsChangedEventArgs e)
    {
        _ = sender;

        MeshEntity? selectedMesh = GetActiveTransformScaleMesh();

        if (selectedMesh == null)
        {
            ClearTransformScaleToolState();
            ShowTransformScaleTool();
            return;
        }

        Transform3DData newTransform = MeshScaleTransform.CreateUserTransformForScale(
            selectedMesh,
            e.ScaleFactors,
            _activeTransformScaleImportSpaceOrigin);

        if (selectedMesh.UserTransform == newTransform)
        {
            return;
        }

        selectedMesh.UserTransform = newTransform;
        _activeTransformScalePreviewTransform = newTransform;
        _hasPendingTransformScalePreview = _activeTransformScaleOriginalTransform != newTransform;
        ShowScaleOriginPreview(selectedMesh);
        _viewModel.SetStatusText(CreateScaleStatusText(e.ScaleFactors));
    }

    /// <summary>
    /// Closes Transform Scale options when the user presses Finish.
    /// </summary>
    private void ScaleToolOptionsControl_FinishRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        CommitActiveTransformScalePreview();
        HideToolOptionsOverlay();
        _viewModel.SetStatusText("Finished scaling model");
    }

    /// <summary>
    /// Refreshes the active scale UI when selection changes while the tool is open.
    /// </summary>
    private void RefreshTransformScaleToolForSelection()
    {
        if (!_isTransformScaleToolActive)
        {
            return;
        }

        MeshEntity? selectedMesh = GetSelectedTransformScaleMesh();

        if (selectedMesh == null)
        {
            HideToolOptionsOverlay();
            return;
        }

        if (!_activeTransformScaleModelId.HasValue || _activeTransformScaleModelId.Value != selectedMesh.Id)
        {
            ShowTransformScaleTool();
            return;
        }

        _scaleToolOptionsControl.SetOriginalSize(MeshScaleTransform.CalculateImportSpaceSize(selectedMesh));
        _scaleToolOptionsControl.SetScaleFactors(selectedMesh.UserTransform.Scale);
        ShowScaleOriginPreview(selectedMesh);
    }

    /// <summary>
    /// Clears transient scale-tool state and hides the visual-only origin marker.
    /// </summary>
    private void ClearTransformScaleToolState()
    {
        if (!_isTransformScaleToolActive && !_activeTransformScaleModelId.HasValue)
        {
            _scene.HideScaleOriginPreview();
            return;
        }

        RevertPendingTransformScalePreview();
        _isTransformScaleToolActive = false;
        _activeTransformScaleModelId = null;
        _activeTransformScaleImportSpaceOrigin = Vector3.Zero;
        _activeTransformScaleOriginalTransform = Transform3DData.Identity;
        _activeTransformScalePreviewTransform = Transform3DData.Identity;
        _hasPendingTransformScalePreview = false;
        _scene.HideScaleOriginPreview();
    }

    /// <summary>
    /// Commits the live scale preview as one undoable command and regenerates attached supports once.
    /// </summary>
    private void CommitActiveTransformScalePreview()
    {
        if (!_isTransformScaleToolActive
            || !_activeTransformScaleModelId.HasValue
            || !_hasPendingTransformScalePreview
            || _activeTransformScaleOriginalTransform == _activeTransformScalePreviewTransform)
        {
            return;
        }

        MeshEntity? selectedMesh = FindEntityById(_activeTransformScaleModelId.Value) as MeshEntity;

        if (selectedMesh == null)
        {
            _hasPendingTransformScalePreview = false;
            return;
        }

        Transform3DData oldTransform = _activeTransformScaleOriginalTransform;
        Transform3DData newTransform = _activeTransformScalePreviewTransform;
        IReadOnlyList<SupportGroupRegeneration> supportRegenerations = SupportGroupTransformRegenerator.CreateRegenerations(
            _document,
            selectedMesh,
            oldTransform,
            newTransform);

        // Return the model to the pre-preview state so the command owns the durable document mutation.
        selectedMesh.UserTransform = oldTransform;
        _commandRunner.Execute(new SetMeshUserTransformCommand(
            _document,
            selectedMesh,
            oldTransform,
            newTransform,
            supportRegenerations,
            "Scale Model"));

        _activeTransformScaleOriginalTransform = newTransform;
        _activeTransformScalePreviewTransform = newTransform;
        _hasPendingTransformScalePreview = false;
    }

    /// <summary>
    /// Restores the model transform when a live scale preview is abandoned before Finish.
    /// </summary>
    private void RevertPendingTransformScalePreview()
    {
        if (!_hasPendingTransformScalePreview || !_activeTransformScaleModelId.HasValue)
        {
            return;
        }

        MeshEntity? selectedMesh = FindEntityById(_activeTransformScaleModelId.Value) as MeshEntity;

        if (selectedMesh != null)
        {
            selectedMesh.UserTransform = _activeTransformScaleOriginalTransform;
        }

        _activeTransformScalePreviewTransform = _activeTransformScaleOriginalTransform;
        _hasPendingTransformScalePreview = false;
    }

    /// <summary>
    /// Gets the mesh currently selected for starting or refreshing the scale tool.
    /// </summary>
    private MeshEntity? GetSelectedTransformScaleMesh()
    {
        Guid? selectedModelEntityId = _layerPanelViewModel.GetSelectedModelEntityId();

        if (selectedModelEntityId.HasValue && FindEntityById(selectedModelEntityId.Value) is MeshEntity layerSelectedMesh)
        {
            return layerSelectedMesh;
        }

        if (_scene.SelectionManager.SelectedCount != 1)
        {
            return null;
        }

        Guid? selectedEntityId = GetSingleSelectedEntityId();

        if (selectedEntityId.HasValue && FindEntityById(selectedEntityId.Value) is MeshEntity viewportSelectedMesh)
        {
            return viewportSelectedMesh;
        }

        return null;
    }

    /// <summary>
    /// Gets the active scale target, if it still exists in the document and remains selected.
    /// </summary>
    private MeshEntity? GetActiveTransformScaleMesh()
    {
        if (!_activeTransformScaleModelId.HasValue)
        {
            return null;
        }

        MeshEntity? mesh = FindEntityById(_activeTransformScaleModelId.Value) as MeshEntity;

        if (mesh == null)
        {
            return null;
        }

        MeshEntity? selectedMesh = GetSelectedTransformScaleMesh();

        if (selectedMesh == null || selectedMesh.Id != mesh.Id)
        {
            return null;
        }

        return mesh;
    }

    /// <summary>
    /// Updates the viewport origin marker for the supplied mesh.
    /// </summary>
    private void ShowScaleOriginPreview(MeshEntity mesh)
    {
        Vector3 worldOrigin = MeshScaleTransform.CalculateWorldOrigin(mesh, _activeTransformScaleImportSpaceOrigin);
        _scene.ShowScaleOriginPreview(worldOrigin, CalculateScaleOriginPreviewRadius(mesh));
    }

    /// <summary>
    /// Chooses a marker size proportional to the model while keeping very small or large models readable.
    /// </summary>
    private static float CalculateScaleOriginPreviewRadius(MeshEntity mesh)
    {
        (Vector3 Min, Vector3 Max) bounds = mesh.GetBounds();
        Vector3 size = bounds.Max - bounds.Min;
        float largestDimension = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
        float radius = largestDimension * ScaleOriginPreviewModelSizeFactor;

        if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0.0f)
        {
            return MinimumScaleOriginPreviewRadius;
        }

        return MathF.Min(MaximumScaleOriginPreviewRadius, MathF.Max(MinimumScaleOriginPreviewRadius, radius));
    }

    /// <summary>
    /// Creates concise status text using percentages because the UI treats original size as 100%.
    /// </summary>
    private static string CreateScaleStatusText(Vector3 scaleFactors)
    {
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "Scaled model to X {0:0.#}%, Y {1:0.#}%, Z {2:0.#}%",
            scaleFactors.X * 100.0f,
            scaleFactors.Y * 100.0f,
            scaleFactors.Z * 100.0f);
    }
}
