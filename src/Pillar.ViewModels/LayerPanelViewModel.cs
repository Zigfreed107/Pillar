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
    private readonly Dictionary<Guid, bool> _modelLayerVisibilityById = new Dictionary<Guid, bool>();
    private readonly Dictionary<Guid, bool> _supportLayerVisibilityById = new Dictionary<Guid, bool>();
    private readonly Dictionary<Guid, bool> _raftLayerVisibilityById = new Dictionary<Guid, bool>();
    private readonly HashSet<CadEntity> _subscribedEntities = new HashSet<CadEntity>();
    private Guid? _raftTargetModelEntityId;
    private LayerTreeItemViewModel? _selectedLayer;
    private int _selectedModelCount;
    private int _selectedSupportLayerGroupCount;

    /// <summary>
    /// Creates a Layer Panel model that mirrors the supplied document.
    /// </summary>
    public LayerPanelViewModel(CadDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _document.EntitiesChanged += OnDocumentStructureChanged;
        _document.SupportLayerGroupsChanged += OnSupportLayerGroupsChanged;
        RefreshEntitySubscriptions();
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
    /// Gets whether a top-level model row is currently selected.
    /// </summary>
    public bool HasSelectedModelLayer
    {
        get { return _selectedLayer != null && _selectedLayer.Kind == LayerTreeItemKind.Model; }
    }

    /// <summary>
    /// Gets whether a support group row is currently selected.
    /// </summary>
    public bool HasSelectedSupportGroupLayer
    {
        get { return _selectedLayer != null && _selectedLayer.Kind == LayerTreeItemKind.SupportGroup; }
    }

    /// <summary>
    /// Gets whether the current selection resolves to one model that owns at least one support base.
    /// </summary>
    public bool CanGenerateRaft
    {
        get { return _raftTargetModelEntityId.HasValue && ModelHasSupportBases(_raftTargetModelEntityId.Value); }
    }

    /// <summary>
    /// Gets whether multiple mesh models are currently selected in the scene.
    /// </summary>
    public bool HasMultipleSelectedModels
    {
        get { return _selectedModelCount > 1; }
    }

    /// <summary>
    /// Gets whether multiple support layers are currently selected through viewport support selection.
    /// </summary>
    public bool HasMultipleSelectedSupportLayerGroups
    {
        get { return GetEffectiveSelectedSupportLayerGroupCount() > 1; }
    }

    /// <summary>
    /// Gets whether exactly one support layer is selected and Edit Supports tools can run.
    /// </summary>
    public bool CanUseSupportEditingTools
    {
        get { return GetEffectiveSelectedSupportLayerGroupCount() == 1; }
    }

    /// <summary>
    /// Gets whether the workflow tabs should be visible for the current selection context.
    /// </summary>
    public bool CanShowWorkflowTabs
    {
        get
        {
            return HasSelectedModelLayer
                || HasSelectedSupportGroupLayer
                || HasMultipleSelectedModels
                || GetEffectiveSelectedSupportLayerGroupCount() > 0;
        }
    }

    /// <summary>
    /// Gets whether the add model button can start the model import workflow.
    /// </summary>
    public bool CanAddModel
    {
        get { return true; }
    }

    /// <summary>
    /// Gets whether the remove button can delete the selected model, support layer, or support modifier.
    /// </summary>
    public bool CanRemoveSelectedLayer
    {
        get
        {
            return _selectedLayer != null
                && (_selectedLayer.Kind == LayerTreeItemKind.Model
                    || _selectedLayer.Kind == LayerTreeItemKind.SupportGroup
                    || _selectedLayer.Kind == LayerTreeItemKind.SupportModifier);
        }
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
                OnPropertyChanged(nameof(CanRemoveSelectedLayer));
                OnPropertyChanged(nameof(HasSelectedModelLayer));
                OnPropertyChanged(nameof(HasSelectedSupportGroupLayer));
                OnPropertyChanged(nameof(HasMultipleSelectedSupportLayerGroups));
                OnPropertyChanged(nameof(CanUseSupportEditingTools));
                OnPropertyChanged(nameof(CanShowWorkflowTabs));
                UpdateLayerSelectionFlags();
            }
        }
    }

    /// <summary>
    /// Updates the count of mesh models selected in the scene so shell panels can respond to multi-model workflows.
    /// </summary>
    public void SetSelectedModelCount(int selectedModelCount)
    {
        if (selectedModelCount < 0)
        {
            selectedModelCount = 0;
        }

        if (_selectedModelCount == selectedModelCount)
        {
            return;
        }

        _selectedModelCount = selectedModelCount;
        OnPropertyChanged(nameof(HasMultipleSelectedModels));
        OnPropertyChanged(nameof(CanShowWorkflowTabs));
    }

    /// <summary>
    /// Updates the number of distinct support layer groups selected in the viewport.
    /// </summary>
    public void SetSelectedSupportLayerGroupCount(int selectedSupportLayerGroupCount)
    {
        if (selectedSupportLayerGroupCount < 0)
        {
            selectedSupportLayerGroupCount = 0;
        }

        if (_selectedSupportLayerGroupCount == selectedSupportLayerGroupCount)
        {
            return;
        }

        _selectedSupportLayerGroupCount = selectedSupportLayerGroupCount;
        OnPropertyChanged(nameof(HasMultipleSelectedSupportLayerGroups));
        OnPropertyChanged(nameof(CanUseSupportEditingTools));
        OnPropertyChanged(nameof(CanShowWorkflowTabs));
    }

    /// <summary>
    /// Updates the single model resolved from the current viewer or layer selection.
    /// </summary>
    public void SetRaftTargetModelEntityId(Guid? modelEntityId)
    {
        if (_raftTargetModelEntityId == modelEntityId)
        {
            return;
        }

        _raftTargetModelEntityId = modelEntityId;
        OnPropertyChanged(nameof(CanGenerateRaft));
    }

    /// <summary>
    /// Gets the model currently eligible for raft generation.
    /// </summary>
    public Guid? GetRaftTargetModelEntityId()
    {
        return _raftTargetModelEntityId;
    }

    /// <summary>
    /// Rebuilds the layer tree from the current document entities and support groups.
    /// </summary>
    public void RefreshFromDocument()
    {
        Guid? selectedId = SelectedLayer?.Id;
        LayerTreeItemKind? selectedKind = SelectedLayer?.Kind;
        Guid? selectedModelEntityId = SelectedLayer?.ModelEntityId;
        Guid? selectedSupportLayerGroupId = SelectedLayer?.SupportLayerGroupId;

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
                modelRow.IsVisible = GetModelLayerVisibility(entity.Id);

                modelRowsById.Add(entity.Id, modelRow);
                ModelLayers.Add(modelRow);
            }
        }

        if (_raftTargetModelEntityId.HasValue && !modelRowsById.ContainsKey(_raftTargetModelEntityId.Value))
        {
            SetRaftTargetModelEntityId(null);
        }
        foreach (CadEntity entity in _document.Entities)
        {
            if (entity is RaftEntity raft
                && modelRowsById.TryGetValue(raft.ModelEntityId, out LayerTreeItemViewModel? modelRow))
            {
                LayerTreeItemViewModel raftRow = new LayerTreeItemViewModel(
                    raft.Id,
                    raft.ModelEntityId,
                    LayerTreeItemKind.Raft,
                    raft.Settings.GetDisplayName(),
                    raft.Color);
                raftRow.IsVisible = GetRaftLayerVisibility(raft.Id);
                modelRow.Children.Add(raftRow);
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
                    supportLayerGroup.Color,
                    supportLayerGroup.Id);
                supportGroupRow.IsVisible = GetSupportLayerVisibility(supportLayerGroup.Id);

                IReadOnlyList<SupportModifierDefinition> supportModifiers = supportLayerGroup.SupportModifiers;

                HashSet<Guid> displayedToolSessionIds = new HashSet<Guid>();

                for (int i = 0; i < supportModifiers.Count; i++)
                {
                    SupportModifierDefinition modifier = supportModifiers[i];

                    if (!displayedToolSessionIds.Add(modifier.ToolSessionId))
                    {
                        continue;
                    }

                    supportGroupRow.Children.Add(new LayerTreeItemViewModel(
                        modifier.ToolSessionId,
                        supportLayerGroup.ModelEntityId,
                        LayerTreeItemKind.SupportModifier,
                        CreateToolSessionDisplayName(supportModifiers, modifier.ToolSessionId),
                        supportLayerGroup.Color,
                        supportLayerGroup.Id));
                }

                modelRow.Children.Add(supportGroupRow);
            }
        }

        PruneVisibilityState();

        LayerTreeItemViewModel? restoredSelection = FindLayer(selectedId, selectedKind);

        if (restoredSelection == null && selectedKind == LayerTreeItemKind.SupportModifier && selectedSupportLayerGroupId.HasValue)
        {
            restoredSelection = FindLayer(selectedSupportLayerGroupId.Value, LayerTreeItemKind.SupportGroup);
        }

        if (restoredSelection == null
            && selectedModelEntityId.HasValue
            && (selectedKind == LayerTreeItemKind.SupportGroup || selectedKind == LayerTreeItemKind.SupportModifier))
        {
            restoredSelection = FindLayer(selectedModelEntityId.Value, LayerTreeItemKind.Model);
        }

        SelectedLayer = restoredSelection ?? GetDefaultSelectedLayer();
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
    /// Selects one imported model row by entity id when the shell wants a deterministic active model.
    /// </summary>
    public void SelectModelLayer(Guid modelEntityId)
    {
        foreach (LayerTreeItemViewModel modelLayer in ModelLayers)
        {
            if (modelLayer.Kind == LayerTreeItemKind.Model && modelLayer.ModelEntityId == modelEntityId)
            {
                SelectedLayer = modelLayer;
                return;
            }
        }
    }

    /// <summary>
    /// Selects one generated raft row by entity id.
    /// </summary>
    public void SelectRaftLayer(Guid raftEntityId)
    {
        LayerTreeItemViewModel? raftLayer = FindLayer(raftEntityId, LayerTreeItemKind.Raft);
        if (raftLayer == null)
        {
            return;
        }

        SelectedLayer = raftLayer;

        foreach (LayerTreeItemViewModel modelLayer in ModelLayers)
        {
            if (modelLayer.ModelEntityId == raftLayer.ModelEntityId)
            {
                modelLayer.IsExpanded = true;
                break;
            }
        }
    }

    /// <summary>
    /// Selects one support group row by support-layer-group id.
    /// </summary>
    public void SelectSupportGroupLayer(Guid supportLayerGroupId)
    {
        foreach (LayerTreeItemViewModel modelLayer in ModelLayers)
        {
            foreach (LayerTreeItemViewModel childLayer in modelLayer.Children)
            {
                if (childLayer.Kind == LayerTreeItemKind.SupportGroup && childLayer.Id == supportLayerGroupId)
                {
                    SelectedLayer = childLayer;
                    modelLayer.IsExpanded = true;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Clears any current layer-tree selection.
    /// </summary>
    public void ClearSelectedLayer()
    {
        SelectedLayer = null;
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
    /// Updates one model layer's session visibility state and matching tree row.
    /// </summary>
    public void SetModelLayerVisibility(Guid modelEntityId, bool isVisible)
    {
        _modelLayerVisibilityById[modelEntityId] = isVisible;

        LayerTreeItemViewModel? layer = FindLayer(modelEntityId, LayerTreeItemKind.Model);

        if (layer != null)
        {
            layer.IsVisible = isVisible;
        }
    }

    /// <summary>
    /// Updates one support group layer's session visibility state and matching tree row.
    /// </summary>
    public void SetSupportLayerVisibility(Guid supportLayerGroupId, bool isVisible)
    {
        _supportLayerVisibilityById[supportLayerGroupId] = isVisible;

        LayerTreeItemViewModel? layer = FindLayer(supportLayerGroupId, LayerTreeItemKind.SupportGroup);

        if (layer != null)
        {
            layer.IsVisible = isVisible;
        }
    }

    /// <summary>
    /// Updates one raft layer's session visibility state.
    /// </summary>
    public void SetRaftLayerVisibility(Guid raftEntityId, bool isVisible)
    {
        _raftLayerVisibilityById[raftEntityId] = isVisible;
        LayerTreeItemViewModel? layer = FindLayer(raftEntityId, LayerTreeItemKind.Raft);
        if (layer != null) layer.IsVisible = isVisible;
    }

    /// <summary>
    /// Gets one raft layer's current session visibility state.
    /// </summary>
    public bool GetRaftLayerVisibility(Guid raftEntityId)
    {
        return !_raftLayerVisibilityById.TryGetValue(raftEntityId, out bool isVisible) || isVisible;
    }

    /// <summary>
    /// Gets one model layer's current session visibility state.
    /// </summary>
    public bool GetModelLayerVisibility(Guid modelEntityId)
    {
        if (_modelLayerVisibilityById.TryGetValue(modelEntityId, out bool isVisible))
        {
            return isVisible;
        }

        return true;
    }

    /// <summary>
    /// Gets one support group layer's current session visibility state.
    /// </summary>
    public bool GetSupportLayerVisibility(Guid supportLayerGroupId)
    {
        if (_supportLayerVisibilityById.TryGetValue(supportLayerGroupId, out bool isVisible))
        {
            return isVisible;
        }

        return true;
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
            LayerTreeItemViewModel? foundLayer = FindLayerRecursive(modelLayer, id.Value, kind.Value);

            if (foundLayer != null)
            {
                return foundLayer;
            }
        }

        return null;
    }

    /// <summary>
    /// Removes visibility entries for layers that are no longer present in the document.
    /// </summary>
    private void PruneVisibilityState()
    {
        HashSet<Guid> modelIds = new HashSet<Guid>();
        HashSet<Guid> supportLayerGroupIds = new HashSet<Guid>();
        HashSet<Guid> raftIds = new HashSet<Guid>();

        foreach (CadEntity entity in _document.Entities)
        {
            if (entity is MeshEntity)
            {
                modelIds.Add(entity.Id);
            }
            else if (entity is RaftEntity)
            {
                raftIds.Add(entity.Id);
            }
        }

        foreach (SupportLayerGroup supportLayerGroup in _document.SupportLayerGroups)
        {
            supportLayerGroupIds.Add(supportLayerGroup.Id);
        }

        RemoveMissingVisibilityEntries(_modelLayerVisibilityById, modelIds);
        RemoveMissingVisibilityEntries(_raftLayerVisibilityById, raftIds);
        RemoveMissingVisibilityEntries(_supportLayerVisibilityById, supportLayerGroupIds);
    }

    /// <summary>
    /// Removes stale visibility state from one visibility map.
    /// </summary>
    private static void RemoveMissingVisibilityEntries(Dictionary<Guid, bool> visibilityById, HashSet<Guid> existingIds)
    {
        List<Guid> removedIds = new List<Guid>();

        foreach (Guid id in visibilityById.Keys)
        {
            if (!existingIds.Contains(id))
            {
                removedIds.Add(id);
            }
        }

        for (int i = 0; i < removedIds.Count; i++)
        {
            visibilityById.Remove(removedIds[i]);
        }
    }

    /// <summary>
    /// Finds a layer row recursively in the layer tree.
    /// </summary>
    private static LayerTreeItemViewModel? FindLayerRecursive(LayerTreeItemViewModel layer, Guid id, LayerTreeItemKind kind)
    {
        if (layer.Id == id && layer.Kind == kind)
        {
            return layer;
        }

        foreach (LayerTreeItemViewModel childLayer in layer.Children)
        {
            LayerTreeItemViewModel? foundLayer = FindLayerRecursive(childLayer, id, kind);

            if (foundLayer != null)
            {
                return foundLayer;
            }
        }

        return null;
    }

    /// <summary>
    /// Chooses the default tree selection when the previous selection no longer exists.
    /// </summary>
    private LayerTreeItemViewModel? GetDefaultSelectedLayer()
    {
        if (ModelLayers.Count == 0)
        {
            return null;
        }

        return ModelLayers[0];
    }

    /// <summary>
    /// Mirrors the selected layer into the tree row selection flags used by WPF.
    /// </summary>
    private void UpdateLayerSelectionFlags()
    {
        foreach (LayerTreeItemViewModel modelLayer in ModelLayers)
        {
            UpdateLayerSelectionFlagsRecursive(modelLayer);
        }
    }

    /// <summary>
    /// Mirrors selection state recursively into each tree row.
    /// </summary>
    private void UpdateLayerSelectionFlagsRecursive(LayerTreeItemViewModel layer)
    {
        layer.IsSelected = ReferenceEquals(layer, SelectedLayer);

        foreach (LayerTreeItemViewModel childLayer in layer.Children)
        {
            UpdateLayerSelectionFlagsRecursive(childLayer);
        }
    }

    /// <summary>
    /// Refreshes the tree when document entities are added or removed.
    /// </summary>
    private void OnDocumentStructureChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RefreshEntitySubscriptions();
        RefreshFromDocument();
        OnPropertyChanged(nameof(CanGenerateRaft));
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
        OnPropertyChanged(nameof(CanGenerateRaft));
    }

    /// <summary>
    /// Checks the document-owned support index without allocating support collections.
    /// </summary>
    private bool ModelHasSupportBases(Guid modelEntityId)
    {
        foreach (SupportLayerGroup supportLayerGroup in _document.SupportLayerGroups)
        {
            if (supportLayerGroup.ModelEntityId == modelEntityId
                && _document.HasSupportEntitiesForGroup(supportLayerGroup.Id))
            {
                return true;
            }
        }

        return false;
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
    /// Refreshes the raft row when its durable display color changes.
    /// </summary>
    private void Entity_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is RaftEntity
            && string.Equals(e.PropertyName, nameof(RaftEntity.Color), StringComparison.Ordinal))
        {
            RefreshFromDocument();
        }
    }

    /// <summary>
    /// Rebuilds entity subscriptions after document structure changes, including collection resets.
    /// </summary>
    private void RefreshEntitySubscriptions()
    {
        foreach (CadEntity entity in _subscribedEntities)
        {
            entity.PropertyChanged -= Entity_PropertyChanged;
        }

        _subscribedEntities.Clear();

        foreach (CadEntity entity in _document.Entities)
        {
            entity.PropertyChanged += Entity_PropertyChanged;
            _subscribedEntities.Add(entity);
        }
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

    /// <summary>
    /// Builds one Layer Panel label for all internal actions captured by a tool-launch session.
    /// </summary>
    private static string CreateToolSessionDisplayName(
        IReadOnlyList<SupportModifierDefinition> modifiers,
        Guid toolSessionId)
    {
        HashSet<Guid> targetIds = new HashSet<Guid>();
        bool hasCluster = false;
        bool hasBracing = false;
        bool hasDelete = false;
        bool hasDirectEdit = false;

        for (int i = 0; i < modifiers.Count; i++)
        {
            SupportModifierDefinition modifier = modifiers[i];

            if (modifier.ToolSessionId != toolSessionId)
            {
                continue;
            }

            hasCluster |= modifier.Kind == SupportModifierKind.Cluster;
            hasBracing |= modifier.Kind == SupportModifierKind.Brace || modifier.Kind == SupportModifierKind.Buttress;
            hasDelete |= modifier.Kind == SupportModifierKind.Delete;
            hasDirectEdit |= modifier.Kind == SupportModifierKind.DirectEdit;

            for (int targetIndex = 0; targetIndex < modifier.TargetSupportIds.Count; targetIndex++)
            {
                targetIds.Add(modifier.TargetSupportIds[targetIndex]);
            }
        }

        string name = hasBracing
            ? "Brace"
            : hasCluster
                ? "Cluster"
                : hasDirectEdit
                    ? "Direct Edit"
                    : hasDelete
                    ? "Delete"
                    : "Support Edit";
        return $"{name} ({targetIds.Count})";
    }

    /// <summary>
    /// Gets the effective support-layer selection count from either the layer tree or viewport support selection.
    /// </summary>
    private int GetEffectiveSelectedSupportLayerGroupCount()
    {
        if (_selectedLayer != null && _selectedLayer.Kind == LayerTreeItemKind.SupportGroup)
        {
            return 1;
        }

        return _selectedSupportLayerGroupCount;
    }
}
