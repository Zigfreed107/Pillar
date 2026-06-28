// SupportClusterStemSizingMode.cs
// Defines how clustered support shared-stem diameters are chosen from saved clustering settings.
namespace Pillar.Core.Layers;

/// <summary>
/// Identifies whether cluster stems use calculated diameters or explicit user-entered diameters.
/// </summary>
public enum SupportClusterStemSizingMode
{
    Automatic,
    Manual
}
