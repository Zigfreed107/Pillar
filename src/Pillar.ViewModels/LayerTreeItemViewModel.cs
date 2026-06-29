// LayerTreeItemViewModel.cs
// Represents one row in the Layer Panel tree while keeping WPF-specific tree state out of the document model.
using CommunityToolkit.Mvvm.ComponentModel;
using Pillar.Core.Layers;
using System;
using System.Collections.ObjectModel;

namespace Pillar.ViewModels;

/// <summary>
/// Provides bindable state for one model or support group layer row.
/// </summary>
public partial class LayerTreeItemViewModel : ObservableObject
{
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isEditing;
    private bool _isVisible;
    private string _editingName;
    private SupportLayerColor _supportColor;

    /// <summary>
    /// Creates one layer tree row.
    /// </summary>
    public LayerTreeItemViewModel(Guid id, Guid modelEntityId, LayerTreeItemKind kind, string name, SupportLayerColor supportColor, Guid? supportLayerGroupId = null)
    {
        Id = id;
        ModelEntityId = modelEntityId;
        Kind = kind;
        SupportLayerGroupId = supportLayerGroupId ?? (kind == LayerTreeItemKind.SupportGroup ? id : Guid.Empty);
        Name = name;
        _supportColor = supportColor;
        _editingName = name;
        _isExpanded = kind == LayerTreeItemKind.Model;
        _isVisible = true;
    }

    /// <summary>
    /// Gets the document identifier represented by this row.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the imported model entity id that owns this row.
    /// </summary>
    public Guid ModelEntityId { get; }

    /// <summary>
    /// Gets the owning support group id when this row represents support data.
    /// </summary>
    public Guid SupportLayerGroupId { get; }

    /// <summary>
    /// Gets whether this row represents an imported model, support group, or support modifier.
    /// </summary>
    public LayerTreeItemKind Kind { get; }

    /// <summary>
    /// Gets the child rows shown beneath this layer.
    /// </summary>
    public ObservableCollection<LayerTreeItemViewModel> Children { get; } = new ObservableCollection<LayerTreeItemViewModel>();

    /// <summary>
    /// Gets the user-visible layer name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets whether this row can be renamed from the Layer Panel.
    /// </summary>
    public bool CanRename
    {
        get { return Kind == LayerTreeItemKind.Model || Kind == LayerTreeItemKind.SupportGroup; }
    }

    /// <summary>
    /// Gets whether this row can export an imported model and its owned support groups.
    /// </summary>
    public bool CanExportModel
    {
        get { return Kind == LayerTreeItemKind.Model; }
    }

    /// <summary>
    /// Gets whether this row can export only the supports in one support group.
    /// </summary>
    public bool CanExportSupportGroup
    {
        get { return Kind == LayerTreeItemKind.SupportGroup; }
    }

    /// <summary>
    /// Gets whether this row should show the support-group color button.
    /// </summary>
    public bool CanPickColor
    {
        get { return Kind == LayerTreeItemKind.SupportGroup; }
    }

    /// <summary>
    /// Gets whether this row should show a support-group tool edit button.
    /// </summary>
    public bool CanEditSupportGroup
    {
        get { return Kind == LayerTreeItemKind.SupportGroup; }
    }

    /// <summary>
    /// Gets whether this row should show a support modifier edit button.
    /// </summary>
    public bool CanEditSupportModifier
    {
        get { return Kind == LayerTreeItemKind.SupportModifier; }
    }

    /// <summary>
    /// Gets whether this row should show any tool-edit button.
    /// </summary>
    public bool CanEditLayerTool
    {
        get { return CanEditSupportGroup || CanEditSupportModifier; }
    }

    /// <summary>
    /// Gets whether this row should show a layer visibility toggle.
    /// </summary>
    public bool CanToggleVisibility
    {
        get { return Kind == LayerTreeItemKind.Model || Kind == LayerTreeItemKind.SupportGroup; }
    }

    /// <summary>
    /// Gets or sets whether this layer row is currently visible in the viewport.
    /// </summary>
    public bool IsVisible
    {
        get { return _isVisible; }
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                OnPropertyChanged(nameof(IsVisibilityToggleChecked));
                OnPropertyChanged(nameof(VisibilityToggleLabel));
            }
        }
    }

    /// <summary>
    /// Gets whether the visibility toggle should appear pressed for a hidden layer.
    /// </summary>
    public bool IsVisibilityToggleChecked
    {
        get { return !IsVisible; }
    }

    /// <summary>
    /// Gets the compact visibility label required by the Layer Panel.
    /// </summary>
    public string VisibilityToggleLabel
    {
        get { return IsVisible ? "V" : "H"; }
    }

    /// <summary>
    /// Gets or sets whether WPF should keep this tree row expanded.
    /// </summary>
    public bool IsExpanded
    {
        get { return _isExpanded; }
        set { SetProperty(ref _isExpanded, value); }
    }

    /// <summary>
    /// Gets or sets whether WPF should keep this tree row selected.
    /// </summary>
    public bool IsSelected
    {
        get { return _isSelected; }
        set { SetProperty(ref _isSelected, value); }
    }

    /// <summary>
    /// Gets or sets whether this row is currently using inline rename editing.
    /// </summary>
    public bool IsEditing
    {
        get { return _isEditing; }
        set { SetProperty(ref _isEditing, value); }
    }

    /// <summary>
    /// Gets or sets the draft name used by inline rename editing.
    /// </summary>
    public string EditingName
    {
        get { return _editingName; }
        set { SetProperty(ref _editingName, value); }
    }

    /// <summary>
    /// Gets or sets the support-group display color shown by the Layer Panel.
    /// </summary>
    public SupportLayerColor SupportColor
    {
        get { return _supportColor; }
        set { SetProperty(ref _supportColor, value); }
    }

    /// <summary>
    /// Starts inline editing with the current committed name.
    /// </summary>
    public void BeginRename()
    {
        if (!CanRename)
        {
            return;
        }

        EditingName = Name;
        IsEditing = true;
    }

    /// <summary>
    /// Cancels inline editing without changing document data.
    /// </summary>
    public void CancelRename()
    {
        EditingName = Name;
        IsEditing = false;
    }
}
