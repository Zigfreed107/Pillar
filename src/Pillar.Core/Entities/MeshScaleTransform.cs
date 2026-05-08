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
    /// Calculates the import-space scaling origin from the XY footprint bounds center and lowest model Z value.
    /// </summary>
    public static Vector3 CalculateImportSpaceOrigin(MeshEntity mesh)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        Matrix4x4 importPlacementMatrix = mesh.ImportPlacementTransform.ToMatrix4x4();
        Vector3 firstVertex = Vector3.Transform(mesh.Vertices[0], importPlacementMatrix);
        Vector3 min = firstVertex;
        Vector3 max = firstVertex;

        for (int i = 1; i < mesh.Vertices.Count; i++)
        {
            Vector3 importSpaceVertex = Vector3.Transform(mesh.Vertices[i], importPlacementMatrix);
            min = Vector3.Min(min, importSpaceVertex);
            max = Vector3.Max(max, importSpaceVertex);
        }

        float centerX = (min.X + max.X) / 2.0f;
        float centerY = (min.Y + max.Y) / 2.0f;
        return new Vector3(centerX, centerY, min.Z);
    }

    /// <summary>
    /// Calculates the original 100% model size along import-space X, Y, and Z axes.
    /// </summary>
    public static Vector3 CalculateImportSpaceSize(MeshEntity mesh)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        Matrix4x4 importPlacementMatrix = mesh.ImportPlacementTransform.ToMatrix4x4();
        Vector3 firstVertex = Vector3.Transform(mesh.Vertices[0], importPlacementMatrix);
        Vector3 min = firstVertex;
        Vector3 max = firstVertex;

        for (int i = 1; i < mesh.Vertices.Count; i++)
        {
            Vector3 importSpaceVertex = Vector3.Transform(mesh.Vertices[i], importPlacementMatrix);
            min = Vector3.Min(min, importSpaceVertex);
            max = Vector3.Max(max, importSpaceVertex);
        }

        return max - min;
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
