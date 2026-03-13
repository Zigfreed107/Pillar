using CadApp.Core.Document;
using CadApp.Core.Entities;
using CadApp.Core.Tools;
using CadApp.Rendering.Math;
using HelixToolkit.SharpDX.Core;
using HelixToolkit.Wpf.SharpDX;
using SharpDX;
using System.Numerics;

namespace CadApp.Rendering.Tools;

public class LineTool : ITool
{
    private readonly Viewport3DX _viewport;
    private readonly CadDocument _document;

    private bool _isDrawing;
    private Vector3 _startPoint;

    private LineGeometryModel3D? _previewLine;

    public LineTool(Viewport3DX viewport, CadDocument document)
    {
        _viewport = viewport;
        _document = document;
    }

    public void OnMouseDown(double x, double y)
    {
        if (!TryGetWorldPoint(x, y, out var point))
            return;

        if (!_isDrawing)
        {
            _startPoint = point;
            _isDrawing = true;

            _previewLine = CreateLineModel(_startPoint, point, Color.Red);
            _viewport.Items.Add(_previewLine);
        }
        else
        {
            var line = new LineEntity
            {
                Start = _startPoint,
                End = point
            };

            _document.Entities.Add(line);

            if (_previewLine != null)
                _viewport.Items.Remove(_previewLine);

            _previewLine = null;
            _isDrawing = false;
        }
    }

    public void OnMouseMove(double x, double y)
    {
        if (!_isDrawing || _previewLine == null)
            return;

        if (!TryGetWorldPoint(x, y, out var point))
            return;

        _previewLine.Geometry = CreateLineGeometry(_startPoint, point);
    }

    public void OnMouseUp(double x, double y) { }

    private bool TryGetWorldPoint(double x, double y, out Vector3 point)
    {
        return Workplane.TryGetPointOnPlane(_viewport, x, y, out point);
    }

    private static LineGeometryModel3D CreateLineModel(Vector3 start, Vector3 end, Color color)
    {
        return new LineGeometryModel3D
        {
            Color = color,
            Thickness = 2,
            Geometry = CreateLineGeometry(start, end)
        };
    }

    private static LineGeometry3D CreateLineGeometry(Vector3 start, Vector3 end)
    {
        var builder = new LineBuilder();
        builder.AddLine(
            new SharpDX.Vector3(start.X, start.Y, start.Z),
            new SharpDX.Vector3(end.X, end.Y, end.Z));

        return builder.ToLineGeometry3D();
    }
}
