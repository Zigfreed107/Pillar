// Transform3DData.cs
// Defines a small renderer-agnostic transform value used to keep entity placement separate from immutable geometry data.
using System;
using System.Numerics;

namespace Pillar.Core.Entities;

/// <summary>
/// Represents one editable 3D transform as explicit translation, rotation, and scale components.
/// </summary>
public readonly struct Transform3DData : IEquatable<Transform3DData>
{
    /// <summary>
    /// Creates a validated transform value from explicit translation, rotation, and scale components.
    /// </summary>
    public Transform3DData(Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        ValidateTranslation(translation);
        ValidateRotation(rotation);
        ValidateScale(scale);

        Translation = translation;
        Rotation = Quaternion.Normalize(rotation);
        Scale = scale;
    }

    /// <summary>
    /// Gets the identity transform used when no placement override is required.
    /// </summary>
    public static Transform3DData Identity
    {
        get { return new Transform3DData(Vector3.Zero, Quaternion.Identity, Vector3.One); }
    }

    /// <summary>
    /// Gets the translation component in world units.
    /// </summary>
    public Vector3 Translation { get; }

    /// <summary>
    /// Gets the normalized rotation component.
    /// </summary>
    public Quaternion Rotation { get; }

    /// <summary>
    /// Gets the non-zero scale component.
    /// </summary>
    public Vector3 Scale { get; }

    /// <summary>
    /// Creates a pure translation transform.
    /// </summary>
    public static Transform3DData CreateTranslation(Vector3 translation)
    {
        return new Transform3DData(translation, Quaternion.Identity, Vector3.One);
    }

    /// <summary>
    /// Converts the explicit transform components into one matrix for rendering and bounds math.
    /// </summary>
    public Matrix4x4 ToMatrix4x4()
    {
        return Matrix4x4.CreateScale(Scale)
            * Matrix4x4.CreateFromQuaternion(Rotation)
            * Matrix4x4.CreateTranslation(Translation);
    }

    /// <summary>
    /// Returns true when this transform matches another transform component-for-component.
    /// </summary>
    public bool Equals(Transform3DData other)
    {
        return Translation.Equals(other.Translation)
            && Rotation.Equals(other.Rotation)
            && Scale.Equals(other.Scale);
    }

    /// <summary>
    /// Returns true when this transform matches another boxed transform value.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is Transform3DData other && Equals(other);
    }

    /// <summary>
    /// Returns a stable hash code for dictionary and set use.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(Translation, Rotation, Scale);
    }

    /// <summary>
    /// Compares two transforms for exact component equality.
    /// </summary>
    public static bool operator ==(Transform3DData left, Transform3DData right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Compares two transforms for component differences.
    /// </summary>
    public static bool operator !=(Transform3DData left, Transform3DData right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Rejects NaN and infinity from the translation component.
    /// </summary>
    private static void ValidateTranslation(Vector3 translation)
    {
        if (!IsFinite(translation.X) || !IsFinite(translation.Y) || !IsFinite(translation.Z))
        {
            throw new ArgumentException("Transform translation must contain only finite values.", nameof(translation));
        }
    }

    /// <summary>
    /// Rejects invalid quaternions before they reach rendering or persistence.
    /// </summary>
    private static void ValidateRotation(Quaternion rotation)
    {
        if (!IsFinite(rotation.X) || !IsFinite(rotation.Y) || !IsFinite(rotation.Z) || !IsFinite(rotation.W))
        {
            throw new ArgumentException("Transform rotation must contain only finite values.", nameof(rotation));
        }

        if (rotation.LengthSquared() <= float.Epsilon)
        {
            throw new ArgumentException("Transform rotation must have non-zero length.", nameof(rotation));
        }
    }

    /// <summary>
    /// Rejects invalid or collapsed scale values that would produce unusable transforms.
    /// </summary>
    private static void ValidateScale(Vector3 scale)
    {
        if (!IsFinite(scale.X) || !IsFinite(scale.Y) || !IsFinite(scale.Z))
        {
            throw new ArgumentException("Transform scale must contain only finite values.", nameof(scale));
        }

        if (MathF.Abs(scale.X) <= float.Epsilon || MathF.Abs(scale.Y) <= float.Epsilon || MathF.Abs(scale.Z) <= float.Epsilon)
        {
            throw new ArgumentException("Transform scale cannot contain zero components.", nameof(scale));
        }
    }

    /// <summary>
    /// Returns true when one scalar can safely participate in transform math.
    /// </summary>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
