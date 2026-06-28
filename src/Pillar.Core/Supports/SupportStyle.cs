// SupportStyle.cs
// Defines style-specific support dimension rules without involving rendering code.
using System;

namespace Pillar.Core.Supports;

/// <summary>
/// Base class for style-specific support dimension behavior.
/// </summary>
public abstract class SupportStyle
{
    /// <summary>
    /// Gets a reusable individual support style instance.
    /// </summary>
    public static IndividualSupportStyle Individual { get; } = new IndividualSupportStyle();

    /// <summary>
    /// Gets the style kind used for persistence and conversion decisions.
    /// </summary>
    public abstract SupportStyleKind Kind { get; }

    /// <summary>
    /// Creates a defensive copy for support entity ownership.
    /// </summary>
    public abstract SupportStyle Clone();

    /// <summary>
    /// Resolves the branch diameter for this style from the supplied base profile.
    /// </summary>
    internal abstract float ResolveBranchDiameter(SupportProfile profile);
}

/// <summary>
/// Style for normal standalone supports.
/// </summary>
public sealed class IndividualSupportStyle : SupportStyle
{
    /// <summary>
    /// Gets the style kind used for persistence and conversion decisions.
    /// </summary>
    public override SupportStyleKind Kind
    {
        get { return SupportStyleKind.Individual; }
    }

    /// <summary>
    /// Creates a defensive copy for support entity ownership.
    /// </summary>
    public override SupportStyle Clone()
    {
        return new IndividualSupportStyle();
    }

    /// <summary>
    /// Individual branches inherit the top stem diameter.
    /// </summary>
    internal override float ResolveBranchDiameter(SupportProfile profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        return profile.StemTopDiameter;
    }
}

/// <summary>
/// Style for supports converted into members of a shared-stem cluster.
/// </summary>
public sealed class ClusteredSupportStyle : SupportStyle
{
    /// <summary>
    /// Creates a clustered style with an explicit branch diameter and profile-driven central stem diameters.
    /// </summary>
    public ClusteredSupportStyle(float branchDiameter)
        : this(null, null, branchDiameter)
    {
    }

    /// <summary>
    /// Creates a clustered style with explicit central stem and branch diameters.
    /// </summary>
    public ClusteredSupportStyle(float centralStemBottomDiameter, float centralStemTopDiameter, float branchDiameter)
        : this((float?)centralStemBottomDiameter, (float?)centralStemTopDiameter, branchDiameter)
    {
    }

    /// <summary>
    /// Creates a clustered style while allowing old documents to fall back to profile stem diameters.
    /// </summary>
    internal ClusteredSupportStyle(float? centralStemBottomDiameter, float? centralStemTopDiameter, float branchDiameter)
    {
        if (centralStemBottomDiameter.HasValue != centralStemTopDiameter.HasValue)
        {
            throw new ArgumentException("Clustered support styles must provide both central stem diameters or neither.");
        }

        CentralStemBottomDiameter = centralStemBottomDiameter.HasValue
            ? ValidateDiameter(centralStemBottomDiameter.Value, nameof(centralStemBottomDiameter))
            : null;
        CentralStemTopDiameter = centralStemTopDiameter.HasValue
            ? ValidateDiameter(centralStemTopDiameter.Value, nameof(centralStemTopDiameter))
            : null;
        BranchDiameter = ValidateDiameter(branchDiameter, nameof(branchDiameter));
    }

    /// <summary>
    /// Gets the explicit central stem bottom diameter, or null for profile-driven compatibility.
    /// </summary>
    public float? CentralStemBottomDiameter { get; }

    /// <summary>
    /// Gets the explicit central stem top diameter, or null for profile-driven compatibility.
    /// </summary>
    public float? CentralStemTopDiameter { get; }

    /// <summary>
    /// Gets the explicit branch diameter used between the cluster stem and original head.
    /// </summary>
    public float BranchDiameter { get; }

    /// <summary>
    /// Gets the style kind used for persistence and conversion decisions.
    /// </summary>
    public override SupportStyleKind Kind
    {
        get { return SupportStyleKind.Clustered; }
    }

    /// <summary>
    /// Creates a defensive copy for support entity ownership.
    /// </summary>
    public override SupportStyle Clone()
    {
        return new ClusteredSupportStyle(CentralStemBottomDiameter, CentralStemTopDiameter, BranchDiameter);
    }

    /// <summary>
    /// Clustered branches use the explicit cluster branch diameter.
    /// </summary>
    internal override float ResolveBranchDiameter(SupportProfile profile)
    {
        _ = profile;
        return BranchDiameter;
    }

    /// <summary>
    /// Rejects invalid style dimensions before they reach support geometry.
    /// </summary>
    private static float ValidateDiameter(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Support style diameters must be finite positive values.");
        }

        return value;
    }
}