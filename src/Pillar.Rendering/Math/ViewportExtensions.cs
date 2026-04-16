using HelixToolkit.Wpf.SharpDX;
using SharpDX;
using System.Numerics;
using System.Windows;

namespace Pillar.Rendering.Math;

public static class ViewportExtensions
{
    public static bool GetMouseRay(this Viewport3DX viewport, Point mousePos,
        out Vector3 rayOrigin, out Vector3 rayDirection)
    {
        var ray = viewport.UnProject(mousePos);

        if (ray == null)
        {
            rayOrigin = Vector3.Zero;
            rayDirection = Vector3.Zero;
            return false;
        }

        rayOrigin = ray.Position;
        rayDirection = ray.Direction;

        return true;
    }
}