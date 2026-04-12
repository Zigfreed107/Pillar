using CadApp.Core.Entities;
using CadApp.Rendering.Scene;
using HelixToolkit.Wpf.SharpDX;
using System.Numerics;
using System.Windows;

namespace CadApp.Rendering.Tools;

public class SelectTool : CadApp.Core.Tools.ITool
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

    public void OnMouseDown(Vector2 screenPosition)
    {
        var hits = _viewport.FindHits(new Point(screenPosition.X, screenPosition.Y));

        if (hits.Count > 0 && hits[0].ModelHit is Element3D modelHit)
        {
            CadEntity? entity = _scene.GetEntityFromVisual(modelHit);

            if (entity != null)
            {
                _selection.SelectSingle(entity);
                return;
            }
        }

        _selection.ClearSelection();
    }

    public void OnMouseMove(Vector2 screenPosition) { }

    public void OnMouseUp(Vector2 screenPosition) { }
}
