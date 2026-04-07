using CadApp.Core.Document;
using CadApp.Core.Entities;
using CadApp.Core.Snapping;
using CadApp.Core.Spatial;
using CadApp.Rendering.Math;
using CadApp.Rendering.Scene;
using HelixToolkit.Geometry;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SharpDX;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Windows;
using System.Windows.Input;

namespace CadApp.Rendering.Tools;

public class LineTool : ITool
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

        var builder = new MeshBuilder();
        builder.AddSphere(Vector3.Zero, 0.05f);

    }

    public void OnMouseDown(MouseButtonEventArgs e, Viewport3DX viewport)
    {
        var pos = e.GetPosition(viewport);

        if (!_projection.TryGetWorldPoint(pos, out var worldPos))
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

    public void OnMouseMove(MouseEventArgs e, Viewport3DX viewport)
    {
        if (_startPoint == null)
            return;

        var pos = e.GetPosition(viewport);

        if (!_projection.TryGetWorldPoint(pos, out var worldPos))
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

        _scene.ShowPreviewLine(_startPoint.Value, _currentPoint);
        
    }

}