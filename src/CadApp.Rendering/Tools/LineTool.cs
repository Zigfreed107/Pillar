// LineTool.cs
// Handles interactive line creation while routing durable document changes through CAD commands.
using CadApp.Commands;
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
    private readonly CadCommandRunner _commandRunner;
    private Vector3 _currentPoint;
    private bool _hasSnap;
    private SnapResult _snapResult;

    private Vector3? _startPoint;

    /// <summary>
    /// Creates the interactive line tool used to place two-point line entities.
    /// </summary>
    public LineTool(CadDocument document,
                    ProjectionService projection,
                    SceneManager scene,
                    SnapManager snapManager,
                    CadCommandRunner commandRunner)
    {
        _document = document;
        _projection = projection;
        _scene = scene;
        _snapManager = snapManager;
        _commandRunner = commandRunner;

    }

    /// <summary>
    /// Captures the first point or completes a line using the current snapped/world point.
    /// </summary>
    public void OnMouseDown(Vector2 screenPosition)
    {
        Vector3 worldPosition;

        if (!_projection.TryGetWorldPoint(screenPosition, out worldPosition))
        {
            return;
        }

        // SNAP
        SnapResult snap;

        if (_snapManager.TryGetSnap(worldPosition, out snap))
        {
            _currentPoint = snap.Position;
            _snapResult = snap;
            _hasSnap = true;
        }
        else
        {
            _currentPoint = worldPosition;
            _hasSnap = false;
        }

        // Draw start point or line
        if (_startPoint == null)
        {
            _startPoint = _currentPoint;
        }
        else
        {
            LineEntity line = new LineEntity(_startPoint.Value, _currentPoint);
            _commandRunner.Execute(new AddEntityCommand(_document, line, "Add Line"));
            _scene.HidePreviewLine();
            _startPoint = null;
        }
    }

    /// <summary>
    /// Updates snapping feedback and preview geometry while the user is placing the line.
    /// </summary>
    public void OnMouseMove(Vector2 screenPosition)
    {
        Vector3 worldPosition;

        if (!_projection.TryGetWorldPoint(screenPosition, out worldPosition))
        {
            return;
        }

        // SNAP
        SnapResult snap;

        if (_snapManager.TryGetSnap(worldPosition, out snap))
        {
            _currentPoint = snap.Position;
            _hasSnap = true;

        }
        else
        {
            _currentPoint = worldPosition;
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
        {
            return;
        }

        _scene.ShowPreviewLine(_startPoint.Value, _currentPoint);
        
    }

    /// <summary>
    /// Handles mouse release; line creation is committed on clicks, so no release work is needed yet.
    /// </summary>
    public void OnMouseUp(Vector2 screenPosition)
    {
    }

    /// <summary>
    /// Cancels any in-progress line and hides transient preview and snap feedback.
    /// </summary>
    public void Cancel()
    {
        _scene.HidePreviewLine();
        _scene.HideSnappingPoint();
        _startPoint = null;
        _hasSnap = false;
    }

}
