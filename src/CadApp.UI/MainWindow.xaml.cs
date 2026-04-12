// MainWindow.xaml.cs
// Composes the WPF workspace shell with CAD document, scene, interaction, and lightweight dock-region behavior.
using CadApp.Core.Document;
using CadApp.Core.Entities;
using CadApp.Core.Import;
using CadApp.Core.Snapping;
using CadApp.Core.Tools;
using CadApp.Rendering.Math;
using CadApp.Rendering.Scene;
using CadApp.Rendering.Tools;
using CadApp.ViewModels;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Input;

namespace CadApp.UI;

public partial class MainWindow : Window
{
    private readonly CadDocument _document;
    private readonly SceneManager _scene;
    private readonly MainViewModel _viewModel;
    private readonly ProjectionService _projection;
    private readonly ToolManager _toolManager;
    private readonly SelectTool _selectTool;
    private readonly LineTool _lineTool;
    private readonly IModelImporter _stlImporter;
    private readonly SnapManager _snapManager;
    private string _activeToolStatusText = "Select tool active";
    private int _selectedEntityCount;

    public DefaultEffectsManager EffectsManager { get; }

    /// <summary>
    /// Creates the main application window and composes the current CAD services.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        EffectsManager = new DefaultEffectsManager();
        Viewport.EffectsManager = EffectsManager; // Needed since EffectsManager is initialised AFTER Main window is initialised. Data binding needs to be updated.

        _document = new CadDocument();
        _scene = new SceneManager(Viewport, _document);
        _snapManager = new SnapManager(_document.SpatialGrid);
        _projection = new ProjectionService(Viewport);
        _toolManager = new ToolManager();
        _selectTool = new SelectTool(Viewport, _scene, _scene.SelectionManager);
        _lineTool = new LineTool(_document, _projection, _scene, _snapManager);
        _stlImporter = new StlImporter();

        WireWorkspaceState();
        SetActiveTool(_selectTool, "Select tool active");
    }

    /// <summary>
    /// Connects domain events to shell UI state so workspace feedback stays outside the renderer.
    /// </summary>
    private void WireWorkspaceState()
    {
        _scene.SelectionManager.SelectionChanged += OnSelectionChanged;
        _viewModel.SetStatusText("Ready");
    }

    /// <summary>
    /// Handles viewport clicks by routing them to the active interaction logic and hit-test based selection.
    /// </summary>
    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _toolManager.ActiveTool?.OnMouseDown(GetScreenPosition(e));
    }

    /// <summary>
    /// Handles viewport mouse movement so interactive tools can update previews efficiently.
    /// </summary>
    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        _toolManager.ActiveTool?.OnMouseMove(GetScreenPosition(e));
    }

    /// <summary>
    /// Routes mouse-up events to the active interaction tool.
    /// </summary>
    private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _toolManager.ActiveTool?.OnMouseUp(GetScreenPosition(e));
    }

    private void SelectToolButton_Click(object sender, RoutedEventArgs e)
    {
        SetActiveTool(_selectTool, "Select tool active");
    }

    private void LineToolButton_Click(object sender, RoutedEventArgs e)
    {
        SetActiveTool(_lineTool, "Line tool active: click two points");
    }

    private void ImportStlButton_Click(object sender, RoutedEventArgs e)
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
            _document.Entities.Add(importedEntity);

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

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        _lineTool.Cancel();
        SetActiveTool(_selectTool, "Select tool active");
        e.Handled = true;
    }

    private void SetActiveTool(ITool tool, string statusText)
    {
        if (!ReferenceEquals(tool, _lineTool))
        {
            _lineTool.Cancel();
        }

        _activeToolStatusText = statusText;
        _toolManager.SetTool(tool);
        _viewModel.SetStatusText(statusText);
        _viewModel.SetToolPanelText(statusText);
    }

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
        _selectedEntityCount += CountIds(addedIds);
        _selectedEntityCount -= CountIds(removedIds);

        if (_selectedEntityCount < 0)
        {
            _selectedEntityCount = 0;
        }

        if (_selectedEntityCount > 0)
        {
            _viewModel.SetStatusText("Object Selected");
            return;
        }

        _viewModel.SetStatusText(_activeToolStatusText);
    }

    /// <summary>
    /// Counts identifiers in an enumerable without relying on LINQ during interactive updates.
    /// </summary>
    private static int CountIds(IEnumerable<Guid> ids)
    {
        int count = 0;

        foreach (Guid id in ids)
        {
            _ = id;
            count++;
        }

        return count;
    }
}
