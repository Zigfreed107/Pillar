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
    private string _editingName;
    private SupportLayerColor _supportColor;

    /// <summary>
    /// Creates one layer tree row.
    /// </summary>
    public LayerTreeItemViewModel(Guid id, Guid modelEntityId, LayerTreeItemKind kind, string name, SupportLayerColor supportColor)
    {
        Id = id;
        ModelEntityId = modelEntityId;
        Kind = kind;
        Name = name;
        _supportColor = supportColor;
        _editingName = name;
        _isExpanded = kind == LayerTreeItemKind.Model;
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
    /// Gets whether this row represents an imported model or a support group.
    /// </summary>
    public LayerTreeItemKind Kind { get; }

    /// <summary>
    /// Gets the child support groups shown under model rows.
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
