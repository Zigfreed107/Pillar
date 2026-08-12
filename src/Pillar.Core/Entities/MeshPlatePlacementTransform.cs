// MeshPlatePlacementTransform.cs
// Contains renderer-agnostic transform math for placing an imported model's lowest vertex on the build plate.
using System;
using System.Numerics;

namespace Pillar.Core.Entities;

/// <summary>
/// Provides CAD-domain helpers for moving imported meshes vertically onto the build plate.
/// </summary>
public static class MeshPlatePlacementTransform
{
    /// <summary>
    /// Creates a user transform that changes only Z translation so the lowest transformed vertex is at Z zero.
    /// </summary>
    public static Transform3DData CreateUserTransformForMoveToPlate(MeshEntity mesh)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        Matrix4x4 worldTransform = mesh.WorldTransform;
        float lowestWorldZ = Vector3.Transform(mesh.Vertices[0], worldTransform).Z;

        for (int i = 1; i < mesh.Vertices.Count; i++)
        {
            float worldZ = Vector3.Transform(mesh.Vertices[i], worldTransform).Z;
            lowestWorldZ = MathF.Min(lowestWorldZ, worldZ);
        }

        Transform3DData currentTransform = mesh.UserTransform;
        Vector3 translation = currentTransform.Translation;
        translation.Z -= lowestWorldZ;

        return new Transform3DData(translation, currentTransform.Rotation, currentTransform.Scale);
    }
}
