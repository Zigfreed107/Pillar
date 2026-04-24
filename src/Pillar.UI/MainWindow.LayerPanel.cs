// MainWindow.LayerPanel.cs
// Contains shell-level import and layer-panel workflows that translate UI requests into document commands without moving CAD behavior into the window.
using Pillar.Commands;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.UI.Layers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Media.Media3D;

namespace Pillar.UI;

public partial class MainWindow
{
    /// <summary>
    /// Imports one STL mesh into the document and lets the scene manager render it incrementally.
    /// </summary>
    private void ImportStlButton_Click(object sender, RoutedEventArgs e)
    {
        ImportModelFromDialog();
    }

    /// <summary>
    /// Imports a model from the shared file dialog used by both the File menu and Layer Panel empty state.
    /// </summary>
    private void ImportModelFromDialog()
    {
        OpenFileDialog dialog = new OpenFileDialog
        {
            Title = "Import STL",
            Filter = "STL files (*.stl)|*.stl|All files (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            CadEntity importedEntity = _stlImporter.Import(dialog.FileName);

            if (importedEntity is not MeshEntity importedMesh)
            {
                throw new InvalidDataException("Only mesh model imports can be added to the Layer Panel.");
            }

            (Vector3 Min, Vector3 Max) localBounds = importedMesh.GetLocalBounds();
            importedMesh.ImportPlacementTransform = Transform3DData.CreateTranslation(new Vector3(0.0f, 0.0f, -localBounds.Min.Z));
            importedMesh.UserTransform = Transform3DData.CreateTranslation(new Vector3(
                0.0f,
                0.0f,
                (float)Properties.Settings.Default.DefaultModelZStandoffFromOrigin));

            SupportLayerGroup initialSupportLayerGroup = new SupportLayerGroup(importedMesh.Id, "Supports Group 1");
            _commandRunner.Execute(new ImportMeshWithSupportGroupCommand(_document, importedMesh, initialSupportLayerGroup));
            FrameEntityInViewport(importedMesh);

            string fileName = Path.GetFileName(dialog.FileName);
            _viewModel.SetStatusText($"Imported {fileName}");
            _viewModel.SetToolPanelText($"Imported {fileName}");
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ArgumentException)
        {
            _viewModel.SetStatusText("STL import failed");
            MessageBox.Show(this, ex.Message, "STL Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Runs the shared import workflow from the Layer Panel empty state.
    /// </summary>
    private void LayerPanel_ImportModelRequested(object? sender, EventArgs e)
    {
        ImportModelFromDialog();
    }

    /// <summary>
    /// Frames one entity in the viewport using its domain bounds after scene updates complete.
    /// </summary>
    private void FrameEntityInViewport(CadEntity entity)
    {
        if (entity == null)
        {
            return;
        }

        (Vector3 Min, Vector3 Max) bounds = entity.GetBounds();
        double width = global::System.Math.Max(bounds.Max.X - bounds.Min.X, 0.01f);
        double height = global::System.Math.Max(bounds.Max.Y - bounds.Min.Y, 0.01f);
        double depth = global::System.Math.Max(bounds.Max.Z - bounds.Min.Z, 0.01f);
        Rect3D viewportBounds = new Rect3D(bounds.Min.X, bounds.Min.Y, bounds.Min.Z, width, height, depth);

        Viewport.ZoomExtents(viewportBounds, 0.0);
    }

    /// <summary>
    /// Removes the selected imported model and all support groups owned by it after user confirmation.
    /// </summary>
    private void LayerPanel_RemoveModelRequested(object? sender, EventArgs e)
    {
        Guid? selectedModelEntityId = _layerPanelViewModel.GetSelectedModelEntityId();

        if (!selectedModelEntityId.HasValue)
        {
            return;
        }

        MeshEntity? selectedModel = FindEntityById(selectedModelEntityId.Value) as MeshEntity;

        if (selectedModel == null)
        {
            _layerPanelViewModel.RefreshFromDocument();
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            this,
            $"The model '{selectedModel.Name}' and all of its supports will be permanently deleted from the project.",
            "Remove Model",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
        {
            _viewModel.SetStatusText("Remove model cancelled");
            return;
        }

        List<SupportLayerGroup> supportLayerGroups = GetSupportLayerGroupsForModel(selectedModel.Id);
        _commandRunner.Execute(new RemoveModelWithSupportGroupsCommand(_document, selectedModel, supportLayerGroups));
        _layerPanelViewModel.RefreshFromDocument();
        RefreshPropertiesPanelFromSelection();
        _viewModel.SetStatusText($"Removed {selectedModel.Name}");
    }

    /// <summary>
    /// Adds a support group under the selected imported model layer.
    /// </summary>
    private void LayerPanel_AddSupportGroupRequested(object? sender, EventArgs e)
    {
        Guid? selectedModelEntityId = _layerPanelViewModel.GetSelectedModelEntityId();

        if (!selectedModelEntityId.HasValue)
        {
            return;
        }

        string supportGroupName = _layerPanelViewModel.CreateNextSupportGroupName(selectedModelEntityId.Value);
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(selectedModelEntityId.Value, supportGroupName);

        _commandRunner.Execute(new AddSupportLayerGroupCommand(_document, supportLayerGroup));
        _viewModel.SetStatusText($"Added {supportGroupName}");
    }

    /// <summary>
    /// Removes the selected support group layer without deleting the imported model.
    /// </summary>
    private void LayerPanel_RemoveSupportGroupRequested(object? sender, EventArgs e)
    {
        Guid? selectedSupportLayerGroupId = _layerPanelViewModel.GetSelectedSupportLayerGroupId();

        if (!selectedSupportLayerGroupId.HasValue)
        {
            return;
        }

        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(selectedSupportLayerGroupId.Value);

        if (supportLayerGroup == null)
        {
            _layerPanelViewModel.RefreshFromDocument();
            return;
        }

        _commandRunner.Execute(new RemoveSupportLayerGroupCommand(_document, supportLayerGroup));
        _viewModel.SetStatusText($"Removed {supportLayerGroup.Name}");
    }

    /// <summary>
    /// Applies a completed support group rename as one undoable command.
    /// </summary>
    private void LayerPanel_RenameSupportGroupRequested(object? sender, LayerRenameRequestedEventArgs e)
    {
        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(e.SupportLayerGroupId);

        if (supportLayerGroup == null)
        {
            _layerPanelViewModel.RefreshFromDocument();
            return;
        }

        string oldName = NormalizeSupportGroupName(supportLayerGroup.Name);
        string newName = NormalizeSupportGroupName(e.NewName);

        if (string.Equals(oldName, newName, StringComparison.Ordinal))
        {
            _layerPanelViewModel.RefreshFromDocument();
            return;
        }

        _commandRunner.Execute(new RenameSupportLayerGroupCommand(_document, supportLayerGroup, oldName, newName));
        _layerPanelViewModel.RefreshFromDocument();
        _viewModel.SetStatusText("Renamed support group");
    }

    /// <summary>
    /// Applies a completed support group color change as one undoable command.
    /// </summary>
    private void LayerPanel_ChangeSupportGroupColorRequested(object? sender, LayerColorChangeRequestedEventArgs e)
    {
        _ = sender;

        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(e.SupportLayerGroupId);

        if (supportLayerGroup == null)
        {
            _layerPanelViewModel.RefreshFromDocument();
            return;
        }

        if (supportLayerGroup.Color == e.NewColor)
        {
            _layerPanelViewModel.RefreshFromDocument();
            return;
        }

        _commandRunner.Execute(new SetSupportLayerGroupColorCommand(_document, supportLayerGroup, e.OldColor, e.NewColor));
        _layerPanelViewModel.RefreshFromDocument();
        _viewModel.SetStatusText("Changed support group color");
    }

    /// <summary>
    /// Captures the support groups owned by one imported model before the model is removed.
    /// </summary>
    private List<SupportLayerGroup> GetSupportLayerGroupsForModel(Guid modelEntityId)
    {
        List<SupportLayerGroup> supportLayerGroups = new List<SupportLayerGroup>();

        foreach (SupportLayerGroup supportLayerGroup in _document.SupportLayerGroups)
        {
            if (supportLayerGroup.ModelEntityId == modelEntityId)
            {
                supportLayerGroups.Add(supportLayerGroup);
            }
        }

        return supportLayerGroups;
    }
}
