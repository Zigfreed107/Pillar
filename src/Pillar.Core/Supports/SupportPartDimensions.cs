// SupportPartDimensions.cs
// Resolves profile and style data into concrete diameters for support mesh generation and bounds.
using System;

namespace Pillar.Core.Supports;

/// <summary>
/// Stores concrete support part diameters after style rules have been applied.
/// </summary>
public readonly struct SupportPartDimensions
{
    /// <summary>
    /// Creates one resolved support dimension set.
    /// </summary>
    public SupportPartDimensions(
        float stemBottomDiameter,
        float stemTopDiameter,
        float branchDiameter,
        float headBottomDiameter,
        float headTopDiameter)
    {
        StemBottomDiameter = stemBottomDiameter;
        StemTopDiameter = stemTopDiameter;
        BranchDiameter = branchDiameter;
        HeadBottomDiameter = headBottomDiameter;
        HeadTopDiameter = headTopDiameter;
    }

    /// <summary>
    /// Gets the stem diameter where the stem attaches to the base.
    /// </summary>
    public float StemBottomDiameter { get; }

    /// <summary>
    /// Gets the stem diameter where the stem meets the upper support section.
    /// </summary>
    public float StemTopDiameter { get; }

    /// <summary>
    /// Gets the branch diameter between the stem joint and head joint.
    /// </summary>
    public float BranchDiameter { get; }

    /// <summary>
    /// Gets the head diameter where the head attaches to the branch or stem.
    /// </summary>
    public float HeadBottomDiameter { get; }

    /// <summary>
    /// Gets the head diameter at the model intersection point.
    /// </summary>
    public float HeadTopDiameter { get; }
}

/// <summary>
/// Resolves support profiles and styles into concrete part dimensions.
/// </summary>
public static class SupportDimensionResolver
{
    /// <summary>
    /// Resolves all support part diameters for one profile and style pair.
    /// </summary>
    public static SupportPartDimensions Resolve(SupportProfile profile, SupportStyle? style)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        SupportStyle resolvedStyle = style ?? SupportStyle.Individual;
        float branchDiameter = resolvedStyle.ResolveBranchDiameter(profile);
        float stemBottomDiameter = profile.StemBottomDiameter;
        float stemTopDiameter = profile.StemTopDiameter;

        if (resolvedStyle is ClusteredSupportStyle clusteredStyle)
        {
            stemBottomDiameter = clusteredStyle.CentralStemBottomDiameter ?? stemBottomDiameter;
            stemTopDiameter = clusteredStyle.CentralStemTopDiameter ?? stemTopDiameter;
        }
        else if (resolvedStyle is ButtressSupportStyle buttressStyle)
        {
            branchDiameter = buttressStyle.BranchDiameter;
        }
        else if (resolvedStyle is BraceMemberSupportStyle braceMemberStyle)
        {
            stemBottomDiameter = braceMemberStyle.Diameter;
            stemTopDiameter = braceMemberStyle.Diameter;
            branchDiameter = braceMemberStyle.Diameter;
        }

        return new SupportPartDimensions(
            stemBottomDiameter,
            stemTopDiameter,
            branchDiameter,
            branchDiameter,
            resolvedStyle is BraceMemberSupportStyle ? branchDiameter : profile.HeadTopDiameter);
    }
}
