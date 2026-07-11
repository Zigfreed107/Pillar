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

/// <summary>
/// Style for generated buttress supports whose branch joins another support without a model-contact head.
/// </summary>
public sealed class ButtressSupportStyle : SupportStyle
{
    /// <summary>
    /// Creates a headless buttress style with an explicit branch diameter.
    /// </summary>
    public ButtressSupportStyle(float branchDiameter)
    {
        BranchDiameter = ValidateDiameter(branchDiameter, nameof(branchDiameter));
    }

    /// <summary>
    /// Gets the branch diameter copied from the support being buttressed.
    /// </summary>
    public float BranchDiameter { get; }

    /// <summary>
    /// Gets the style kind used for persistence and geometry decisions.
    /// </summary>
    public override SupportStyleKind Kind
    {
        get { return SupportStyleKind.Buttress; }
    }

    /// <summary>
    /// Creates a defensive copy for support entity ownership.
    /// </summary>
    public override SupportStyle Clone()
    {
        return new ButtressSupportStyle(BranchDiameter);
    }

    /// <summary>
    /// Buttress branches use the copied source-support branch diameter.
    /// </summary>
    internal override float ResolveBranchDiameter(SupportProfile profile)
    {
        _ = profile;
        return BranchDiameter;
    }

    /// <summary>
    /// Rejects invalid branch diameters before they reach support geometry.
    /// </summary>
    private static float ValidateDiameter(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Buttress branch diameters must be finite positive values.");
        }

        return value;
    }
}

/// <summary>
/// Style for generated brace members that render as simple reinforcement cylinders.
/// </summary>
public sealed class BraceMemberSupportStyle : SupportStyle
{
    /// <summary>
    /// Creates a brace-member style with an explicit member diameter.
    /// </summary>
    public BraceMemberSupportStyle(float diameter)
    {
        Diameter = ValidateDiameter(diameter, nameof(diameter));
    }

    /// <summary>
    /// Gets the generated reinforcement member diameter.
    /// </summary>
    public float Diameter { get; }

    /// <summary>
    /// Gets the style kind used for persistence and conversion decisions.
    /// </summary>
    public override SupportStyleKind Kind
    {
        get { return SupportStyleKind.BraceMember; }
    }

    /// <summary>
    /// Creates a defensive copy for support entity ownership.
    /// </summary>
    public override SupportStyle Clone()
    {
        return new BraceMemberSupportStyle(Diameter);
    }

    /// <summary>
    /// Brace members use their explicit member diameter.
    /// </summary>
    internal override float ResolveBranchDiameter(SupportProfile profile)
    {
        _ = profile;
        return Diameter;
    }

    /// <summary>
    /// Rejects invalid style dimensions before they reach support geometry.
    /// </summary>
    private static float ValidateDiameter(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Brace member diameters must be finite positive values.");
        }

        return value;
    }
}