// RingSupportSurfaceTargetMode.cs
// Identifies how Ring Support guide points choose target surfaces that overlap in XY.
namespace Pillar.Core.Layers;

/// <summary>
/// Selects the surface-targeting policy used by generated Ring Supports.
/// </summary>
public enum RingSupportSurfaceTargetMode
{
    /// <summary>
    /// Uses the first exterior surface reachable from the build plate.
    /// </summary>
    FirstReachable = 0,

    /// <summary>
    /// Uses only accepted faces and chooses the one nearest to each 3D ring guide point.
    /// </summary>
    SelectedFacesOnly = 1
}
