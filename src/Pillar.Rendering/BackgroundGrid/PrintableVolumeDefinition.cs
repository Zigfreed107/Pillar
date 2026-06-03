// PrintableVolumeDefinition.cs
// Stores the build-volume dimensions used by viewport background visuals without coupling application settings to rendering code.
using System;

namespace Pillar.Rendering.BackgroundGrid;

/// <summary>
/// Describes the printer build volume in model units, where X and Y define the bed and Z defines maximum print height.
/// </summary>
public sealed class PrintableVolumeDefinition
{
    /// <summary>
    /// Creates a validated printable-volume definition.
    /// </summary>
    public PrintableVolumeDefinition(float xDistance, float yDistance, float zDistance)
    {
        if (xDistance <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(xDistance), "Printable volume X distance must be greater than zero.");
        }

        if (yDistance <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(yDistance), "Printable volume Y distance must be greater than zero.");
        }

        if (zDistance <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(zDistance), "Printable volume Z distance must be greater than zero.");
        }

        XDistance = xDistance;
        YDistance = yDistance;
        ZDistance = zDistance;
    }

    /// <summary>
    /// Gets the printable width along the X axis.
    /// </summary>
    public float XDistance { get; }

    /// <summary>
    /// Gets the printable depth along the Y axis.
    /// </summary>
    public float YDistance { get; }

    /// <summary>
    /// Gets the printable height along the Z axis.
    /// </summary>
    public float ZDistance { get; }
}
