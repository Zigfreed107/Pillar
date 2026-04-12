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
    /* PSEUDOCODE / PLAN
     - Problem: referencing the generated property `StatusText` can produce CS0103
       if source generation hasn't produced the property at the time of compilation or
       if the code path doesn't see the generated member.
     - Fix: update the backing field `_statusText` using ObservableObject's SetProperty
       which sets the field and raises PropertyChanged.
     - Implementation:
       public void SetStatusText(string statusText)
       {
           // Use the existing private field declared with [ObservableProperty]
           // and the base class helper to update and notify.
           SetProperty(ref _statusText, statusText);
       }
     - This avoids directly referencing the generated property name while preserving
       notification behavior and keeping the existing ObservableProperty attribute.
    */

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
    /// Uses SetProperty to avoid referencing the generated property directly.
    /// </summary>
    public void SetStatusText(string statusText)
    {
        SetProperty(ref _statusText, statusText);
    }
}
