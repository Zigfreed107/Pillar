// MainViewModel.cs
// Owns the WPF shell state for the main CAD workspace without taking dependencies on rendering services.
using CadApp.Core.Entities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CadApp.ViewModels;

/// <summary>
/// Provides UI state for the main application shell.
/// This view model keeps layout state and status text out of the rendering layer.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private bool _isEntitySelected;
    private string _selectedEntityType = "No selection";
    private string _selectedEntityName = string.Empty;

    [ObservableProperty]
    private string _windowTitle = "CadApp";

    [ObservableProperty]
    private string _viewerTitle = "Viewer";

    [ObservableProperty]
    private string _toolPanelTitle = "Tools";

    [ObservableProperty]
    private string _propertiesPanelTitle = "Properties";

    [ObservableProperty]
    private string _toolPanelText = "Tool Panel";

    [ObservableProperty]
    private string _propertiesPanelText = "Properties Panel";

    [ObservableProperty]
    private string _statusText = "Ready";

    /// <summary>
    /// Indicates whether the properties panel has an entity that can be edited.
    /// </summary>
    public bool IsEntitySelected
    {
        get { return _isEntitySelected; }
        private set { SetProperty(ref _isEntitySelected, value); }
    }

    /// <summary>
    /// Displays the selected entity kind in the properties panel.
    /// </summary>
    public string SelectedEntityType
    {
        get { return _selectedEntityType; }
        private set { SetProperty(ref _selectedEntityType, value); }
    }

    /// <summary>
    /// Gets or sets the selected entity name shown in the properties panel.
    /// </summary>
    public string SelectedEntityName
    {
        get { return _selectedEntityName; }
        set { SetProperty(ref _selectedEntityName, value); }
    }

    /// <summary>
    /// Updates the short status message displayed in the status bar.
    /// </summary>
    public void SetStatusText(string statusText)
    {
        StatusText = statusText;
    }

    /// <summary>
    /// Updates the tool panel guidance shown above the active tool buttons.
    /// </summary>
    public void SetToolPanelText(string toolPanelText)
    {
        ToolPanelText = toolPanelText;
    }

    /// <summary>
    /// Sets the entity currently displayed by the properties panel.
    /// </summary>
    public void SetSelectedEntity(CadEntity? selectedEntity)
    {
        IsEntitySelected = selectedEntity != null;
        SelectedEntityType = selectedEntity == null ? "No selection" : selectedEntity.GetType().Name;
        SelectedEntityName = selectedEntity == null ? string.Empty : selectedEntity.Name;
        PropertiesPanelText = selectedEntity == null ? "Select an entity to edit its properties." : "Entity properties";
    }

    /// <summary>
    /// Sets the properties panel to a read-only summary for multi-selection.
    /// </summary>
    public void SetMultipleSelection(int selectedEntityCount)
    {
        IsEntitySelected = false;
        SelectedEntityType = $"{selectedEntityCount} entities selected";
        SelectedEntityName = string.Empty;
        PropertiesPanelText = "Multiple selection. Select one entity to edit its properties.";
    }
}
