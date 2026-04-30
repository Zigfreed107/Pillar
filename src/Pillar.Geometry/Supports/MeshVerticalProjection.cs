// MeshVerticalProjection.cs
// Projects guide points onto mesh triangles along the build Z axis without depending on rendering objects.
using Pillar.Core.Entities;
using System;
using System.Numerics;

namespace Pillar.Geometry.Supports;

/// <summary>
/// Projects world-space guide points vertically onto transformed mesh triangles.
/// </summary>
public static class MeshVerticalProjection
{
    /// <summary>
    /// Finds the nearest intersection between a vertical Z line through a guide point and the supplied mesh.
    /// </summary>
    public static bool TryProjectToMesh(MeshEntity mesh, Vector3 guidePoint, out Vector3 projectedPoint)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        Matrix4x4 worldTransform = mesh.WorldTransform;
        float bestDistance = float.MaxValue;
        Vector3 bestPoint = Vector3.Zero;
        bool hasHit = false;

        for (int i = 0; i < mesh.TriangleIndices.Count; i += 3)
        {
            Vector3 a = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[i]], worldTransform);
            Vector3 b = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[i + 1]], worldTransform);
            Vector3 c = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[i + 2]], worldTransform);

            if (!TryIntersectVerticalLineWithTriangle(guidePoint.X, guidePoint.Y, a, b, c, out float z))
            {
                continue;
            }

            float distance = MathF.Abs(z - guidePoint.Z);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPoint = new Vector3(guidePoint.X, guidePoint.Y, z);
                hasHit = true;
            }
        }

        projectedPoint = bestPoint;
        return hasHit;
    }

    /// <summary>
    /// Intersects one vertical line with one triangle by solving barycentric coordinates in the XY projection.
    /// </summary>
    private static bool TryIntersectVerticalLineWithTriangle(float x, float y, Vector3 a, Vector3 b, Vector3 c, out float z)
    {
        const float Epsilon = 0.000001f;

        Vector2 p = new Vector2(x, y);
        Vector2 a2 = new Vector2(a.X, a.Y);
        Vector2 b2 = new Vector2(b.X, b.Y);
        Vector2 c2 = new Vector2(c.X, c.Y);
        Vector2 v0 = b2 - a2;
        Vector2 v1 = c2 - a2;
        Vector2 v2 = p - a2;
        float denominator = (v0.X * v1.Y) - (v1.X * v0.Y);

        if (MathF.Abs(denominator) <= Epsilon)
        {
            z = 0.0f;
            return false;
        }

        float u = ((v2.X * v1.Y) - (v1.X * v2.Y)) / denominator;
        float v = ((v0.X * v2.Y) - (v2.X * v0.Y)) / denominator;
        float w = 1.0f - u - v;

        if (u < -Epsilon || v < -Epsilon || w < -Epsilon)
        {
            z = 0.0f;
            return false;
        }

        z = (w * a.Z) + (u * b.Z) + (v * c.Z);
        return true;
    }
}
