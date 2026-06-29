// MainWindow.LayerPanel.cs
// Contains shell-level import and layer-panel workflows that translate UI requests into document commands without moving CAD behavior into the window.
using Pillar.Commands;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Geometry.Export;
using Pillar.UI.Layers;
using Pillar.ViewModels;
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

            _commandRunner.Execute(new AddEntityCommand(_document, importedMesh, "Import Mesh"));
            _layerPanelViewModel.SelectModelLayer(importedMesh.Id);
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

        CancelActiveDocumentMutationSessions();
        List<SupportLayerGroup> supportLayerGroups = GetSupportLayerGroupsForModel(selectedModel.Id);
        _commandRunner.Execute(new RemoveModelWithSupportGroupsCommand(_document, selectedModel, supportLayerGroups));
        _layerPanelViewModel.RefreshFromDocument();
        _viewModel.SetStatusText($"Removed {selectedModel.Name}");
    }

    /// <summary>
    /// Exports the requested model layer and all support entities owned by that model to binary STL.
    /// </summary>
    private void LayerPanel_ExportModelRequested(object? sender, LayerModelExportRequestedEventArgs e)
    {
        _ = sender;

        MeshEntity? selectedModel = FindEntityById(e.ModelEntityId) as MeshEntity;

        if (selectedModel == null)
        {
            _layerPanelViewModel.RefreshFromDocument();
            _viewModel.SetStatusText("Export failed; model was not found");
            return;
        }

        SaveFileDialog dialog = new SaveFileDialog
        {
            Title = "Export Model STL",
            Filter = "STL files (*.stl)|*.stl",
            DefaultExt = ".stl",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = CreateDefaultStlExportFileName(selectedModel.Name, "model")
        };

        if (dialog.ShowDialog(this) != true)
        {
            _viewModel.SetStatusText("Export cancelled");
            return;
        }

        try
        {
            List<SupportEntity> supportEntities = GetSupportEntitiesForModel(selectedModel.Id);
            StlExporter exporter = new StlExporter();

            RunWithWaitCursor(() =>
            {
                exporter.ExportModelWithSupports(
                    dialog.FileName,
                    selectedModel,
                    supportEntities,
                    Properties.Settings.Default.SupportSides);
            });

            string fileName = Path.GetFileName(dialog.FileName);
            _viewModel.SetStatusText($"Exported {fileName}");
            _viewModel.SetToolPanelText($"Exported {fileName}");
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ArgumentException || ex is UnauthorizedAccessException)
        {
            _viewModel.SetStatusText("STL export failed");
            MessageBox.Show(this, ex.Message, "STL Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Exports only the support entities that belong to the requested support group.
    /// </summary>
    private void LayerPanel_ExportSupportGroupRequested(object? sender, LayerSupportGroupExportRequestedEventArgs e)
    {
        _ = sender;

        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(e.SupportLayerGroupId);

        if (supportLayerGroup == null)
        {
            _layerPanelViewModel.RefreshFromDocument();
            _viewModel.SetStatusText("Export failed; support group was not found");
            return;
        }

        SaveFileDialog dialog = new SaveFileDialog
        {
            Title = "Export Support Group STL",
            Filter = "STL files (*.stl)|*.stl",
            DefaultExt = ".stl",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = CreateDefaultStlExportFileName(supportLayerGroup.Name, "support-group")
        };

        if (dialog.ShowDialog(this) != true)
        {
            _viewModel.SetStatusText("Export cancelled");
            return;
        }

        try
        {
            IReadOnlyList<SupportEntity> supportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
            StlExporter exporter = new StlExporter();

            RunWithWaitCursor(() =>
            {
                exporter.ExportSupports(
                    dialog.FileName,
                    supportEntities,
                    Properties.Settings.Default.SupportSides);
            });

            string fileName = Path.GetFileName(dialog.FileName);
            _viewModel.SetStatusText($"Exported {fileName}");
            _viewModel.SetToolPanelText($"Exported {fileName}");
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ArgumentException || ex is UnauthorizedAccessException)
        {
            _viewModel.SetStatusText("STL export failed");
            MessageBox.Show(this, ex.Message, "STL Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

        CancelActiveDocumentMutationSessions();
        _commandRunner.Execute(new RemoveSupportLayerGroupCommand(_document, supportLayerGroup));
        _viewModel.SetStatusText($"Removed {supportLayerGroup.Name}");
    }

    /// <summary>
    /// Applies a completed layer rename as one undoable command.
    /// </summary>
    private void LayerPanel_RenameLayerRequested(object? sender, LayerRenameRequestedEventArgs e)
    {
        if (e.LayerKind == LayerTreeItemKind.Model)
        {
            RenameModelLayer(e);
            return;
        }

        if (e.LayerKind == LayerTreeItemKind.SupportGroup)
        {
            RenameSupportGroupLayer(e);
        }
    }

    /// <summary>
    /// Applies a completed model layer rename as one undoable command.
    /// </summary>
    private void RenameModelLayer(LayerRenameRequestedEventArgs e)
    {
        MeshEntity? mesh = FindEntityById(e.LayerId) as MeshEntity;

        if (mesh == null)
        {
            _layerPanelViewModel.RefreshFromDocument();
            return;
        }

        string oldName = NormalizeModelName(mesh.Name);
        string newName = NormalizeModelName(e.NewName);

        if (string.Equals(oldName, newName, StringComparison.Ordinal))
        {
            _layerPanelViewModel.RefreshFromDocument();
            return;
        }

        _commandRunner.Execute(new RenameModelCommand(mesh, oldName, newName));
        _layerPanelViewModel.RefreshFromDocument();
        _viewModel.SetStatusText("Renamed model");
    }

    /// <summary>
    /// Applies a completed support group rename as one undoable command.
    /// </summary>
    private void RenameSupportGroupLayer(LayerRenameRequestedEventArgs e)
    {
        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(e.LayerId);

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
    /// Normalizes imported model names entered through inline layer editing.
    /// </summary>
    private static string NormalizeModelName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Imported mesh";
        }

        return name.Trim();
    }

    /// <summary>
    /// Applies a model or support group layer visibility request to session UI and render state.
    /// </summary>
    private void LayerPanel_ChangeLayerVisibilityRequested(object? sender, LayerVisibilityChangeRequestedEventArgs e)
    {
        _ = sender;

        if (e.LayerKind == LayerTreeItemKind.Model)
        {
            SetModelLayerVisibility(e.LayerId, e.IsVisible);
            return;
        }

        if (e.LayerKind == LayerTreeItemKind.SupportGroup)
        {
            SetSupportLayerVisibility(e.LayerId, e.IsVisible);
        }
    }

    /// <summary>
    /// Applies one model layer visibility value to the Layer Panel and viewport.
    /// </summary>
    private void SetModelLayerVisibility(Guid modelEntityId, bool isVisible)
    {
        MeshEntity? mesh = FindEntityById(modelEntityId) as MeshEntity;

        if (mesh == null)
        {
            _layerPanelViewModel.RefreshFromDocument();
            return;
        }

        _layerPanelViewModel.SetModelLayerVisibility(modelEntityId, isVisible);
        _scene.SetModelLayerVisibility(modelEntityId, isVisible);
        _viewModel.SetStatusText(isVisible ? $"Showed {mesh.Name}" : $"Hid {mesh.Name}");
    }

    /// <summary>
    /// Applies one support group layer visibility value to the Layer Panel and viewport.
    /// </summary>
    private void SetSupportLayerVisibility(Guid supportLayerGroupId, bool isVisible)
    {
        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(supportLayerGroupId);

        if (supportLayerGroup == null)
        {
            _layerPanelViewModel.RefreshFromDocument();
            return;
        }

        _layerPanelViewModel.SetSupportLayerVisibility(supportLayerGroupId, isVisible);
        _scene.SetSupportLayerGroupVisibility(supportLayerGroupId, isVisible);
        _viewModel.SetStatusText(isVisible ? $"Showed {supportLayerGroup.Name}" : $"Hid {supportLayerGroup.Name}");
    }

    /// <summary>
    /// Shows one support layer and hides all other support layers for focused editing workflows.
    /// </summary>
    private void ShowOnlySupportLayer(Guid visibleSupportLayerGroupId)
    {
        foreach (SupportLayerGroup supportLayerGroup in _document.SupportLayerGroups)
        {
            bool isVisible = supportLayerGroup.Id == visibleSupportLayerGroupId;
            _layerPanelViewModel.SetSupportLayerVisibility(supportLayerGroup.Id, isVisible);
            _scene.SetSupportLayerGroupVisibility(supportLayerGroup.Id, isVisible);
        }
    }

    /// <summary>
    /// Captures current support-layer visibility and focuses the viewport on one support layer for the Cluster tool.
    /// </summary>
    private void FocusSupportLayerForClusterTool(Guid visibleSupportLayerGroupId)
    {
        if (_clusterToolSupportLayerVisibilitySnapshot == null)
        {
            _clusterToolSupportLayerVisibilitySnapshot = CaptureSupportLayerVisibility();
        }

        ShowOnlySupportLayer(visibleSupportLayerGroupId);
    }

    /// <summary>
    /// Restores support-layer visibility captured before the Cluster tool focused one layer.
    /// </summary>
    private void RestoreSupportLayerVisibilityAfterClusterTool()
    {
        if (_clusterToolSupportLayerVisibilitySnapshot == null)
        {
            return;
        }

        foreach (SupportLayerGroup supportLayerGroup in _document.SupportLayerGroups)
        {
            bool isVisible = true;

            if (_clusterToolSupportLayerVisibilitySnapshot.TryGetValue(supportLayerGroup.Id, out bool capturedVisibility))
            {
                isVisible = capturedVisibility;
            }

            _layerPanelViewModel.SetSupportLayerVisibility(supportLayerGroup.Id, isVisible);
            _scene.SetSupportLayerGroupVisibility(supportLayerGroup.Id, isVisible);
        }

        _clusterToolSupportLayerVisibilitySnapshot = null;
    }

    /// <summary>
    /// Captures current session visibility for every support layer before a focused editing workflow changes it.
    /// </summary>
    private Dictionary<Guid, bool> CaptureSupportLayerVisibility()
    {
        Dictionary<Guid, bool> visibilityBySupportLayerId = new Dictionary<Guid, bool>();

        foreach (SupportLayerGroup supportLayerGroup in _document.SupportLayerGroups)
        {
            visibilityBySupportLayerId.Add(supportLayerGroup.Id, _layerPanelViewModel.GetSupportLayerVisibility(supportLayerGroup.Id));
        }

        return visibilityBySupportLayerId;
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

    /// <summary>
    /// Captures the support entities owned by all support groups under one imported model.
    /// </summary>
    private List<SupportEntity> GetSupportEntitiesForModel(Guid modelEntityId)
    {
        List<SupportEntity> supportEntities = new List<SupportEntity>();
        List<SupportLayerGroup> supportLayerGroups = GetSupportLayerGroupsForModel(modelEntityId);

        foreach (SupportLayerGroup supportLayerGroup in supportLayerGroups)
        {
            IReadOnlyList<SupportEntity> groupSupportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);

            foreach (SupportEntity supportEntity in groupSupportEntities)
            {
                supportEntities.Add(supportEntity);
            }
        }

        return supportEntities;
    }

    /// <summary>
    /// Creates a filesystem-safe default STL filename from a layer display name.
    /// </summary>
    private static string CreateDefaultStlExportFileName(string displayName, string fallbackName)
    {
        string fileNameWithoutExtension = string.IsNullOrWhiteSpace(displayName) ? fallbackName : displayName.Trim();
        char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

        foreach (char invalidFileNameChar in invalidFileNameChars)
        {
            fileNameWithoutExtension = fileNameWithoutExtension.Replace(invalidFileNameChar, '_');
        }

        return $"{fileNameWithoutExtension}.stl";
    }
}
