// SupportDefaults.cs
// Centralizes the current hard-coded default support geometry used when tools create new supports.
namespace Pillar.Core.Supports;

/// <summary>
/// Provides the initial hard-coded support defaults used by v1 support creation tools.
/// </summary>
public static class SupportDefaults
{
    public const float DefaultTipDiameter = 0.35f;
    public const float DefaultTipLength = 5.20f;
    public const float DefaultBodyDiameter = 1.00f;
    public const float DefaultBaseDiameter = 2.50f;
    public const float DefaultBaseHeight = 1.50f;

    /// <summary>
    /// Gets a fresh copy of the default support profile so callers cannot mutate shared state.
    /// </summary>
    public static SupportProfile CreateProfile()
    {
        return CreateProfile(DefaultBodyDiameter);
    }

    /// <summary>
    /// Gets a fresh support profile using the requested body diameter as the user-facing support thickness.
    /// </summary>
    public static SupportProfile CreateProfile(float bodyDiameter)
    {
        return new SupportProfile(
            tipDiameter: DefaultTipDiameter,
            tipLength: DefaultTipLength,
            bodyDiameter: bodyDiameter,
            baseDiameter: DefaultBaseDiameter,
            baseHeight: DefaultBaseHeight);
    }
}
