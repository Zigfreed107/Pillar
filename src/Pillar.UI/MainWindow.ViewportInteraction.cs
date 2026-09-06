// MainWindow.ViewportInteraction.cs
// Hosts viewport input and camera-navigation glue so the shell can route WPF events into CAD tools without mixing that code with unrelated workflows.
using Pillar.Rendering.Math;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Tools;
using Pillar.UI.Interaction;
using Pillar.UI.Modes;
using System;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace Pillar.UI;

public partial class MainWindow
{
    private const long QuickRightClickMaximumDurationMilliseconds = 350;
    private Point _rightButtonPressPosition;
    private long _rightButtonPressTimeMilliseconds;
    private bool _isRightButtonCompletionGesturePending;
    private bool _rightButtonExceededDragThreshold;

    /// <summary>
    /// Observes preview input even when Helix marks camera-navigation events handled.
    /// </summary>
    private void ConfigureViewportRightClickCompletionGesture()
    {
        Viewport.AddHandler(
            Mouse.PreviewMouseDownEvent,
            new MouseButtonEventHandler(Viewport_PreviewMouseDownForDrawingCompletion),
            true);
        Viewport.AddHandler(
            Mouse.PreviewMouseMoveEvent,
            new MouseEventHandler(Viewport_PreviewMouseMoveForDrawingCompletion),
            true);
        Viewport.AddHandler(
            Mouse.PreviewMouseUpEvent,
            new MouseButtonEventHandler(Viewport_PreviewMouseUpForDrawingCompletion),
            true);
    }

    /// <summary>
    /// Starts observing a possible drawing-completion click without consuming Helix's right-button navigation input.
    /// </summary>
    private void Viewport_PreviewMouseDownForDrawingCompletion(object sender, MouseButtonEventArgs e)
    {
        _ = sender;

        if (e.ChangedButton != MouseButton.Right)
        {
            return;
        }

        if (_faceSetSelectionTool == null || !_faceSetSelectionTool.IsDrawingInProgress)
        {
            ResetRightButtonCompletionGesture();
            return;
        }

        _rightButtonPressPosition = e.GetPosition(Viewport);
        _rightButtonPressTimeMilliseconds = Environment.TickCount64;
        _rightButtonExceededDragThreshold = false;
        _isRightButtonCompletionGesturePending = true;
    }

    /// <summary>
    /// Tracks the farthest movement while Helix handles a possible right-button camera orbit.
    /// </summary>
    private void Viewport_PreviewMouseMoveForDrawingCompletion(object sender, MouseEventArgs e)
    {
        _ = sender;
        UpdateRightButtonCompletionGesture(e);
    }

    /// <summary>
    /// Finishes an active face-selection drawing only when the full right-button gesture remained a quick click.
    /// </summary>
    private void Viewport_PreviewMouseUpForDrawingCompletion(object sender, MouseButtonEventArgs e)
    {
        _ = sender;

        if (e.ChangedButton != MouseButton.Right || !_isRightButtonCompletionGesturePending)
        {
            return;
        }

        Point releasePosition = e.GetPosition(Viewport);
        long elapsedMilliseconds = Environment.TickCount64 - _rightButtonPressTimeMilliseconds;
        bool isQuickClick = ViewportRightClickGestureClassifier.IsQuickClick(
            _rightButtonPressPosition,
            releasePosition,
            _rightButtonExceededDragThreshold,
            elapsedMilliseconds,
            SystemParameters.MinimumHorizontalDragDistance,
            SystemParameters.MinimumVerticalDragDistance,
            QuickRightClickMaximumDurationMilliseconds);
        ResetRightButtonCompletionGesture();

        if (isQuickClick)
        {
            TryHandleFaceSetSelectionFinish();
        }
    }

    /// <summary>
    /// Handles viewport clicks by routing tool input into the active CAD tool while Helix owns navigation gestures.
    /// </summary>
    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _ = sender;

        if (e.ChangedButton == MouseButton.Right && IsRingSupportOperationActive())
        {
            _manualSupportTool.Cancel();
            ExitRingSupportMode();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        Vector2 screenPosition = GetScreenPosition(e);
        SynchronizeLayerPanelSelectionFromViewportHit(screenPosition);

        _toolManager.ActiveTool?.OnMouseDown(screenPosition);

        Viewport.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>
    /// Handles viewport mouse movement so CAD previews update only when Helix is not consuming the pointer for navigation.
    /// </summary>
    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        _ = sender;

        if (e.RightButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed)
        {
            return;
        }

        Vector2 screenPosition = GetScreenPosition(e);
        _toolManager.ActiveTool?.OnMouseMove(screenPosition);
        UpdateScaledCursorPreview(screenPosition);

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Routes left-button mouse-up events to the active CAD tool while leaving Helix-owned navigation buttons alone.
    /// </summary>
    private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _ = sender;

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _toolManager.ActiveTool?.OnMouseUp(GetScreenPosition(e));

        if (Viewport.IsMouseCaptured)
        {
            Viewport.ReleaseMouseCapture();
        }

        e.Handled = true;
    }

    /// <summary>
    /// Converts a WPF mouse event into the float screen coordinate format used by CAD tools.
    /// </summary>
    private Vector2 GetScreenPosition(MouseEventArgs e)
    {
        Point mousePosition = e.GetPosition(Viewport);
        return new Vector2((float)mousePosition.X, (float)mousePosition.Y);
    }

    /// <summary>
    /// Remembers if any point in the right-button gesture crossed WPF's system drag threshold.
    /// </summary>
    private void UpdateRightButtonCompletionGesture(MouseEventArgs e)
    {
        if (!_isRightButtonCompletionGesturePending || _rightButtonExceededDragThreshold)
        {
            return;
        }

        Point currentPosition = e.GetPosition(Viewport);
        double horizontalMovement = Math.Abs(currentPosition.X - _rightButtonPressPosition.X);
        double verticalMovement = Math.Abs(currentPosition.Y - _rightButtonPressPosition.Y);

        if (horizontalMovement >= SystemParameters.MinimumHorizontalDragDistance
            || verticalMovement >= SystemParameters.MinimumVerticalDragDistance)
        {
            _rightButtonExceededDragThreshold = true;
        }
    }

    /// <summary>
    /// Clears transient right-button classification state after completion or an unrelated press.
    /// </summary>
    private void ResetRightButtonCompletionGesture()
    {
        _isRightButtonCompletionGesturePending = false;
        _rightButtonExceededDragThreshold = false;
    }

    /// <summary>
    /// Gets whether left-click handling may trigger Ring Support hit testing or marker projection.
    /// </summary>
    private bool IsRingSupportOperationActive()
    {
        return _activeModeId == WorkspaceModeId.ManualSupport
            && _manualSupportTool.ActiveOperationKind == ManualSupportOperationKind.Ring;
    }

    /// <summary>
    /// Gets whether keyboard handling should finish the active Line Support polyline.
    /// </summary>
    private bool IsLineSupportOperationActive()
    {
        return _activeModeId == WorkspaceModeId.ManualSupport
            && _manualSupportTool.ActiveOperationKind == ManualSupportOperationKind.Line;
    }
}
