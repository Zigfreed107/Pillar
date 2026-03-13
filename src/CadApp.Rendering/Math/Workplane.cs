using System.Numerics;
using System.Windows;
using HelixToolkit.Wpf.SharpDX;

namespace CadApp.Rendering.Math;

public static class Workplane
{
    public static bool TryGetPointOnPlane(
        Viewport3DX viewport,
        double x,
        double y,
        out Vector3 point)
    {
        var ray = viewport.UnProject(new Point(x, y));

        var rayOrigin = ray.Position;
        var rayDirection = ray.Direction;

        // XY plane (Z = 0)
        const float planeZ = 0f;

        if (System.Math.Abs(rayDirection.Z) < 0.0001f)
        {
            point = default;
            return false;
        }

        var t = (planeZ - rayOrigin.Z) / rayDirection.Z;
        var hit = rayOrigin + rayDirection * t;

        point = new Vector3(hit.X, hit.Y, hit.Z);

        return true;
    }
}
