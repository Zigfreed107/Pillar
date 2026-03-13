using CadApp.Core.Document;
using CadApp.Core.Entities;
using CadApp.Core.Selection;
using CadApp.Core.Tools;
using CadApp.Rendering.Scene;
using CadApp.Rendering.Tools;
using System.Numerics;
using System.Windows;
using System.Windows.Input;

namespace CadApp.UI;

public partial class MainWindow : Window
{
    private readonly CadDocument _document;
    private readonly SceneManager _scene;
    private readonly ToolManager _tools = new();

    private readonly SelectionManager _selection = new();


    public MainWindow()
    {
        InitializeComponent();

        _document = new CadDocument();

        _scene = new SceneManager(Viewport, _document);
        //_tools.SetTool(new SelectTool(Viewport, _scene, _selection));
        _tools.SetTool(new LineTool(Viewport, _document));

        // Test entity
        _document.Entities.Add(new LineEntity
        {
            Start = new Vector3(0, 0, 0),
            End = new Vector3(5, 5, 0)
        });

        _document.Entities.Add(new LineEntity
        {
            Start = new Vector3(0, 0, 0),
            End = new Vector3(0, 5, 5)
        });

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
        var pos = e.GetPosition(Viewport);
        _tools.ActiveTool?.OnMouseDown(pos.X, pos.Y);
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(Viewport);
        _tools.ActiveTool?.OnMouseMove(pos.X, pos.Y);
    }

    private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(Viewport);
        _tools.ActiveTool?.OnMouseUp(pos.X, pos.Y);
    }
}
