// SupportDefaults.cs
// Centralizes the current hard-coded default support geometry used when tools create new supports.
namespace Pillar.Core.Supports;

/// <summary>
/// Provides the initial hard-coded support defaults used by v1 support creation tools.
/// </summary>
public static class SupportDefaults
{
    public const float DefaultBaseBottomRadius = 1.25f;
    public const float DefaultBaseHeight = 1.50f;
    public const float DefaultStemBottomDiameter = 1.00f;
    public const float DefaultStemTopDiameter = 0.75f;
    public const float DefaultHeadHeight = 5.20f;
    public const float DefaultHeadPenetrationDepth = 0.30f;
    public const float DefaultHeadTopDiameter = 0.35f;

    /// <summary>
    /// Gets a fresh copy of the default support profile so callers cannot mutate shared state.
    /// </summary>
    public static SupportProfile CreateProfile()
    {
        return new SupportProfile(
            baseBottomRadius: DefaultBaseBottomRadius,
            baseHeight: DefaultBaseHeight,
            stemBottomDiameter: DefaultStemBottomDiameter,
            stemTopDiameter: DefaultStemTopDiameter,
            headHeight: DefaultHeadHeight,
            headPenetrationDepth: DefaultHeadPenetrationDepth,
            headTopDiameter: DefaultHeadTopDiameter);
    }
}
