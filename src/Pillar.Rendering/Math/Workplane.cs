using System.Numerics;
using System.Windows;
using HelixToolkit.Wpf.SharpDX;

namespace Pillar.Rendering.Math;

public class Workplane
{
    public Vector3 Origin { get; set; } = Vector3.Zero;
    public Vector3 Normal { get; set; } = Vector3.UnitZ;

    public float DistanceToPoint(Vector3 point)
    {
        return Vector3.Dot(point - Origin, Normal);
    }

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

    public bool IntersectRay(Vector3 rayOrigin, Vector3 rayDirection, out Vector3 hit)
    {
        float denom = Vector3.Dot(Normal, rayDirection);

        if (System.Math.Abs(denom) < 0.0001f)
        {
            hit = Vector3.Zero;
            return false;
        }

        float t = Vector3.Dot(Origin - rayOrigin, Normal) / denom;

        hit = rayOrigin + rayDirection * t;
        return t >= 0;
    }
}
