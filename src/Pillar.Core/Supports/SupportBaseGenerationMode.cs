// SupportBaseGenerationMode.cs
// Defines the user-selected surface preference and fallback order for support base placement.
namespace Pillar.Core.Supports;

/// <summary>
/// Identifies where support bases should be generated and the fallback order when both surfaces are allowed.
/// </summary>
public enum SupportBaseGenerationMode
{
    BuildPlateOnly = 0,
    ModelOnly = 1,
    BuildPlateThenModel = 2,
    ModelThenBuildPlate = 3
}
