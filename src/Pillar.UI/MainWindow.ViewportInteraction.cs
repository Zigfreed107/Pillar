// MainWindow.ViewportInteraction.cs
// Hosts viewport input and camera-navigation glue so the shell can route WPF events into CAD tools without mixing that code with unrelated workflows.
using Pillar.Rendering.Math;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Tools;
using Pillar.UI.Modes;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace Pillar.UI;

public partial class MainWindow
{
    /// <summary>
    /// Handles viewport clicks by routing left-button input into the active CAD tool while Helix owns navigation gestures.
    /// </summary>
    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _ = sender;

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        Vector2 screenPosition = GetScreenPosition(e);

        if (IsCircleSupportOperationActive())
        {
            RunWithWaitCursor(() => _toolManager.ActiveTool?.OnMouseDown(screenPosition));
        }
        else
        {
            _toolManager.ActiveTool?.OnMouseDown(screenPosition);
        }

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

        _toolManager.ActiveTool?.OnMouseMove(GetScreenPosition(e));

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
    /// Gets whether left-click handling may trigger Circle Support hit testing or marker projection.
    /// </summary>
    private bool IsCircleSupportOperationActive()
    {
        return _activeModeId == WorkspaceModeId.ManualSupport
            && _manualSupportTool.ActiveOperationKind == ManualSupportOperationKind.Circle;
    }
}
