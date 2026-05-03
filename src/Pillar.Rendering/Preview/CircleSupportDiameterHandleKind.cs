// CircleSupportDiameterHandleKind.cs
// Identifies transient Circle Support diameter handles used by preview editing without adding document entities.
namespace Pillar.Rendering.Preview;

/// <summary>
/// Identifies which Circle Support diameter handle is being inspected or dragged.
/// </summary>
public enum CircleSupportDiameterHandleKind
{
    /// <summary>
    /// No Circle Support diameter handle is active.
    /// </summary>
    None = 0,

    /// <summary>
    /// The handle for the first diameter point.
    /// </summary>
    FirstPoint = 1,

    /// <summary>
    /// The handle for the second diameter point.
    /// </summary>
    SecondPoint = 2
}
