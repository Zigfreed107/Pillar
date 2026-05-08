// MeshScaleTransform.cs
// Contains renderer-agnostic scale-origin and transform math for imported mesh models.
using System;
using System.Numerics;

namespace Pillar.Core.Entities;

/// <summary>
/// Provides CAD-domain helpers for scaling imported meshes around a stable model origin.
/// </summary>
public static class MeshScaleTransform
{
    /// <summary>
    /// Calculates the import-space scaling origin from the mesh vertex centroid and lowest model Z value.
    /// </summary>
    public static Vector3 CalculateImportSpaceOrigin(MeshEntity mesh)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        Matrix4x4 importPlacementMatrix = mesh.ImportPlacementTransform.ToMatrix4x4();
        Vector3 centroidAccumulator = Vector3.Zero;
        float minZ = float.PositiveInfinity;

        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            Vector3 importSpaceVertex = Vector3.Transform(mesh.Vertices[i], importPlacementMatrix);
            centroidAccumulator += importSpaceVertex;
            minZ = MathF.Min(minZ, importSpaceVertex.Z);
        }

        Vector3 centroid = centroidAccumulator / mesh.Vertices.Count;
        return new Vector3(centroid.X, centroid.Y, minZ);
    }

    /// <summary>
    /// Creates a user transform that applies the requested scale while keeping the supplied origin fixed.
    /// </summary>
    public static Transform3DData CreateUserTransformForScale(MeshEntity mesh, Vector3 scale, Vector3 importSpaceOrigin)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        Transform3DData currentTransform = mesh.UserTransform;
        Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(currentTransform.Rotation);
        Vector3 originAfterRotation = Vector3.Transform(importSpaceOrigin, rotationMatrix);
        Vector3 currentScaledOriginAfterRotation = Vector3.Transform(importSpaceOrigin * currentTransform.Scale, rotationMatrix);
        Vector3 baseTranslation = currentTransform.Translation - originAfterRotation + currentScaledOriginAfterRotation;
        Vector3 newScaledOriginAfterRotation = Vector3.Transform(importSpaceOrigin * scale, rotationMatrix);
        Vector3 newTranslation = baseTranslation + originAfterRotation - newScaledOriginAfterRotation;

        return new Transform3DData(newTranslation, currentTransform.Rotation, scale);
    }

    /// <summary>
    /// Calculates the current world-space location of the fixed scaling origin.
    /// </summary>
    public static Vector3 CalculateWorldOrigin(MeshEntity mesh, Vector3 importSpaceOrigin)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        Transform3DData currentTransform = mesh.UserTransform;
        Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(currentTransform.Rotation);
        Vector3 originAfterRotation = Vector3.Transform(importSpaceOrigin, rotationMatrix);
        Vector3 currentScaledOriginAfterRotation = Vector3.Transform(importSpaceOrigin * currentTransform.Scale, rotationMatrix);
        Vector3 baseTranslation = currentTransform.Translation - originAfterRotation + currentScaledOriginAfterRotation;

        return originAfterRotation + baseTranslation;
    }
}
