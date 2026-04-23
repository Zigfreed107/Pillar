// LayerPanelViewModel.cs
// Builds the Layer Panel tree from document entities and support groups without introducing rendering dependencies.
using CommunityToolkit.Mvvm.ComponentModel;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Pillar.ViewModels;

/// <summary>
/// Provides bindable state for the viewport Layer Panel overlay.
/// </summary>
public partial class LayerPanelViewModel : ObservableObject
{
    private readonly CadDocument _document;
    private LayerTreeItemViewModel? _selectedLayer;

    /// <summary>
    /// Creates a Layer Panel model that mirrors the supplied document.
    /// </summary>
    public LayerPanelViewModel(CadDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _document.EntitiesChanged += OnDocumentStructureChanged;
        _document.SupportLayerGroupsChanged += OnSupportLayerGroupsChanged;
        SubscribeToExistingSupportLayerGroups();

        RefreshFromDocument();
    }

    /// <summary>
    /// Gets the imported model layer rows shown by the tree.
    /// </summary>
    public ObservableCollection<LayerTreeItemViewModel> ModelLayers { get; } = new ObservableCollection<LayerTreeItemViewModel>();

    /// <summary>
    /// Gets whether the document has imported model layers to show.
    /// </summary>
    public bool HasImportedModels
    {
        get { return ModelLayers.Count > 0; }
    }

    /// <summary>
    /// Gets whether the add button can create a support group under the selected model.
    /// </summary>
    public bool CanAddSupportGroup
    {
        get { return _selectedLayer != null && _selectedLayer.Kind == LayerTreeItemKind.Model; }
    }

    /// <summary>
    /// Gets whether the add model button can start the model import workflow.
    /// </summary>
    public bool CanAddModel
    {
        get { return true; }
    }

    /// <summary>
    /// Gets whether the remove model button can delete the selected imported model.
    /// </summary>
    public bool CanRemoveModel
    {
        get { return _selectedLayer != null && _selectedLayer.Kind == LayerTreeItemKind.Model; }
    }

    /// <summary>
    /// Gets whether the remove button can delete the selected support group.
    /// </summary>
    public bool CanRemoveSupportGroup
    {
        get { return _selectedLayer != null && _selectedLayer.Kind == LayerTreeItemKind.SupportGroup; }
    }

    /// <summary>
    /// Gets the selected layer tree row.
    /// </summary>
    public LayerTreeItemViewModel? SelectedLayer
    {
        get { return _selectedLayer; }
        private set
        {
            if (SetProperty(ref _selectedLayer, value))
            {
                OnPropertyChanged(nameof(CanAddSupportGroup));
                OnPropertyChanged(nameof(CanRemoveSupportGroup));
                OnPropertyChanged(nameof(CanRemoveModel));
            }
        }
    }

    /// <summary>
    /// Rebuilds the layer tree from the current document entities and support groups.
    /// </summary>
    public void RefreshFromDocument()
    {
        Guid? selectedId = SelectedLayer?.Id;
        LayerTreeItemKind? selectedKind = SelectedLayer?.Kind;

        ModelLayers.Clear();

        Dictionary<Guid, LayerTreeItemViewModel> modelRowsById = new Dictionary<Guid, LayerTreeItemViewModel>();

        foreach (CadEntity entity in _document.Entities)
        {
            if (entity is MeshEntity)
            {
                LayerTreeItemViewModel modelRow = new LayerTreeItemViewModel(
                    entity.Id,
                    entity.Id,
                    LayerTreeItemKind.Model,
                    entity.Name,
                    default);

                modelRowsById.Add(entity.Id, modelRow);
                ModelLayers.Add(modelRow);
            }
        }

        foreach (SupportLayerGroup supportLayerGroup in _document.SupportLayerGroups)
        {
            if (modelRowsById.TryGetValue(supportLayerGroup.ModelEntityId, out LayerTreeItemViewModel? modelRow))
            {
                LayerTreeItemViewModel supportGroupRow = new LayerTreeItemViewModel(
                    supportLayerGroup.Id,
                    supportLayerGroup.ModelEntityId,
                    LayerTreeItemKind.SupportGroup,
                    supportLayerGroup.Name,
                    supportLayerGroup.Color);

                modelRow.Children.Add(supportGroupRow);
            }
        }

        SelectedLayer = FindLayer(selectedId, selectedKind);
        OnPropertyChanged(nameof(HasImportedModels));
    }

    /// <summary>
    /// Updates selection state from the WPF TreeView.
    /// </summary>
    public void SetSelectedLayer(LayerTreeItemViewModel? selectedLayer)
    {
        SelectedLayer = selectedLayer;
    }

    /// <summary>
    /// Gets the selected imported model id when a top-level layer is selected.
    /// </summary>
    public Guid? GetSelectedModelEntityId()
    {
        if (SelectedLayer == null || SelectedLayer.Kind != LayerTreeItemKind.Model)
        {
            return null;
        }

        return SelectedLayer.ModelEntityId;
    }

    /// <summary>
    /// Gets the selected support group id when a child support layer is selected.
    /// </summary>
    public Guid? GetSelectedSupportLayerGroupId()
    {
        if (SelectedLayer == null || SelectedLayer.Kind != LayerTreeItemKind.SupportGroup)
        {
            return null;
        }

        return SelectedLayer.Id;
    }

    /// <summary>
    /// Finds the next available default support group name under one imported model.
    /// </summary>
    public string CreateNextSupportGroupName(Guid modelEntityId)
    {
        HashSet<string> existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (SupportLayerGroup supportLayerGroup in _document.SupportLayerGroups)
        {
            if (supportLayerGroup.ModelEntityId == modelEntityId)
            {
                existingNames.Add(supportLayerGroup.Name);
            }
        }

        int index = 1;

        while (true)
        {
            string candidateName = $"Supports Group {index}";

            if (!existingNames.Contains(candidateName))
            {
                return candidateName;
            }

            index++;
        }
    }

    /// <summary>
    /// Finds a layer row by id and kind in the current tree.
    /// </summary>
    private LayerTreeItemViewModel? FindLayer(Guid? id, LayerTreeItemKind? kind)
    {
        if (!id.HasValue || !kind.HasValue)
        {
            return null;
        }

        foreach (LayerTreeItemViewModel modelLayer in ModelLayers)
        {
            if (modelLayer.Id == id.Value && modelLayer.Kind == kind.Value)
            {
                return modelLayer;
            }

            foreach (LayerTreeItemViewModel childLayer in modelLayer.Children)
            {
                if (childLayer.Id == id.Value && childLayer.Kind == kind.Value)
                {
                    return childLayer;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Refreshes the tree when document entities are added or removed.
    /// </summary>
    private void OnDocumentStructureChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RefreshFromDocument();
    }

    /// <summary>
    /// Subscribes to support group property changes when groups are added or removed.
    /// </summary>
    private void OnSupportLayerGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;

        if (e.NewItems != null)
        {
            foreach (SupportLayerGroup supportLayerGroup in e.NewItems)
            {
                supportLayerGroup.PropertyChanged += SupportLayerGroup_PropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (SupportLayerGroup supportLayerGroup in e.OldItems)
            {
                supportLayerGroup.PropertyChanged -= SupportLayerGroup_PropertyChanged;
            }
        }

        RefreshFromDocument();
    }

    /// <summary>
    /// Refreshes the tree when support group name or color changes.
    /// </summary>
    private void SupportLayerGroup_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RefreshFromDocument();
    }

    /// <summary>
    /// Subscribes to support groups that already exist when the view model is created.
    /// </summary>
    private void SubscribeToExistingSupportLayerGroups()
    {
        foreach (SupportLayerGroup supportLayerGroup in _document.SupportLayerGroups)
        {
            supportLayerGroup.PropertyChanged += SupportLayerGroup_PropertyChanged;
        }
    }
}
