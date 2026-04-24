// SupportDefaults.cs
// Centralizes the current hard-coded default support geometry used when tools create new supports.
namespace Pillar.Core.Supports;

/// <summary>
/// Provides the initial hard-coded support defaults used by v1 support creation tools.
/// </summary>
public static class SupportDefaults
{
    /// <summary>
    /// Gets a fresh copy of the default support profile so callers cannot mutate shared state.
    /// </summary>
    public static SupportProfile CreateProfile()
    {
        return new SupportProfile(
            tipDiameter: 0.35f,
            tipLength: 5.20f,
            bodyDiameter: 1.00f,
            baseDiameter: 2.50f,
            baseHeight: 1.50f);
    }
}
