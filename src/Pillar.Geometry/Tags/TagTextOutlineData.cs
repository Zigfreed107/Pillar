// TagTextOutlineData.cs
// Carries platform-neutral flattened glyph contours and their measured line width.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Pillar.Geometry.Tags;

/// <summary>
/// Represents centered two-dimensional text outlines ready for triangulation.
/// </summary>
public sealed class TagTextOutlineData
{
    /// <summary>
    /// Creates an immutable snapshot of measured text and closed glyph contours.
    /// </summary>
    public TagTextOutlineData(float measuredWidth, IReadOnlyList<IReadOnlyList<Vector2>> contours)
    {
        if (!float.IsFinite(measuredWidth) || measuredWidth < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(measuredWidth));
        }

        if (contours == null)
        {
            throw new ArgumentNullException(nameof(contours));
        }

        List<IReadOnlyList<Vector2>> copiedContours = new List<IReadOnlyList<Vector2>>(contours.Count);

        for (int contourIndex = 0; contourIndex < contours.Count; contourIndex++)
        {
            IReadOnlyList<Vector2> contour = contours[contourIndex]
                ?? throw new ArgumentException("A text contour cannot be null.", nameof(contours));
            copiedContours.Add(new ReadOnlyCollection<Vector2>(new List<Vector2>(contour)));
        }

        MeasuredWidth = measuredWidth;
        Contours = new ReadOnlyCollection<IReadOnlyList<Vector2>>(copiedContours);
    }

    public float MeasuredWidth { get; }
    public IReadOnlyList<IReadOnlyList<Vector2>> Contours { get; }
}
