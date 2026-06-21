// MeshRotationTransform.cs
// Contains renderer-agnostic rotation math for previewing and committing model rotation around a stable origin.
using System;
using System.Numerics;

namespace Pillar.Core.Entities;

/// <summary>
/// Provides CAD-domain helpers for rotating imported meshes around their transform origin.
/// </summary>
public static class MeshRotationTransform
{
    private const float DegreesToRadians = MathF.PI / 180.0f;

    /// <summary>
    /// Creates a user transform by applying X, Y, then Z rotations in the requested coordinate space.
    /// </summary>
    public static Transform3DData CreateUserTransformForRotation(
        Transform3DData originalTransform,
        Vector3 rotationDegrees,
        Vector3 importSpaceOrigin,
        RotationCoordinateSpace coordinateSpace)
    {
        ValidateRotationDegrees(rotationDegrees);

        if (rotationDegrees == Vector3.Zero)
        {
            return originalTransform;
        }

        Matrix4x4 originalRotationMatrix = Matrix4x4.CreateFromQuaternion(originalTransform.Rotation);
        Matrix4x4 deltaRotationMatrix = Matrix4x4.CreateRotationX(rotationDegrees.X * DegreesToRadians)
            * Matrix4x4.CreateRotationY(rotationDegrees.Y * DegreesToRadians)
            * Matrix4x4.CreateRotationZ(rotationDegrees.Z * DegreesToRadians);
        Matrix4x4 newRotationMatrix = coordinateSpace switch
        {
            RotationCoordinateSpace.World => originalRotationMatrix * deltaRotationMatrix,
            RotationCoordinateSpace.Local => deltaRotationMatrix * originalRotationMatrix,
            _ => throw new ArgumentOutOfRangeException(nameof(coordinateSpace), coordinateSpace, "Unknown rotation coordinate space.")
        };

        if (!Matrix4x4.Decompose(newRotationMatrix, out Vector3 ignoredScale, out Quaternion newRotation, out Vector3 ignoredTranslation))
        {
            throw new InvalidOperationException("The requested model rotation could not be decomposed into a stable transform.");
        }

        _ = ignoredScale;
        _ = ignoredTranslation;

        // Compensate translation so changing rotation does not move the selected pivot in world space.
        Vector3 scaledOrigin = importSpaceOrigin * originalTransform.Scale;
        Vector3 originalWorldOrigin = Vector3.Transform(scaledOrigin, originalRotationMatrix) + originalTransform.Translation;
        Vector3 rotatedOrigin = Vector3.Transform(scaledOrigin, newRotationMatrix);
        Vector3 newTranslation = originalWorldOrigin - rotatedOrigin;

        return new Transform3DData(newTranslation, newRotation, originalTransform.Scale);
    }

    /// <summary>
    /// Removes all user rotation while preserving scale and the pivot's current world-space position.
    /// </summary>
    public static Transform3DData CreateUserTransformForOriginalOrientation(
        Transform3DData currentTransform,
        Vector3 importSpaceOrigin)
    {
        if (currentTransform.Rotation == Quaternion.Identity)
        {
            return currentTransform;
        }

        Vector3 scaledOrigin = importSpaceOrigin * currentTransform.Scale;
        Matrix4x4 currentRotationMatrix = Matrix4x4.CreateFromQuaternion(currentTransform.Rotation);
        Vector3 currentWorldOrigin = Vector3.Transform(scaledOrigin, currentRotationMatrix) + currentTransform.Translation;
        Vector3 resetTranslation = currentWorldOrigin - scaledOrigin;

        return new Transform3DData(resetTranslation, Quaternion.Identity, currentTransform.Scale);
    }
    /// <summary>
    /// Calculates the world-space position of the stable model transform origin.
    /// </summary>
    public static Vector3 CalculateWorldOrigin(Transform3DData transform, Vector3 importSpaceOrigin)
    {
        return Vector3.Transform(importSpaceOrigin * transform.Scale, Matrix4x4.CreateFromQuaternion(transform.Rotation))
            + transform.Translation;
    }

    /// <summary>
    /// Rejects invalid numeric input before it reaches document or rendering state.
    /// </summary>
    private static void ValidateRotationDegrees(Vector3 rotationDegrees)
    {
        if (!IsFinite(rotationDegrees.X) || !IsFinite(rotationDegrees.Y) || !IsFinite(rotationDegrees.Z))
        {
            throw new ArgumentException("Rotation angles must contain only finite values.", nameof(rotationDegrees));
        }
    }

    /// <summary>
    /// Returns true when one angle can safely participate in transform math.
    /// </summary>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
