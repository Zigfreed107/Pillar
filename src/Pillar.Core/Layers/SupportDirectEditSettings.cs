// SupportDirectEditSettings.cs
// Stores one durable direct-edit transition without depending on viewport or rendering types.
using System;
using System.Numerics;

namespace Pillar.Core.Layers;

/// <summary>
/// Describes the original and edited shared-stem geometry produced by one Direct Edit gesture.
/// </summary>
public sealed class SupportDirectEditSettings
{
    /// <summary>
    /// Creates settings whose original geometry is the same as its edited geometry.
    /// </summary>
    public SupportDirectEditSettings(Vector3 basePosition, float stemTopZ)
        : this(basePosition, stemTopZ, basePosition, stemTopZ)
    {
    }

    /// <summary>
    /// Creates validated original and edited geometry settings.
    /// </summary>
    public SupportDirectEditSettings(
        Vector3 basePosition,
        float stemTopZ,
        Vector3 originalBasePosition,
        float originalStemTopZ)
    {
        ValidateGeometry(basePosition, stemTopZ, nameof(basePosition), nameof(stemTopZ));
        ValidateGeometry(originalBasePosition, originalStemTopZ, nameof(originalBasePosition), nameof(originalStemTopZ));
        BasePosition = basePosition;
        StemTopZ = stemTopZ;
        OriginalBasePosition = originalBasePosition;
        OriginalStemTopZ = originalStemTopZ;
    }

    /// <summary>
    /// Gets the edited stem base position.
    /// </summary>
    public Vector3 BasePosition { get; }

    /// <summary>
    /// Gets the edited height of the shared stem joint.
    /// </summary>
    public float StemTopZ { get; }

    /// <summary>
    /// Gets the pre-gesture stem base used to reverse or remove this modifier.
    /// </summary>
    public Vector3 OriginalBasePosition { get; }

    /// <summary>
    /// Gets the pre-gesture stem top used to reverse or remove this modifier.
    /// </summary>
    public float OriginalStemTopZ { get; }

    /// <summary>
    /// Creates a defensive copy for document and undo ownership.
    /// </summary>
    public SupportDirectEditSettings Clone()
    {
        return new SupportDirectEditSettings(BasePosition, StemTopZ, OriginalBasePosition, OriginalStemTopZ);
    }

    /// <summary>
    /// Validates one base and stem-top pair.
    /// </summary>
    private static void ValidateGeometry(Vector3 basePosition, float stemTopZ, string baseParameterName, string topParameterName)
    {
        if (!float.IsFinite(basePosition.X) || !float.IsFinite(basePosition.Y) || !float.IsFinite(basePosition.Z))
        {
            throw new ArgumentException("A direct-edit base position must be finite.", baseParameterName);
        }

        if (!float.IsFinite(stemTopZ) || stemTopZ <= basePosition.Z)
        {
            throw new ArgumentOutOfRangeException(topParameterName, "A direct-edit stem top must be finite and above its base.");
        }
    }
}
