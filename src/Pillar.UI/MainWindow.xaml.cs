// MainWindow.xaml.cs
// Composes the WPF workspace shell with CAD document, scene, interaction, and lightweight dock-region behavior.
using Pillar.Core.Document;
using Pillar.Commands;
using Pillar.Core.Entities;
using Pillar.Core.Import;
using Pillar.Core.Layers;
using Pillar.Core.Persistence;
using Pillar.Core.Snapping;
using Pillar.Core.Tools;
using Pillar.Rendering.Math;
using Pillar.Rendering.Scene;
using Pillar.Rendering.Tools;
using Pillar.UI.Layers;
using Pillar.UI.Modes;
using Pillar.UI.Services;
using Pillar.ViewModels;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using SelectionWindowOverlayController = Pillar.UI.Overlays.SelectionWindowOverlay;

namespace Pillar.UI;

public partial class MainWindow : Window
{
    private readonly CadDocument _document;
    private readonly SceneManager _scene;
    private readonly MainViewModel _viewModel;
    private readonly ProjectionService _projection;
    private readonly ToolManager _toolManager;
    private readonly SelectTool _selectTool;
    private readonly LineTool _lineTool;
    private readonly ManualSupportTool _manualSupportTool;
    private readonly IModelImporter _stlImporter;
    private readonly SnapManager _snapManager;
    private readonly SelectionWindowOverlayController _selectionWindowOverlay;
    private readonly DocumentFileService _documentFileService;
    private readonly CadCommandRunner _commandRunner;
    private readonly LayerPanelViewModel _layerPanelViewModel;
    private readonly Dictionary<WorkspaceModeId, WorkspaceModeDefinition> _modeDefinitions = new Dictionary<WorkspaceModeId, WorkspaceModeDefinition>();
    private WorkspaceModeId _activeModeId = WorkspaceModeId.Select;
    private string _activeToolStatusText = "Select tool active";

    public DefaultEffectsManager EffectsManager { get; }

    /// <summary>
    /// Creates the main application window and composes the current CAD services.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        _selectionWindowOverlay = new SelectionWindowOverlayController(this, SelectionWindowOverlay);

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        EffectsManager = new DefaultEffectsManager();
        Viewport.EffectsManager = EffectsManager; // Needed since EffectsManager is initialised AFTER Main window is initialised. Data binding needs to be updated.

        _document = new CadDocument();
        _scene = new SceneManager(Viewport, _document);
        _snapManager = new SnapManager(_document.SpatialGrid);
        _projection = new ProjectionService(Viewport);
        _toolManager = new ToolManager();
        _commandRunner = new CadCommandRunner(Properties.Settings.Default.UndoHistoryLimit);
        _selectTool = new SelectTool(Viewport, _document, _scene, _scene.SelectionManager);
        _lineTool = new LineTool(_document, _projection, _scene, _snapManager, _commandRunner);
        _layerPanelViewModel = new LayerPanelViewModel(_document);
        _manualSupportTool = new ManualSupportTool(
            _document,
            _projection,
            _scene,
            _commandRunner,
            _layerPanelViewModel.GetSelectedSupportLayerGroupId);
        _stlImporter = new StlImporter();
        WireLayerPanel();
        _documentFileService = new DocumentFileService(
            this,
            _document,
            _scene.SelectionManager,
            new GphDocumentSerializer(),
            CancelTransientToolState,
            _commandRunner.ClearHistory,
            ActivateSelectToolForDocumentCommand);

        WireWorkspaceState();
        RegisterWorkspaceModes();
        _selectTool.SelectionWindowChanged += _selectionWindowOverlay.Update;
        _manualSupportTool.StatusMessageRequested += ManualSupportTool_StatusMessageRequested;
        SetActiveMode(WorkspaceModeId.Select);
    }

    /// <summary>
    /// Connects domain events to shell UI state so workspace feedback stays outside the renderer.
    /// </summary>
    private void WireWorkspaceState()
    {
        _scene.SelectionManager.SelectionChanged += OnSelectionChanged;
        _commandRunner.HistoryChanged += UpdateUndoRedoButtonState;
        _viewModel.SetStatusText("Ready");
        _viewModel.SetSelectedEntity(null);
        UpdateUndoRedoButtonState();
    }

    /// <summary>
    /// Connects Layer Panel UI requests to undoable document commands.
    /// </summary>
    private void WireLayerPanel()
    {
        LayerPanelOverlay.DataContext = _layerPanelViewModel;
        LayerPanelOverlay.ImportModelRequested += LayerPanel_ImportModelRequested;
        LayerPanelOverlay.RemoveModelRequested += LayerPanel_RemoveModelRequested;
        LayerPanelOverlay.AddSupportGroupRequested += LayerPanel_AddSupportGroupRequested;
        LayerPanelOverlay.RemoveSupportGroupRequested += LayerPanel_RemoveSupportGroupRequested;
        LayerPanelOverlay.RenameSupportGroupRequested += LayerPanel_RenameSupportGroupRequested;
    }

    /// <summary>
    /// Creates a new blank document after optionally saving the current document.
    /// </summary>
    private void NewProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ApplyDocumentFileResult(_documentFileService.New());
    }

    /// <summary>
    /// Opens a saved Graphite project file and replaces the current document after confirmation.
    /// </summary>
    private void OpenProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ApplyDocumentFileResult(_documentFileService.Open());
    }

    /// <summary>
    /// Saves the current document to a Graphite project file selected by the user.
    /// </summary>
    private void SaveProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ApplyDocumentFileResult(_documentFileService.Save());
    }

    /// <summary>
    /// Handles viewport clicks by routing them to the active interaction logic and hit-test based selection.
    /// </summary>
    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _toolManager.ActiveTool?.OnMouseDown(GetScreenPosition(e));
        Viewport.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>
    /// Handles viewport mouse movement so interactive tools can update previews efficiently.
    /// </summary>
    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        _toolManager.ActiveTool?.OnMouseMove(GetScreenPosition(e));

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Routes mouse-up events to the active interaction tool.
    /// </summary>
    private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _toolManager.ActiveTool?.OnMouseUp(GetScreenPosition(e));

        if (Viewport.IsMouseCaptured)
        {
            Viewport.ReleaseMouseCapture();
        }

        e.Handled = true;
    }

    /// <summary>
    /// Activates selection mode from the mode toolbar.
    /// </summary>
    private void SelectMode_Click(object sender, RoutedEventArgs e)
    {
        SetActiveMode(WorkspaceModeId.Select);
    }

    /// <summary>
    /// Activates line drawing mode from the mode toolbar.
    /// </summary>
    private void LineMode_Click(object sender, RoutedEventArgs e)
    {
        SetActiveMode(WorkspaceModeId.Line);
    }

    /// <summary>
    /// Rechecks the toolbar when the planned transform mode is clicked programmatically.
    /// </summary>
    private void TransformMode_Click(object sender, RoutedEventArgs e)
    {
        SetActiveMode(WorkspaceModeId.Transform);
    }

    /// <summary>
    /// Rechecks the toolbar when the planned support mode is clicked programmatically.
    /// </summary>
    private void SupportMode_Click(object sender, RoutedEventArgs e)
    {
        SetActiveMode(WorkspaceModeId.ManualSupport);
    }

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

            SupportLayerGroup initialSupportLayerGroup = new SupportLayerGroup(importedMesh.Id, "Supports Group 1");
            _commandRunner.Execute(new ImportMeshWithSupportGroupCommand(_document, importedMesh, initialSupportLayerGroup));

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
    /// Handles workspace keyboard shortcuts that cancel transient tool state.
    /// </summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (IsControlShortcut(e, Key.Z))
        {
            if (IsKeyboardFocusInsideEditableControl())
            {
                return;
            }

            UndoLastCommand();
            e.Handled = true;
            return;
        }

        if (IsControlShortcut(e, Key.Y))
        {
            if (IsKeyboardFocusInsideEditableControl())
            {
                return;
            }

            RedoLastCommand();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape)
        {
            return;
        }

        _toolManager.CancelActiveTool();
        SetActiveMode(WorkspaceModeId.Select);
        e.Handled = true;
    }

    /// <summary>
    /// Undoes the most recent document command from the toolbar.
    /// </summary>
    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        UndoLastCommand();
    }

    /// <summary>
    /// Redoes the most recent undone document command from the toolbar.
    /// </summary>
    private void RedoButton_Click(object sender, RoutedEventArgs e)
    {
        RedoLastCommand();
    }

    /// <summary>
    /// Commits a completed entity-name edit when the properties textbox loses focus.
    /// </summary>
    private void SelectedEntityNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitSelectedEntityNameEdit();
    }

    /// <summary>
    /// Commits a completed entity-name edit when the user presses Enter.
    /// </summary>
    private void SelectedEntityNameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        CommitSelectedEntityNameEdit();
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    /// <summary>
    /// Registers the available and planned workspace modes used by the toolbar and overlay host.
    /// </summary>
    private void RegisterWorkspaceModes()
    {
        _modeDefinitions.Add(
            WorkspaceModeId.Select,
            new WorkspaceModeDefinition(
                WorkspaceModeId.Select,
                "Select",
                "Select tool active",
                true,
                _selectTool,
                () => new SelectModeOverlay()));

        _modeDefinitions.Add(
            WorkspaceModeId.Line,
            new WorkspaceModeDefinition(
                WorkspaceModeId.Line,
                "Line",
                "Line tool active: click two points",
                true,
                _lineTool,
                () => new LineModeOverlay()));

        _modeDefinitions.Add(
            WorkspaceModeId.Transform,
            new WorkspaceModeDefinition(
                WorkspaceModeId.Transform,
                "Transform",
                "Transform mode is planned",
                false,
                null,
                () => new PlaceholderModeOverlay
                {
                    ModeName = "Transform",
                    Message = "Transform tools are planned but not available yet."
                }));

        _modeDefinitions.Add(
            WorkspaceModeId.ManualSupport,
            new WorkspaceModeDefinition(
                WorkspaceModeId.ManualSupport,
                "Manual Support",
                "Manual support mode: choose an operation",
                true,
                _manualSupportTool,
                CreateManualSupportModeOverlay));
    }

    /// <summary>
    /// Creates and wires the Manual Support overlay to the Manual Support tool operation state.
    /// </summary>
    private ManualSupportModeOverlay CreateManualSupportModeOverlay()
    {
        ManualSupportModeOverlay overlay = new ManualSupportModeOverlay();
        overlay.OperationChanged += ManualSupportModeOverlay_OperationChanged;

        return overlay;
    }

    /// <summary>
    /// Applies Manual Support overlay selections to the active Manual Support tool.
    /// </summary>
    private void ManualSupportModeOverlay_OperationChanged(object? sender, ManualSupportOperationChangedEventArgs e)
    {
        _ = sender;

        _manualSupportTool.SetActiveOperation(e.OperationKind);

        if (_activeModeId != WorkspaceModeId.ManualSupport)
        {
            return;
        }

        string statusText = GetManualSupportStatusText(e.OperationKind);
        _activeToolStatusText = statusText;
        _viewModel.SetStatusText(statusText);
        _viewModel.SetToolPanelText(statusText);
    }

    /// <summary>
    /// Applies support-operation status requests to the shell while Manual Support mode is active.
    /// </summary>
    private void ManualSupportTool_StatusMessageRequested(string statusMessage)
    {
        if (_activeModeId != WorkspaceModeId.ManualSupport)
        {
            return;
        }

        _viewModel.SetStatusText(statusMessage);
        _viewModel.SetToolPanelText(statusMessage);
    }

    /// <summary>
    /// Gets the status text that should be shown for a workspace mode activation.
    /// </summary>
    private string GetWorkspaceModeStatusText(WorkspaceModeDefinition mode)
    {
        if (mode.Id == WorkspaceModeId.ManualSupport)
        {
            return GetManualSupportStatusText(_manualSupportTool.ActiveOperationKind);
        }

        return mode.StatusText;
    }

    /// <summary>
    /// Converts a Manual Support operation selection into user-facing shell guidance.
    /// </summary>
    private static string GetManualSupportStatusText(ManualSupportOperationKind operationKind)
    {
        switch (operationKind)
        {
            case ManualSupportOperationKind.Point:
                return "Manual support mode: point support operation active";

            case ManualSupportOperationKind.Line:
                return "Manual support mode: line support operation active";

            case ManualSupportOperationKind.Circle:
                return "Manual support mode: circle support operation active";

            case ManualSupportOperationKind.None:
            default:
                return "Manual support mode: choose an operation";
        }
    }

    /// <summary>
    /// Switches the active workspace mode and updates tool, overlay, toolbar, and shell guidance state.
    /// </summary>
    private void SetActiveMode(WorkspaceModeId modeId)
    {
        WorkspaceModeDefinition mode = _modeDefinitions[modeId];

        if (!mode.IsAvailable || mode.Tool == null)
        {
            UpdateModeToolbarState(_activeModeId);
            _viewModel.SetStatusText($"{mode.DisplayName} mode is not available yet");
            return;
        }

        _activeModeId = modeId;
        string statusText = GetWorkspaceModeStatusText(mode);
        _activeToolStatusText = statusText;
        _toolManager.SetTool(mode.Tool);
        ModePanelHost.Content = mode.GetOverlay();
        UpdateModeToolbarState(modeId);
        _viewModel.SetStatusText(statusText);
        _viewModel.SetToolPanelText(statusText);
    }

    /// <summary>
    /// Keeps the mode toolbar as a visual reflection of the active workspace mode.
    /// </summary>
    private void UpdateModeToolbarState(WorkspaceModeId activeModeId)
    {
        SelectMode.IsEnabled = _modeDefinitions[WorkspaceModeId.Select].IsAvailable;
        LineMode.IsEnabled = _modeDefinitions[WorkspaceModeId.Line].IsAvailable;
        TransformMode.IsEnabled = _modeDefinitions[WorkspaceModeId.Transform].IsAvailable;
        SupportMode.IsEnabled = _modeDefinitions[WorkspaceModeId.ManualSupport].IsAvailable;

        SelectMode.IsChecked = activeModeId == WorkspaceModeId.Select;
        LineMode.IsChecked = activeModeId == WorkspaceModeId.Line;
        TransformMode.IsChecked = activeModeId == WorkspaceModeId.Transform;
        SupportMode.IsChecked = activeModeId == WorkspaceModeId.ManualSupport;
    }

    /// <summary>
    /// Converts a WPF mouse event into the float screen coordinate format used by CAD tools.
    /// </summary>
    private Vector2 GetScreenPosition(MouseEventArgs e)
    {
        Point mousePosition = e.GetPosition(Viewport);
        return new Vector2((float)mousePosition.X, (float)mousePosition.Y);
    }

    /// <summary>
    /// Updates shell state when the domain selection changes.
    /// </summary>
    private void OnSelectionChanged(IEnumerable<Guid> addedIds, IEnumerable<Guid> removedIds)
    {
        _ = addedIds;
        _ = removedIds;

        if (_scene.SelectionManager.SelectedCount == 1)
        {
            Guid? selectedId = GetSingleSelectedEntityId();

            if (selectedId.HasValue)
            {
                _viewModel.SetSelectedEntity(FindEntityById(selectedId.Value));
            }

            _viewModel.SetStatusText("Object selected");
            return;
        }

        if (_scene.SelectionManager.SelectedCount > 1)
        {
            _viewModel.SetMultipleSelection(_scene.SelectionManager.SelectedCount);
            _viewModel.SetStatusText($"{_scene.SelectionManager.SelectedCount} objects selected");
            return;
        }

        _viewModel.SetSelectedEntity(null);
        _viewModel.SetStatusText(_activeToolStatusText);
    }

    /// <summary>
    /// Finds a document entity by id for shell-level selection display.
    /// </summary>
    private CadEntity? FindEntityById(Guid id)
    {
        foreach (CadEntity entity in _document.Entities)
        {
            if (entity.Id == id)
            {
                return entity;
            }
        }

        return null;
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
    /// Gets the only selected id when selection contains exactly one entity.
    /// </summary>
    private Guid? GetSingleSelectedEntityId()
    {
        foreach (Guid selectedId in _scene.SelectionManager.SelectedEntityIds)
        {
            return selectedId;
        }

        return null;
    }

    /// <summary>
    /// Executes undo and writes the applied command name to the status bar.
    /// </summary>
    private void UndoLastCommand()
    {
        ICadCommand? command = _commandRunner.Undo();

        if (command == null)
        {
            return;
        }

        RefreshPropertiesPanelFromSelection();
        _layerPanelViewModel.RefreshFromDocument();
        _viewModel.SetStatusText($"Undid {command.DisplayName}");
    }

    /// <summary>
    /// Executes redo and writes the applied command name to the status bar.
    /// </summary>
    private void RedoLastCommand()
    {
        ICadCommand? command = _commandRunner.Redo();

        if (command == null)
        {
            return;
        }

        RefreshPropertiesPanelFromSelection();
        _layerPanelViewModel.RefreshFromDocument();
        _viewModel.SetStatusText($"Redid {command.DisplayName}");
    }

    /// <summary>
    /// Updates the toolbar buttons from the central command history state.
    /// </summary>
    private void UpdateUndoRedoButtonState()
    {
        UndoButton.IsEnabled = _commandRunner.CanUndo;
        RedoButton.IsEnabled = _commandRunner.CanRedo;
    }

    /// <summary>
    /// Commits the selected entity name as one undoable edit instead of per keystroke.
    /// </summary>
    private void CommitSelectedEntityNameEdit()
    {
        if (_scene.SelectionManager.SelectedCount != 1)
        {
            return;
        }

        Guid? selectedId = GetSingleSelectedEntityId();

        if (!selectedId.HasValue)
        {
            return;
        }

        CadEntity? selectedEntity = FindEntityById(selectedId.Value);

        if (selectedEntity == null)
        {
            RefreshPropertiesPanelFromSelection();
            return;
        }

        string oldName = NormalizeEntityName(selectedEntity.Name);
        string newName = NormalizeEntityName(_viewModel.SelectedEntityName);

        if (string.Equals(oldName, newName, StringComparison.Ordinal))
        {
            _viewModel.SetSelectedEntity(selectedEntity);
            return;
        }

        _commandRunner.Execute(new RenameEntityCommand(selectedEntity, oldName, newName));
        _viewModel.SetSelectedEntity(selectedEntity);
        _layerPanelViewModel.RefreshFromDocument();
        _viewModel.SetStatusText("Renamed entity");
    }

    /// <summary>
    /// Refreshes the properties panel after undo or redo without changing the command status text.
    /// </summary>
    private void RefreshPropertiesPanelFromSelection()
    {
        if (_scene.SelectionManager.SelectedCount == 1)
        {
            Guid? selectedId = GetSingleSelectedEntityId();

            if (selectedId.HasValue)
            {
                _viewModel.SetSelectedEntity(FindEntityById(selectedId.Value));
                return;
            }
        }

        if (_scene.SelectionManager.SelectedCount > 1)
        {
            _viewModel.SetMultipleSelection(_scene.SelectionManager.SelectedCount);
            return;
        }

        _viewModel.SetSelectedEntity(null);
    }

    /// <summary>
    /// Normalizes user-entered entity names before comparing or applying rename commands.
    /// </summary>
    private static string NormalizeEntityName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Entity";
        }

        return name.Trim();
    }

    /// <summary>
    /// Normalizes user-entered support group names before comparing or applying rename commands.
    /// </summary>
    private static string NormalizeSupportGroupName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Supports Group";
        }

        return name.Trim();
    }

    /// <summary>
    /// Checks whether the current key event matches a control-key shortcut.
    /// </summary>
    private static bool IsControlShortcut(KeyEventArgs e, Key shortcutKey)
    {
        return e.Key == shortcutKey
            && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
    }

    /// <summary>
    /// Checks whether keyboard focus is currently inside a control that owns its own text undo and redo.
    /// </summary>
    private static bool IsKeyboardFocusInsideEditableControl()
    {
        DependencyObject? focusedElement = Keyboard.FocusedElement as DependencyObject;

        while (focusedElement != null)
        {
            if (focusedElement is TextBoxBase || focusedElement is PasswordBox)
            {
                return true;
            }

            if (focusedElement is ComboBox comboBox && comboBox.IsEditable)
            {
                return true;
            }

            focusedElement = GetUiParent(focusedElement);
        }

        return false;
    }

    /// <summary>
    /// Gets the logical or visual parent used when walking from the focused element back to the window.
    /// </summary>
    private static DependencyObject? GetUiParent(DependencyObject element)
    {
        DependencyObject? logicalParent = LogicalTreeHelper.GetParent(element);

        if (logicalParent != null)
        {
            return logicalParent;
        }

        if (element is Visual || element is Visual3D)
        {
            return VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    /// <summary>
    /// Cancels active tool previews before document-level file commands change entities.
    /// </summary>
    private void CancelTransientToolState()
    {
        _toolManager.CancelActiveTool();
    }

    /// <summary>
    /// Returns the workspace to selection mode after a document-level file command.
    /// </summary>
    private void ActivateSelectToolForDocumentCommand()
    {
        _viewModel.SetSelectedEntity(null);
        SetActiveMode(WorkspaceModeId.Select);
    }

    /// <summary>
    /// Applies user-facing status updates returned by document file commands.
    /// </summary>
    private void ApplyDocumentFileResult(DocumentFileOperationResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.StatusText))
        {
            _viewModel.SetStatusText(result.StatusText);
        }

        if (!string.IsNullOrWhiteSpace(result.ToolPanelText))
        {
            _viewModel.SetToolPanelText(result.ToolPanelText);
        }
    }
}
