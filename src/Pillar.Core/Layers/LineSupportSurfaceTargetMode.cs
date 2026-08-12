// LineSupportSurfaceTargetMode.cs
// Defines how Line Support guide points choose a target surface when several mesh heights share the same XY position.

namespace Pillar.Core.Layers;

/// <summary>
/// Selects the surface-targeting policy used while generating Line Support output.
/// </summary>
public enum LineSupportSurfaceTargetMode
{
    /// <summary>
    /// Chooses the nearest surface that can be reached by a valid support from the build plate.
    /// </summary>
    FirstReachable = 0,

    /// <summary>
    /// Chooses the surface nearest to the sampled 3D line point before validating support reachability.
    /// </summary>
    NearestToLine = 1,

    /// <summary>
    /// Chooses the nearest target only from the mesh faces explicitly selected for the Line Support group.
    /// </summary>
    SelectedFacesOnly = 2
}
