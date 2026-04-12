// MainViewModel.cs
// Owns the WPF shell state for the main CAD workspace without taking dependencies on rendering services.
using CommunityToolkit.Mvvm.ComponentModel;

namespace CadApp.ViewModels;

/// <summary>
/// Provides UI state for the main application shell.
/// This view model keeps layout state and status text out of the rendering layer.
/// </summary>
public partial class MainViewModel : ObservableObject
{
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
}
