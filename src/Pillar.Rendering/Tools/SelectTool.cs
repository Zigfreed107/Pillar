// SelectTool.cs
// Converts selection-mode mouse gestures into domain selection changes while keeping viewport hit-testing in the rendering layer.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Rendering.Scene;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace Pillar.Rendering.Tools;

/// <summary>
/// Describes the screen-space selection rectangle that the WPF shell should draw over the viewport.
/// </summary>
public readonly struct SelectionWindowOverlayState
{
    /// <summary>
    /// Creates immutable overlay state for the active selection drag.
    /// </summary>
    public SelectionWindowOverlayState(
        bool isVisible,
        double left,
        double top,
        double width,
        double height,
        bool selectsCrossingEntities)
    {
        IsVisible = isVisible;
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        SelectsCrossingEntities = selectsCrossingEntities;
    }

    public bool IsVisible { get; }
    public double Left { get; }
    public double Top { get; }
    public double Width { get; }
    public double Height { get; }
    public bool SelectsCrossingEntities { get; }
}

/// <summary>
/// Handles normal CAD selection, including modifier-based click selection and rectangular window selection.
/// </summary>
public class SelectTool : Pillar.Core.Tools.ITool
{
    private const float DragThresholdPixels = 4.0f;
    private const float DragThresholdSquared = DragThresholdPixels * DragThresholdPixels;

    private enum WindowSelectionOperation
    {
        Replace,
        Add,
        Subtract
    }

    private enum ClickSelectionOperation
    {
        Replace,
        Add,
        Subtract
    }

    private readonly Viewport3DX _viewport;
    private readonly CadDocument _document;
    private readonly SceneManager _scene;
    private readonly SelectionManager _selection;
    private readonly List<CadEntity> _windowSelectionBuffer = new List<CadEntity>(64);

    private Vector2 _mouseDownPosition;
    private bool _isMouseDown;
    private bool _isDraggingWindow;

    public SelectTool(
        Viewport3DX viewport,
        CadDocument document,
        SceneManager scene,
        SelectionManager selection)
    {
        _viewport = viewport;
        _document = document;
        _scene = scene;
        _selection = selection;
    }

    /// <summary>
    /// Raised while the user drags a window selection rectangle.
    /// </summary>
    public event Action<SelectionWindowOverlayState>? SelectionWindowChanged;

    /// <summary>
    /// Starts a possible click or window-selection gesture.
    /// </summary>
    public void OnMouseDown(Vector2 screenPosition)
    {
        _mouseDownPosition = screenPosition;
        _isMouseDown = true;
        _isDraggingWindow = false;
    }

    /// <summary>
    /// Updates the window-selection overlay once the mouse has moved far enough to count as a drag.
    /// </summary>
    public void OnMouseMove(Vector2 screenPosition)
    {
        if (!_isMouseDown)
        {
            return;
        }

        if (!_isDraggingWindow && Vector2.DistanceSquared(_mouseDownPosition, screenPosition) < DragThresholdSquared)
        {
            return;
        }

        _isDraggingWindow = true;
        PublishSelectionWindow(screenPosition);
    }

    /// <summary>
    /// Commits the pending click or window-selection gesture.
    /// </summary>
    public void OnMouseUp(Vector2 screenPosition)
    {
        if (!_isMouseDown)
        {
            return;
        }

        if (_isDraggingWindow)
        {
            Rect selectionRect = CreateScreenRect(_mouseDownPosition, screenPosition);
            bool selectsCrossingEntities = IsRightToLeftDrag(_mouseDownPosition, screenPosition);
            WindowSelectionOperation operation = GetWindowSelectionOperation();

            HideSelectionWindow();
            ApplyWindowSelection(selectionRect, selectsCrossingEntities, operation);
            ResetGestureState();
            return;
        }

        ApplyClickSelection(screenPosition);
        ResetGestureState();
    }

    /// <summary>
    /// Cancels transient selection UI without changing the current selected entities.
    /// </summary>
    public void Cancel()
    {
        HideSelectionWindow();
        ResetGestureState();
    }

    /// <summary>
    /// Applies single-click selection using replace, add, or subtract semantics.
    /// </summary>
    private void ApplyClickSelection(Vector2 screenPosition)
    {
        CadEntity? hitEntity = FindHitEntity(screenPosition);
        ClickSelectionOperation operation = GetClickSelectionOperation();

        if (hitEntity != null)
        {
            if (operation == ClickSelectionOperation.Subtract)
            {
                _selection.RemoveFromSelection(hitEntity);
                return;
            }

            if (operation == ClickSelectionOperation.Add)
            {
                _selection.AddToSelection(hitEntity);
                return;
            }

            _selection.SelectSingle(hitEntity);
            return;
        }

        if (operation == ClickSelectionOperation.Replace)
        {
            _selection.ClearSelection();
        }
    }

    /// <summary>
    /// Applies rectangular selection to all document entities using the requested crossing/window rule.
    /// </summary>
    private void ApplyWindowSelection(
        Rect selectionRect,
        bool selectsCrossingEntities,
        WindowSelectionOperation operation)
    {
        _windowSelectionBuffer.Clear();

        foreach (CadEntity entity in _document.Entities)
        {
            if (IsEntitySelectedByWindow(entity, selectionRect, selectsCrossingEntities))
            {
                _windowSelectionBuffer.Add(entity);
            }
        }

        if (operation == WindowSelectionOperation.Subtract)
        {
            _selection.RemoveRangeFromSelection(_windowSelectionBuffer);
            return;
        }

        if (operation == WindowSelectionOperation.Add)
        {
            _selection.AddRangeToSelection(_windowSelectionBuffer);
            return;
        }

        _selection.SelectMany(_windowSelectionBuffer);
    }

    /// <summary>
    /// Finds the nearest selectable entity under a screen point.
    /// </summary>
    private CadEntity? FindHitEntity(Vector2 screenPosition)
    {
        Point hitPoint = new Point(screenPosition.X, screenPosition.Y);
        IList<HitTestResult> hits = _viewport.FindHits(hitPoint);

        for (int i = 0; i < hits.Count; i++)
        {
            if (hits[i].ModelHit is Element3D modelHit)
            {
                CadEntity? entity = _scene.GetEntityFromVisual(modelHit);

                if (entity != null)
                {
                    return entity;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether an entity is selected by the active rectangle rule.
    /// </summary>
    private bool IsEntitySelectedByWindow(CadEntity entity, Rect selectionRect, bool selectsCrossingEntities)
    {
        if (entity is LineEntity line)
        {
            return IsLineSelectedByWindow(line, selectionRect, selectsCrossingEntities);
        }

        Rect projectedBounds;

        if (!TryGetProjectedBounds(entity, out projectedBounds))
        {
            return false;
        }

        if (selectsCrossingEntities)
        {
            return selectionRect.IntersectsWith(projectedBounds);
        }

        return selectionRect.Contains(projectedBounds);
    }

    /// <summary>
    /// Tests line entities with segment math so crossing selection does not depend only on bounding boxes.
    /// </summary>
    private bool IsLineSelectedByWindow(LineEntity line, Rect selectionRect, bool selectsCrossingEntities)
    {
        Point startPoint;
        Point endPoint;

        if (!TryProjectWorldPoint(line.Start, out startPoint) || !TryProjectWorldPoint(line.End, out endPoint))
        {
            return false;
        }

        if (!selectsCrossingEntities)
        {
            return selectionRect.Contains(startPoint) && selectionRect.Contains(endPoint);
        }

        if (selectionRect.Contains(startPoint) || selectionRect.Contains(endPoint))
        {
            return true;
        }

        return SegmentIntersectsRectangle(startPoint, endPoint, selectionRect);
    }

    /// <summary>
    /// Projects an entity's 3D axis-aligned bounds to a conservative screen-space rectangle.
    /// </summary>
    private bool TryGetProjectedBounds(CadEntity entity, out Rect projectedBounds)
    {
        (Vector3 Min, Vector3 Max) bounds = entity.GetBounds();
        bool hasProjectedPoint = false;
        projectedBounds = Rect.Empty;

        if (!TryAppendProjectedBoundsPoint(new Vector3(bounds.Min.X, bounds.Min.Y, bounds.Min.Z), ref projectedBounds, ref hasProjectedPoint))
        {
            return false;
        }

        if (!TryAppendProjectedBoundsPoint(new Vector3(bounds.Max.X, bounds.Min.Y, bounds.Min.Z), ref projectedBounds, ref hasProjectedPoint))
        {
            return false;
        }

        if (!TryAppendProjectedBoundsPoint(new Vector3(bounds.Min.X, bounds.Max.Y, bounds.Min.Z), ref projectedBounds, ref hasProjectedPoint))
        {
            return false;
        }

        if (!TryAppendProjectedBoundsPoint(new Vector3(bounds.Max.X, bounds.Max.Y, bounds.Min.Z), ref projectedBounds, ref hasProjectedPoint))
        {
            return false;
        }

        if (!TryAppendProjectedBoundsPoint(new Vector3(bounds.Min.X, bounds.Min.Y, bounds.Max.Z), ref projectedBounds, ref hasProjectedPoint))
        {
            return false;
        }

        if (!TryAppendProjectedBoundsPoint(new Vector3(bounds.Max.X, bounds.Min.Y, bounds.Max.Z), ref projectedBounds, ref hasProjectedPoint))
        {
            return false;
        }

        if (!TryAppendProjectedBoundsPoint(new Vector3(bounds.Min.X, bounds.Max.Y, bounds.Max.Z), ref projectedBounds, ref hasProjectedPoint))
        {
            return false;
        }

        if (!TryAppendProjectedBoundsPoint(new Vector3(bounds.Max.X, bounds.Max.Y, bounds.Max.Z), ref projectedBounds, ref hasProjectedPoint))
        {
            return false;
        }

        return hasProjectedPoint;
    }

    /// <summary>
    /// Adds one projected bounds corner to the accumulated screen-space rectangle.
    /// </summary>
    private bool TryAppendProjectedBoundsPoint(Vector3 worldPoint, ref Rect projectedBounds, ref bool hasProjectedPoint)
    {
        Point screenPoint;

        if (!TryProjectWorldPoint(worldPoint, out screenPoint))
        {
            return false;
        }

        if (!hasProjectedPoint)
        {
            projectedBounds = new Rect(screenPoint, screenPoint);
            hasProjectedPoint = true;
            return true;
        }

        projectedBounds.Union(screenPoint);
        return true;
    }

    /// <summary>
    /// Projects a world point into viewport pixel coordinates and rejects invalid results.
    /// </summary>
    private bool TryProjectWorldPoint(Vector3 worldPoint, out Point screenPoint)
    {
        screenPoint = _viewport.Project(new Point3D(worldPoint.X, worldPoint.Y, worldPoint.Z));

        return IsFinite(screenPoint.X) && IsFinite(screenPoint.Y);
    }

    /// <summary>
    /// Publishes overlay geometry for the shell to draw.
    /// </summary>
    private void PublishSelectionWindow(Vector2 screenPosition)
    {
        Rect selectionRect = CreateScreenRect(_mouseDownPosition, screenPosition);
        bool selectsCrossingEntities = !IsRightToLeftDrag(_mouseDownPosition, screenPosition);

        SelectionWindowChanged?.Invoke(new SelectionWindowOverlayState(
            true,
            selectionRect.Left,
            selectionRect.Top,
            selectionRect.Width,
            selectionRect.Height,
            selectsCrossingEntities));
    }

    /// <summary>
    /// Hides the transient window-selection overlay.
    /// </summary>
    private void HideSelectionWindow()
    {
        SelectionWindowChanged?.Invoke(new SelectionWindowOverlayState(false, 0.0, 0.0, 0.0, 0.0, false));
    }

    /// <summary>
    /// Creates a positive-size rectangle from two viewport pixel positions.
    /// </summary>
    private static Rect CreateScreenRect(Vector2 startPosition, Vector2 endPosition)
    {
        double left = System.Math.Min(startPosition.X, endPosition.X);
        double top = System.Math.Min(startPosition.Y, endPosition.Y);
        double width = System.Math.Abs(endPosition.X - startPosition.X);
        double height = System.Math.Abs(endPosition.Y - startPosition.Y);

        return new Rect(left, top, width, height);
    }

    /// <summary>
    /// Returns true when the drag direction should use crossing selection.
    /// </summary>
    private static bool IsRightToLeftDrag(Vector2 startPosition, Vector2 endPosition)
    {
        return endPosition.X < startPosition.X;
    }

    /// <summary>
    /// Tests whether a 2D line segment crosses any edge of a rectangle.
    /// </summary>
    private static bool SegmentIntersectsRectangle(Point startPoint, Point endPoint, Rect rectangle)
    {
        Point topLeft = new Point(rectangle.Left, rectangle.Top);
        Point topRight = new Point(rectangle.Right, rectangle.Top);
        Point bottomRight = new Point(rectangle.Right, rectangle.Bottom);
        Point bottomLeft = new Point(rectangle.Left, rectangle.Bottom);

        return SegmentsIntersect(startPoint, endPoint, topLeft, topRight)
            || SegmentsIntersect(startPoint, endPoint, topRight, bottomRight)
            || SegmentsIntersect(startPoint, endPoint, bottomRight, bottomLeft)
            || SegmentsIntersect(startPoint, endPoint, bottomLeft, topLeft);
    }

    /// <summary>
    /// Tests two 2D line segments, including collinear overlap along rectangle edges.
    /// </summary>
    private static bool SegmentsIntersect(Point firstStart, Point firstEnd, Point secondStart, Point secondEnd)
    {
        double firstDirection = Cross(secondStart, secondEnd, firstStart);
        double secondDirection = Cross(secondStart, secondEnd, firstEnd);
        double thirdDirection = Cross(firstStart, firstEnd, secondStart);
        double fourthDirection = Cross(firstStart, firstEnd, secondEnd);

        if (((firstDirection > 0.0 && secondDirection < 0.0) || (firstDirection < 0.0 && secondDirection > 0.0))
            && ((thirdDirection > 0.0 && fourthDirection < 0.0) || (thirdDirection < 0.0 && fourthDirection > 0.0)))
        {
            return true;
        }

        return IsPointOnSegment(secondStart, secondEnd, firstStart, firstDirection)
            || IsPointOnSegment(secondStart, secondEnd, firstEnd, secondDirection)
            || IsPointOnSegment(firstStart, firstEnd, secondStart, thirdDirection)
            || IsPointOnSegment(firstStart, firstEnd, secondEnd, fourthDirection);
    }

    /// <summary>
    /// Calculates the signed 2D cross product used by segment intersection.
    /// </summary>
    private static double Cross(Point lineStart, Point lineEnd, Point point)
    {
        return ((point.X - lineStart.X) * (lineEnd.Y - lineStart.Y))
            - ((point.Y - lineStart.Y) * (lineEnd.X - lineStart.X));
    }

    /// <summary>
    /// Tests whether a point lies on a segment when the caller already knows it is collinear.
    /// </summary>
    private static bool IsPointOnSegment(Point segmentStart, Point segmentEnd, Point point, double cross)
    {
        const double Tolerance = 0.0001;

        if (System.Math.Abs(cross) > Tolerance)
        {
            return false;
        }

        return point.X >= System.Math.Min(segmentStart.X, segmentEnd.X) - Tolerance
            && point.X <= System.Math.Max(segmentStart.X, segmentEnd.X) + Tolerance
            && point.Y >= System.Math.Min(segmentStart.Y, segmentEnd.Y) - Tolerance
            && point.Y <= System.Math.Max(segmentStart.Y, segmentEnd.Y) + Tolerance;
    }

    /// <summary>
    /// Chooses how a window selection should modify the current selection.
    /// </summary>
    private static WindowSelectionOperation GetWindowSelectionOperation()
    {
        if (IsControlModifierDown())
        {
            return WindowSelectionOperation.Subtract;
        }

        if (IsShiftModifierDown())
        {
            return WindowSelectionOperation.Add;
        }

        return WindowSelectionOperation.Replace;
    }

    /// <summary>
    /// Chooses how a click selection should modify the current selection.
    /// </summary>
    private static ClickSelectionOperation GetClickSelectionOperation()
    {
        if (IsControlModifierDown())
        {
            return ClickSelectionOperation.Subtract;
        }

        if (IsShiftModifierDown())
        {
            return ClickSelectionOperation.Add;
        }

        return ClickSelectionOperation.Replace;
    }

    /// <summary>
    /// Reads the current CTRL modifier state for subtractive selection.
    /// </summary>
    private static bool IsControlModifierDown()
    {
        return (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
    }

    /// <summary>
    /// Reads the current SHIFT modifier state for additive window selection.
    /// </summary>
    private static bool IsShiftModifierDown()
    {
        return (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
    }

    /// <summary>
    /// Rejects NaN and infinity from projection math.
    /// </summary>
    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// Clears gesture flags after a click, drag, or cancel.
    /// </summary>
    private void ResetGestureState()
    {
        _isMouseDown = false;
        _isDraggingWindow = false;
    }
}
