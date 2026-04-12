// MainWindow.xaml.cs
// Composes the WPF workspace shell with CAD document, scene, interaction, and lightweight dock-region behavior.
using CadApp.Core.Document;
using CadApp.Core.Entities;
using CadApp.Core.Snapping;
using CadApp.Rendering.Math;
using CadApp.Rendering.Scene;
using CadApp.Rendering.Tools;
using CadApp.ViewModels;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using HitTestResult = HelixToolkit.SharpDX.HitTestResult;

namespace CadApp.UI;

public partial class MainWindow : Window
{
    private readonly CadDocument _document;
    private readonly SceneManager _scene;
    private readonly MainViewModel _viewModel;
    private readonly ProjectionService _projection;
    private readonly LineTool _lineTool;
    private readonly SnapManager _snapManager;
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
        Viewport.EffectsManager = EffectsManager;

        _document = new CadDocument();
        _scene = new SceneManager(Viewport, _document);
        _snapManager = new SnapManager(_document.SpatialGrid);
        _projection = new ProjectionService(Viewport);
        _lineTool = new LineTool(_document, _projection, _scene, _snapManager);

        WireWorkspaceState();
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
        _lineTool.OnMouseDown(e, Viewport);

        IList<HitTestResult> hits = Viewport.FindHits(e.GetPosition(Viewport));

        if (hits.Count == 0)
        {
            _scene.SelectionManager.ClearSelection();
            return;
        }

        HitTestResult result = hits[0];

        if (result.ModelHit is not Element3D modelHit)
        {
            _scene.SelectionManager.ClearSelection();
            return;
        }

        CadEntity? entity = _scene.GetEntityFromVisual(modelHit);

        if (entity != null)
        {
            _scene.SelectionManager.SelectSingle(entity);
            return;
        }

        _scene.SelectionManager.ClearSelection();
    }

    /// <summary>
    /// Handles viewport mouse movement so interactive tools can update previews efficiently.
    /// </summary>
    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        _lineTool.OnMouseMove(e, Viewport);
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

        _viewModel.SetStatusText("Ready");
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
