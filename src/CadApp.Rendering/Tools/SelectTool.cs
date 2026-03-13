using CadApp.Core.Selection;
using CadApp.Core.Tools;
using CadApp.Rendering.Scene;
using HelixToolkit.Wpf.SharpDX;
using System.Windows;

namespace CadApp.Rendering.Tools;

public class SelectTool : ITool
{
    private readonly Viewport3DX _viewport;
    private readonly SceneManager _scene;
    private readonly SelectionManager _selection;

    public SelectTool(
        Viewport3DX viewport,
        SceneManager scene,
        SelectionManager selection)
    {
        _viewport = viewport;
        _scene = scene;
        _selection = selection;
    }

    public void OnMouseDown(double x, double y)
    {
        var hits = _viewport.FindHits(new Point(x, y));

        if (hits.Count > 0)
        {
            var entity = _scene.GetEntityFromVisual(hits[0].ModelHit);
            _selection.Select(entity);
        }
        else
        {
            _selection.Clear();
        }
    }

    public void OnMouseMove(double x, double y) { }

    public void OnMouseUp(double x, double y) { }
}
