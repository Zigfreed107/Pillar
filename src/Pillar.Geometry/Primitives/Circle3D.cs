// Circle3D.cs
// Defines renderer-agnostic 3D circle construction used by tools and previews.
using System;
using System.Numerics;

namespace Pillar.Geometry.Primitives;

/// <summary>
/// Represents a stable 3D circle with an orthonormal in-plane basis.
/// </summary>
public readonly struct Circle3D
{
    /// <summary>
    /// Creates a circle from explicit center, radius, and local basis vectors.
    /// </summary>
    public Circle3D(Vector3 center, float radius, Vector3 normal, Vector3 axisU, Vector3 axisV)
    {
        Center = center;
        Radius = radius;
        Normal = normal;
        AxisU = axisU;
        AxisV = axisV;
    }

    /// <summary>
    /// Gets the circle center in world coordinates.
    /// </summary>
    public Vector3 Center { get; }

    /// <summary>
    /// Gets the circle radius in world units.
    /// </summary>
    public float Radius { get; }

    /// <summary>
    /// Gets the normalized circle plane normal.
    /// </summary>
    public Vector3 Normal { get; }

    /// <summary>
    /// Gets the first normalized axis inside the circle plane.
    /// </summary>
    public Vector3 AxisU { get; }

    /// <summary>
    /// Gets the second normalized axis inside the circle plane.
    /// </summary>
    public Vector3 AxisV { get; }

    /// <summary>
    /// Gets the circumference used to convert desired spacing into support count.
    /// </summary>
    public float Circumference
    {
        get { return 2.0f * MathF.PI * Radius; }
    }

    /// <summary>
    /// Calculates one point on the circle for an angle in radians.
    /// </summary>
    public Vector3 GetPoint(float angleRadians)
    {
        return Center + (AxisU * MathF.Cos(angleRadians) + AxisV * MathF.Sin(angleRadians)) * Radius;
    }

    /// <summary>
    /// Builds a circle from three circumference points and rejects unstable duplicate or collinear picks.
    /// </summary>
    public static bool TryCreateFromThreePoints(Vector3 firstPoint, Vector3 secondPoint, Vector3 thirdPoint, out Circle3D circle)
    {
        const float MinimumDistanceSquared = 0.000001f;
        const float MinimumNormalLengthSquared = 0.000001f;

        Vector3 firstToSecond = secondPoint - firstPoint;
        Vector3 firstToThird = thirdPoint - firstPoint;

        if (firstToSecond.LengthSquared() <= MinimumDistanceSquared
            || firstToThird.LengthSquared() <= MinimumDistanceSquared
            || Vector3.DistanceSquared(secondPoint, thirdPoint) <= MinimumDistanceSquared)
        {
            circle = default;
            return false;
        }

        Vector3 planeNormal = Vector3.Cross(firstToSecond, firstToThird);
        float normalLengthSquared = planeNormal.LengthSquared();

        if (normalLengthSquared <= MinimumNormalLengthSquared)
        {
            circle = default;
            return false;
        }

        Vector3 centerOffset =
            ((firstToSecond.LengthSquared() * Vector3.Cross(firstToThird, planeNormal))
            + (firstToThird.LengthSquared() * Vector3.Cross(planeNormal, firstToSecond)))
            / (2.0f * normalLengthSquared);

        Vector3 center = firstPoint + centerOffset;
        Vector3 axisU = Vector3.Normalize(firstPoint - center);
        Vector3 normal = Vector3.Normalize(planeNormal);
        Vector3 axisV = Vector3.Normalize(Vector3.Cross(normal, axisU));
        float radius = Vector3.Distance(center, firstPoint);

        if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0.0f)
        {
            circle = default;
            return false;
        }

        circle = new Circle3D(center, radius, normal, axisU, axisV);
        return true;
    }

    /// <summary>
    /// Builds a provisional circle from two diameter endpoints while the final third point is not known yet.
    /// </summary>
    public static bool TryCreateFromDiameter(Vector3 firstPoint, Vector3 secondPoint, out Circle3D circle)
    {
        const float MinimumDistanceSquared = 0.000001f;

        Vector3 diameter = secondPoint - firstPoint;

        if (diameter.LengthSquared() <= MinimumDistanceSquared)
        {
            circle = default;
            return false;
        }

        Vector3 center = (firstPoint + secondPoint) * 0.5f;
        float radius = diameter.Length() * 0.5f;
        Vector3 axisU = Vector3.Normalize(firstPoint - center);
        Vector3 axisV = Vector3.Cross(Vector3.UnitZ, axisU);

        if (axisV.LengthSquared() <= MinimumDistanceSquared)
        {
            axisV = Vector3.UnitX;
        }
        else
        {
            axisV = Vector3.Normalize(axisV);
        }

        Vector3 normal = Vector3.Normalize(Vector3.Cross(axisU, axisV));
        circle = new Circle3D(center, radius, normal, axisU, axisV);
        return true;
    }

    /// <summary>
    /// Builds a horizontal XY-plane circle from two picked diameter endpoints, preserving the first pick's height.
    /// </summary>
    public static bool TryCreateHorizontalFromDiameter(Vector3 firstPoint, Vector3 secondPoint, out Circle3D circle)
    {
        const float MinimumDistanceSquared = 0.000001f;

        Vector2 firstPointXY = new Vector2(firstPoint.X, firstPoint.Y);
        Vector2 secondPointXY = new Vector2(secondPoint.X, secondPoint.Y);
        Vector2 diameterXY = secondPointXY - firstPointXY;

        if (diameterXY.LengthSquared() <= MinimumDistanceSquared)
        {
            circle = default;
            return false;
        }

        Vector2 centerXY = (firstPointXY + secondPointXY) * 0.5f;
        float radius = diameterXY.Length() * 0.5f;
        Vector3 center = new Vector3(centerXY.X, centerXY.Y, firstPoint.Z);
        Vector3 axisU = Vector3.Normalize(new Vector3(firstPoint.X - center.X, firstPoint.Y - center.Y, 0.0f));
        Vector3 axisV = new Vector3(-axisU.Y, axisU.X, 0.0f);

        circle = new Circle3D(center, radius, Vector3.UnitZ, axisU, axisV);
        return true;
    }
}
