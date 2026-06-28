// SupportClusterModifierSettings.cs
// Stores renderer-independent parameters for replaying a support clustering modifier.
using Pillar.Core.Supports;
using System;

namespace Pillar.Core.Layers;

/// <summary>
/// Describes the saved clustering parameters used when evaluating a Cluster support modifier.
/// </summary>
public sealed class SupportClusterModifierSettings
{
    public const float DefaultMaximumClusterRadius = 4.0f;
    public const int DefaultMinimumSupportsPerCluster = 2;
    public const int DefaultMaximumSupportsPerCluster = 6;
    public const float MinimumCentralStemDiameter = 0.25f;
    public const float MaximumCentralStemDiameter = 4.0f;

    /// <summary>
    /// Creates a validated clustering settings snapshot.
    /// </summary>
    public SupportClusterModifierSettings(
        float maximumClusterRadius,
        int minimumSupportsPerCluster,
        int maximumSupportsPerCluster,
        float maximumBranchAngleFromVerticalDegrees,
        SupportClusterStemSizingMode stemSizingMode,
        float manualCentralStemBottomDiameter,
        float manualCentralStemTopDiameter,
        float clusterBranchDiameter)
    {
        MaximumClusterRadius = ValidatePositiveFloat(maximumClusterRadius, nameof(maximumClusterRadius));
        MinimumSupportsPerCluster = ValidateMinimumSupports(minimumSupportsPerCluster, nameof(minimumSupportsPerCluster));
        MaximumSupportsPerCluster = ValidateMaximumSupports(maximumSupportsPerCluster, MinimumSupportsPerCluster, nameof(maximumSupportsPerCluster));
        MaximumBranchAngleFromVerticalDegrees = ValidateBranchAngle(maximumBranchAngleFromVerticalDegrees, nameof(maximumBranchAngleFromVerticalDegrees));
        StemSizingMode = stemSizingMode;
        ManualCentralStemBottomDiameter = ValidateStemDiameter(manualCentralStemBottomDiameter, nameof(manualCentralStemBottomDiameter));
        ManualCentralStemTopDiameter = ValidateStemDiameter(manualCentralStemTopDiameter, nameof(manualCentralStemTopDiameter));
        ClusterBranchDiameter = ValidateStemDiameter(clusterBranchDiameter, nameof(clusterBranchDiameter));
    }

    /// <summary>
    /// Gets a default settings set suitable for opening a new Cluster tool session.
    /// </summary>
    public static SupportClusterModifierSettings CreateDefault()
    {
        return new SupportClusterModifierSettings(
            DefaultMaximumClusterRadius,
            DefaultMinimumSupportsPerCluster,
            DefaultMaximumSupportsPerCluster,
            SupportDefaults.DefaultBranchAngleFromVerticalDegrees,
            SupportClusterStemSizingMode.Automatic,
            SupportDefaults.DefaultStemBottomDiameter,
            SupportDefaults.DefaultStemTopDiameter,
            SupportDefaults.DefaultStemTopDiameter);
    }

    /// <summary>
    /// Gets the maximum horizontal distance from the shared stem axis to any clustered head joint.
    /// </summary>
    public float MaximumClusterRadius { get; }

    /// <summary>
    /// Gets the smallest accepted member count for a generated cluster.
    /// </summary>
    public int MinimumSupportsPerCluster { get; }

    /// <summary>
    /// Gets the largest accepted member count for a generated cluster.
    /// </summary>
    public int MaximumSupportsPerCluster { get; }

    /// <summary>
    /// Gets the steepest allowed branch angle measured away from vertical.
    /// </summary>
    public float MaximumBranchAngleFromVerticalDegrees { get; }

    /// <summary>
    /// Gets whether shared-stem diameters are calculated or manually supplied.
    /// </summary>
    public SupportClusterStemSizingMode StemSizingMode { get; }

    /// <summary>
    /// Gets the manual shared-stem bottom diameter used when manual sizing is active.
    /// </summary>
    public float ManualCentralStemBottomDiameter { get; }

    /// <summary>
    /// Gets the manual shared-stem top diameter used when manual sizing is active.
    /// </summary>
    public float ManualCentralStemTopDiameter { get; }

    /// <summary>
    /// Gets the explicit branch diameter used for clustered support members.
    /// </summary>
    public float ClusterBranchDiameter { get; }

    /// <summary>
    /// Creates a defensive copy for document ownership and undo snapshots.
    /// </summary>
    public SupportClusterModifierSettings Clone()
    {
        return new SupportClusterModifierSettings(
            MaximumClusterRadius,
            MinimumSupportsPerCluster,
            MaximumSupportsPerCluster,
            MaximumBranchAngleFromVerticalDegrees,
            StemSizingMode,
            ManualCentralStemBottomDiameter,
            ManualCentralStemTopDiameter,
            ClusterBranchDiameter);
    }

    /// <summary>
    /// Rejects invalid positive floating-point settings before they reach document state.
    /// </summary>
    private static float ValidatePositiveFloat(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Cluster settings must use finite positive values.");
        }

        return value;
    }

    /// <summary>
    /// Rejects cluster sizes that cannot produce a meaningful shared stem.
    /// </summary>
    private static int ValidateMinimumSupports(int value, string parameterName)
    {
        if (value < 2)
        {
            throw new ArgumentOutOfRangeException(parameterName, "A support cluster must require at least two supports.");
        }

        return value;
    }

    /// <summary>
    /// Rejects maximum member counts that are smaller than the configured minimum.
    /// </summary>
    private static int ValidateMaximumSupports(int value, int minimumSupports, string parameterName)
    {
        if (value < minimumSupports)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Maximum supports per cluster must be at least the minimum supports per cluster.");
        }

        return value;
    }

    /// <summary>
    /// Rejects branch-angle values outside printable support bounds.
    /// </summary>
    private static float ValidateBranchAngle(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 15.0f || value > 45.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Cluster branch angles must be between 15 and 45 degrees from vertical.");
        }

        return value;
    }

    /// <summary>
    /// Rejects cluster diameters outside conservative printable bounds.
    /// </summary>
    private static float ValidateStemDiameter(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < MinimumCentralStemDiameter || value > MaximumCentralStemDiameter)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Cluster diameters are outside the supported printable range.");
        }

        return value;
    }
}
