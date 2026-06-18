// AreaSupportFillMode.cs
// Defines the persistent interior distribution strategy used by generated Area Support groups.
namespace Pillar.Core.Layers;

/// <summary>
/// Selects how Area Support distributes supports after its source face boundaries are extracted.
/// </summary>
public enum AreaSupportFillMode
{
    HexGrid = 0,
    BoundaryOffsets = 1
}
