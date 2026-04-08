using CadApp.Core.Document;
using CadApp.Core.Entities;
using CadApp.Core.Selection;
using CadApp.Core.Snapping;
using CadApp.Core.Spatial;
using CadApp.Core.Tools;
using CadApp.Rendering.Math;
using CadApp.Rendering.Scene;
using CadApp.Rendering.Tools;
using HelixToolkit.Geometry;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CadApp.UI;

public partial class MainWindow : Window
{
    private readonly CadDocument _document;
    private readonly SceneManager _scene;
    private readonly ToolManager _tools = new();
    private readonly SelectionManager _selection = new();
    private ProjectionService _projection;
    private LineTool _lineTool;
    private SnapManager _snapManager;
    
    public DefaultEffectsManager EffectsManager { get; }
    public MainWindow()
    {
        InitializeComponent();

        this.DataContext = this;        //QUick Fix - should be using a ViewModel

        EffectsManager = new DefaultEffectsManager();
 

        _document = new CadDocument();

        _scene = new SceneManager(Viewport, _document);

        _snapManager = new SnapManager(_document.SpatialGrid);

        _projection = new ProjectionService(Viewport);
        _lineTool = new LineTool(_document, _projection, _scene, _snapManager);

        _selection.SelectionChanged += entity =>
        {
            if (entity != null)
                Title = $"Selected: {entity.Id}";
            else
                Title = "CadApp";
        };
    }

    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _lineTool.OnMouseDown(e, Viewport);
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        _lineTool.OnMouseMove(e, Viewport);
    }

    private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(Viewport);
        _tools.ActiveTool?.OnMouseUp(pos.X, pos.Y);
    }
}
