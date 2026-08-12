// MainWindow.TransformTranslate.cs
// Hosts Transform Translate actions while keeping vertex transform math in Core and mutations in command history.
using Pillar.Commands;
using Pillar.Core.Entities;
using Pillar.Core.Supports;
using Pillar.Geometry.Supports;
using Pillar.UI.Modes;
using System;
using System.Collections.Generic;

namespace Pillar.UI;

public partial class MainWindow
{
    private const string TransformTranslateToolName = "Translate";
    private const string TransformMoveToPlateActionName = "Move to Plate";

    /// <summary>
    /// Opens Transform Translate options for the selected model.
    /// </summary>
    private void ShowTransformTranslateTool()
    {
        if (GetSelectedTransformMesh() == null)
        {
            HideToolOptionsHostOnly();
            _viewModel.SetStatusText("Select one imported model before translating.");
            return;
        }

        ShowToolOptionsControl(_translateToolOptionsControl, ToolSessionPanelSet.None);
        _activeToolStatusText = "Transform translate tool active";
        _viewModel.SetStatusText(_activeToolStatusText);
        _viewModel.SetToolPanelText(_activeToolStatusText);
    }

    /// <summary>
    /// Places the selected model on the build plate from the Translate options panel.
    /// </summary>
    private void TranslateToolOptionsControl_MoveToPlateRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        MoveSelectedTransformMeshToPlate();
    }

    /// <summary>
    /// Closes Transform Translate options when the user presses Finish.
    /// </summary>
    private void TranslateToolOptionsControl_FinishRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        HideToolOptionsOverlay();
        _viewModel.SetStatusText("Finished translating model");
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
}
