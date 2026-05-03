// RingSupportPointHandleKind.cs
// Identifies transient Ring Support point handles used by preview editing without adding document entities.
namespace Pillar.Rendering.Preview;

/// <summary>
/// Identifies which Ring Support circumference point handle is being inspected or dragged.
/// </summary>
public enum RingSupportPointHandleKind
{
    /// <summary>
    /// No Ring Support point handle is active.
    /// </summary>
    None = 0,

    /// <summary>
    /// The handle for the first circumference point.
    /// </summary>
    FirstPoint = 1,

    /// <summary>
    /// The handle for the second circumference point.
    /// </summary>
    SecondPoint = 2,

    /// <summary>
    /// The handle for the third circumference point.
    /// </summary>
    ThirdPoint = 3
}
