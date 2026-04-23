// MainWindow.ViewportInteraction.cs
// Hosts viewport input and camera-navigation glue so the shell can route WPF events into CAD tools without mixing that code with unrelated workflows.
using Pillar.Rendering.Math;
using HelixToolkit.Wpf.SharpDX;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace Pillar.UI;

public partial class MainWindow
{
    /// <summary>
    /// Handles viewport clicks by routing them to the active interaction logic and hit-test based selection.
    /// </summary>
    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (TryStartViewportPan(e))
        {
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _toolManager.ActiveTool?.OnMouseDown(GetScreenPosition(e));
        Viewport.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>
    /// Handles viewport mouse movement so interactive tools can update previews efficiently.
    /// </summary>
    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (TryUpdateViewportPan(e))
        {
            return;
        }

        _toolManager.ActiveTool?.OnMouseMove(GetScreenPosition(e));

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Routes mouse-up events to the active interaction tool.
    /// </summary>
    private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (TryEndViewportPan(e))
        {
            return;
        }

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
    /// Applies wheel zoom using the cursor hit point when one is available.
    /// </summary>
    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _ = sender;

        Point mousePosition = e.GetPosition(Viewport);

        if (TryGetViewportZoomOrigin(mousePosition, out Point3D zoomOrigin))
        {
            Viewport.AddZoomForce(e.Delta, zoomOrigin);
        }
        else
        {
            Viewport.AddZoomForce(e.Delta);
        }

        e.Handled = true;
    }

    /// <summary>
    /// Starts shell-owned panning when the user drags with the middle mouse button.
    /// </summary>
    private bool TryStartViewportPan(MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return false;
        }

        _activeViewportNavigationMode = ViewportNavigationMode.Pan;
        _lastViewportNavigationPoint = e.GetPosition(Viewport);
        Viewport.CaptureMouse();
        e.Handled = true;
        return true;
    }

    /// <summary>
    /// Updates shell-owned panning while the middle mouse button remains pressed.
    /// </summary>
    private bool TryUpdateViewportPan(MouseEventArgs e)
    {
        if (_activeViewportNavigationMode != ViewportNavigationMode.Pan)
        {
            return false;
        }

        if (e.MiddleButton != MouseButtonState.Pressed)
        {
            StopViewportPan();
            return false;
        }

        Point currentPoint = e.GetPosition(Viewport);
        System.Windows.Vector delta = currentPoint - _lastViewportNavigationPoint;

        if (delta.LengthSquared > 0.0)
        {
            Viewport.AddPanForce(delta.X, delta.Y);
            _lastViewportNavigationPoint = currentPoint;
        }

        e.Handled = true;
        return true;
    }

    /// <summary>
    /// Ends shell-owned panning when the middle mouse button is released.
    /// </summary>
    private bool TryEndViewportPan(MouseButtonEventArgs e)
    {
        if (_activeViewportNavigationMode != ViewportNavigationMode.Pan || e.ChangedButton != MouseButton.Middle)
        {
            return false;
        }

        StopViewportPan();
        e.Handled = true;
        return true;
    }

    /// <summary>
    /// Clears the current shell-owned pan state.
    /// </summary>
    private void StopViewportPan()
    {
        _activeViewportNavigationMode = ViewportNavigationMode.None;

        if (Viewport.IsMouseCaptured)
        {
            Viewport.ReleaseMouseCapture();
        }
    }

    /// <summary>
    /// Gets the world-space zoom origin under the cursor from model hits or the workplane.
    /// </summary>
    private bool TryGetViewportZoomOrigin(Point mousePosition, out Point3D zoomOrigin)
    {
        Vector2 screenPosition = new Vector2((float)mousePosition.X, (float)mousePosition.Y);

        if (_projection.TryGetMeshSurfaceHit(screenPosition, out MeshSurfaceHit meshSurfaceHit))
        {
            zoomOrigin = new Point3D(meshSurfaceHit.HitPosition.X, meshSurfaceHit.HitPosition.Y, meshSurfaceHit.HitPosition.Z);
            return true;
        }

        if (_projection.TryGetWorldPoint(mousePosition, out Vector3 worldPoint))
        {
            zoomOrigin = new Point3D(worldPoint.X, worldPoint.Y, worldPoint.Z);
            return true;
        }

        zoomOrigin = default;
        return false;
    }

    /// <summary>
    /// Converts a WPF mouse event into the float screen coordinate format used by CAD tools.
    /// </summary>
    private Vector2 GetScreenPosition(MouseEventArgs e)
    {
        Point mousePosition = e.GetPosition(Viewport);
        return new Vector2((float)mousePosition.X, (float)mousePosition.Y);
    }
}
