// SupportHeadDirectionCalculator.cs
// Converts mesh surface normals into stable support head directions without depending on rendering or viewport services.
using System;
using System.Numerics;

namespace Pillar.Core.Supports;

/// <summary>
/// Provides renderer-agnostic helpers for support head orientation and shifted-stem placement.
/// </summary>
public static class SupportHeadDirectionCalculator
{
    private const float DirectionTolerance = 0.000001f;

    /// <summary>
    /// Converts one surface normal into the clamped head direction from the head joint toward the model contact.
    /// </summary>
    public static Vector3 CreateHeadDirectionFromSurfaceNormal(Vector3 surfaceNormal, SupportProfile profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (profile.MaxHeadAngleFromVerticalDegrees <= 0.0f)
        {
            return Vector3.UnitZ;
        }

        Vector3 normalizedSurfaceNormal = NormalizeOrDefault(surfaceNormal, Vector3.UnitZ);
        Vector3 upwardCandidate = normalizedSurfaceNormal.Z < 0.0f
            ? -normalizedSurfaceNormal
            : normalizedSurfaceNormal;

        return ClampDirectionToProfile(upwardCandidate, profile);
    }

    /// <summary>
    /// Clamps an existing head direction to the profile angle limit.
    /// </summary>
    public static Vector3 ClampDirectionToProfile(Vector3 headDirection, SupportProfile profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (profile.MaxHeadAngleFromVerticalDegrees <= 0.0f)
        {
            return Vector3.UnitZ;
        }

        Vector3 normalizedDirection = NormalizeOrDefault(headDirection, Vector3.UnitZ);

        if (normalizedDirection.Z < 0.0f)
        {
            normalizedDirection = -normalizedDirection;
        }

        float clampedZ = Math.Clamp(normalizedDirection.Z, 0.0f, 1.0f);
        float currentAngleRadians = MathF.Acos(clampedZ);
        float maximumAngleRadians = DegreesToRadians(profile.MaxHeadAngleFromVerticalDegrees);

        if (currentAngleRadians <= maximumAngleRadians)
        {
            return normalizedDirection;
        }

        Vector3 horizontal = new Vector3(normalizedDirection.X, normalizedDirection.Y, 0.0f);

        if (horizontal.LengthSquared() <= DirectionTolerance)
        {
            return Vector3.UnitZ;
        }

        Vector3 horizontalDirection = Vector3.Normalize(horizontal);
        return Vector3.Normalize((Vector3.UnitZ * MathF.Cos(maximumAngleRadians)) + (horizontalDirection * MathF.Sin(maximumAngleRadians)));
    }

    /// <summary>
    /// Calculates the shifted build-plate base position below the angled head joint.
    /// </summary>
    public static Vector3 CreateShiftedBasePosition(Vector3 tipPosition, Vector3 headDirection, SupportProfile profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        Vector3 clampedHeadDirection = ClampDirectionToProfile(headDirection, profile);
        Vector3 headBottomPosition = tipPosition - (clampedHeadDirection * profile.HeadHeight);
        return new Vector3(headBottomPosition.X, headBottomPosition.Y, 0.0f);
    }

    /// <summary>
    /// Normalizes a direction, returning a stable fallback for invalid or zero vectors.
    /// </summary>
    private static Vector3 NormalizeOrDefault(Vector3 direction, Vector3 fallback)
    {
        if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || !float.IsFinite(direction.Z))
        {
            return fallback;
        }

        if (direction.LengthSquared() <= DirectionTolerance)
        {
            return fallback;
        }

        return Vector3.Normalize(direction);
    }

    /// <summary>
    /// Converts degrees into radians for trigonometric calculations.
    /// </summary>
    private static float DegreesToRadians(float degrees)
    {
        return degrees * (MathF.PI / 180.0f);
    }
}
