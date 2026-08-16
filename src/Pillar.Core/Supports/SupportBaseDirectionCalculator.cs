// SupportBaseDirectionCalculator.cs
// Converts upward model surface normals into constrained model-connected support base directions.
using System;
using System.Numerics;

namespace Pillar.Core.Supports;

/// <summary>
/// Provides renderer-independent orientation helpers for support bases attached to model surfaces.
/// </summary>
public static class SupportBaseDirectionCalculator
{
    private const float DirectionTolerance = 0.000001f;

    /// <summary>
    /// Points a fixed-length model base from its contact toward a requested vertical-stem position.
    /// </summary>
    public static Vector3 CreateDirectionTowardStem(
        Vector3 contactPosition,
        Vector2 requestedStemPosition,
        SupportProfile profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (!IsFinite(contactPosition)
            || !float.IsFinite(requestedStemPosition.X)
            || !float.IsFinite(requestedStemPosition.Y))
        {
            throw new ArgumentException("Model-base contact and stem positions must be finite.");
        }

        Vector2 horizontalOffset = requestedStemPosition - new Vector2(contactPosition.X, contactPosition.Y);
        float horizontalLength = horizontalOffset.Length();
        float maximumAngleRadians = profile.MaxModelBaseAngleFromVerticalDegrees * (MathF.PI / 180.0f);
        float maximumHorizontalLength = profile.ModelBaseHeight * MathF.Sin(maximumAngleRadians);

        if (horizontalLength > maximumHorizontalLength && horizontalLength > DirectionTolerance)
        {
            horizontalOffset *= maximumHorizontalLength / horizontalLength;
            horizontalLength = maximumHorizontalLength;
        }

        float horizontalRatio = Math.Clamp(horizontalLength / profile.ModelBaseHeight, 0.0f, 1.0f);
        float verticalRatio = MathF.Max(
            MathF.Sqrt(MathF.Max(0.0f, 1.0f - (horizontalRatio * horizontalRatio))),
            DirectionTolerance);
        Vector2 horizontalDirection = horizontalLength > DirectionTolerance
            ? horizontalOffset / horizontalLength
            : Vector2.Zero;
        return Vector3.Normalize(new Vector3(
            horizontalDirection.X * horizontalRatio,
            horizontalDirection.Y * horizontalRatio,
            verticalRatio));
    }

    /// <summary>
    /// Converts an upward-facing surface normal into a base direction constrained by the active profile.
    /// </summary>
    public static bool TryCreateDirectionFromSurfaceNormal(
        Vector3 surfaceNormal,
        SupportProfile profile,
        out Vector3 baseDirection)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        baseDirection = Vector3.UnitZ;

        if (!IsFinite(surfaceNormal) || surfaceNormal.LengthSquared() <= DirectionTolerance)
        {
            return false;
        }

        Vector3 normalizedSurfaceNormal = Vector3.Normalize(surfaceNormal);

        if (normalizedSurfaceNormal.Z <= DirectionTolerance)
        {
            return false;
        }

        baseDirection = ClampDirectionToProfile(normalizedSurfaceNormal, profile);
        return true;
    }

    /// <summary>
    /// Clamps an upward base direction to the profile's maximum angle from vertical.
    /// </summary>
    public static Vector3 ClampDirectionToProfile(Vector3 baseDirection, SupportProfile profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        Vector3 normalizedDirection = NormalizeOrDefault(baseDirection);

        if (normalizedDirection.Z < 0.0f)
        {
            normalizedDirection = -normalizedDirection;
        }

        float maximumAngleRadians = profile.MaxModelBaseAngleFromVerticalDegrees * (MathF.PI / 180.0f);
        float currentAngleRadians = MathF.Acos(Math.Clamp(normalizedDirection.Z, 0.0f, 1.0f));

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
        return Vector3.Normalize(
            (Vector3.UnitZ * MathF.Cos(maximumAngleRadians))
            + (horizontalDirection * MathF.Sin(maximumAngleRadians)));
    }

    /// <summary>
    /// Normalizes a finite direction or returns vertical for invalid input.
    /// </summary>
    private static Vector3 NormalizeOrDefault(Vector3 direction)
    {
        if (!IsFinite(direction) || direction.LengthSquared() <= DirectionTolerance)
        {
            return Vector3.UnitZ;
        }

        return Vector3.Normalize(direction);
    }

    /// <summary>
    /// Tests whether all direction components are finite.
    /// </summary>
    private static bool IsFinite(Vector3 direction)
    {
        return float.IsFinite(direction.X)
            && float.IsFinite(direction.Y)
            && float.IsFinite(direction.Z);
    }
}
