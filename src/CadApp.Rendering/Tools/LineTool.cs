using CadApp.Core.Document;
using CadApp.Core.Entities;
using CadApp.Core.Snapping;
using CadApp.Rendering.Math;
using CadApp.Rendering.Scene;
using System.Numerics;

namespace CadApp.Rendering.Tools;

public class LineTool : CadApp.Core.Tools.ITool
{
    private readonly CadDocument _document;
    private readonly ProjectionService _projection;
    private readonly SceneManager _scene;
    private readonly SnapManager _snapManager;
    private Vector3 _currentPoint;
    private bool _hasSnap;
    private SnapResult _snapResult;

    private Vector3? _startPoint;

    public LineTool(CadDocument document,
                    ProjectionService projection,
                    SceneManager scene,
                    SnapManager snapManager)
    {
        _document = document;
        _projection = projection;
        _scene = scene;
        _snapManager = snapManager;

    }

    public void OnMouseDown(Vector2 screenPosition)
    {
        if (!_projection.TryGetWorldPoint(screenPosition, out var worldPos))
            return;

        // SNAP
        if (_snapManager.TryGetSnap(worldPos, out var snap))
        {
            _currentPoint = snap.Position;
            _snapResult = snap;
            _hasSnap = true;
        }
        else
        {
            _currentPoint = worldPos;
            _hasSnap = false;
        }

        // Draw start point or line
        if (_startPoint == null)
        {
            _startPoint = _currentPoint;
        }
        else
        {
            _document.Entities.Add(new LineEntity(_startPoint.Value, _currentPoint));
            _scene.HidePreviewLine();
            _startPoint = null;
        }
    }

    public void OnMouseMove(Vector2 screenPosition)
    {
        if (!_projection.TryGetWorldPoint(screenPosition, out var worldPos))
            return;

        // SNAP
        if (_snapManager.TryGetSnap(worldPos, out var snap))
        {
            _currentPoint = snap.Position;
            _hasSnap = true;

        }
        else
        {
            _currentPoint = worldPos;
            _hasSnap = false;
        }

        if (_hasSnap)
        {
            _scene.ShowSnappingPoint(_currentPoint);
        }
        else
        {
            _scene.HideSnappingPoint();
        }

        if (_startPoint == null)
            return;

        _scene.ShowPreviewLine(_startPoint.Value, _currentPoint);
        
    }

    public void OnMouseUp(Vector2 screenPosition)
    {
    }

    public void Cancel()
    {
        _scene.HidePreviewLine();
        _scene.HideSnappingPoint();
        _startPoint = null;
        _hasSnap = false;
    }

}
