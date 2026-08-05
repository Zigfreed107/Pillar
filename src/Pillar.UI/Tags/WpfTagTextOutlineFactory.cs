// WpfTagTextOutlineFactory.cs
// Shapes installed-font text with WPF and flattens glyph curves into renderer-neutral contours.
using Pillar.Core.Tags;
using Pillar.Geometry.Tags;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Media;

namespace Pillar.UI.Tags;

/// <summary>
/// Converts one selected installed font into centered millimetre-scale glyph outlines.
/// </summary>
public static class WpfTagTextOutlineFactory
{
    private const double MeasurementEmSize = 100.0;
    private const double FlatteningTolerance = 0.25;
    private const float DuplicatePointToleranceSquared = 0.00000001f;
    private static readonly IReadOnlyDictionary<string, FontFamily> InstalledFonts = CreateInstalledFontMap();

    /// <summary>
    /// Shapes and flattens one line of tag text, using Arial when its requested font is unavailable.
    /// </summary>
    public static TagTextOutlineData Create(TagSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        return Create(
            settings.Text,
            settings.FontFamilyName,
            settings.FontSize,
            TagSettings.DefaultFontFamilyName);
    }

    /// <summary>
    /// Shapes generic printable text while retaining the Tag tool's tested outline path.
    /// </summary>
    public static TagTextOutlineData Create(
        string text,
        string fontFamilyName,
        float fontSize,
        string fallbackFontFamilyName)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new TagTextOutlineData(0.0f, Array.Empty<IReadOnlyList<Vector2>>());
        }

        FontFamily fontFamily = ResolveFontFamily(fontFamilyName, fallbackFontFamilyName);
        Typeface typeface = new Typeface(
            fontFamily,
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);
        FormattedText formattedText = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            MeasurementEmSize,
            Brushes.Black,
            1.0);

        if (formattedText.Height <= 0.0)
        {
            return new TagTextOutlineData(0.0f, Array.Empty<IReadOnlyList<Vector2>>());
        }

        System.Windows.Media.Geometry geometry = formattedText.BuildGeometry(new Point(0.0, 0.0));
        Rect bounds = geometry.Bounds;
        float scale = fontSize / (float)formattedText.Height;
        float measuredWidth = bounds.IsEmpty ? 0.0f : (float)bounds.Width * scale;

        if (bounds.IsEmpty)
        {
            return new TagTextOutlineData(measuredWidth, Array.Empty<IReadOnlyList<Vector2>>());
        }

        PathGeometry flattened = geometry.GetFlattenedPathGeometry(
            FlatteningTolerance,
            ToleranceType.Absolute);
        double centerX = bounds.Left + bounds.Width * 0.5;
        double centerY = bounds.Top + bounds.Height * 0.5;
        List<IReadOnlyList<Vector2>> contours = new List<IReadOnlyList<Vector2>>(flattened.Figures.Count);

        for (int figureIndex = 0; figureIndex < flattened.Figures.Count; figureIndex++)
        {
            PathFigure figure = flattened.Figures[figureIndex];

            if (!figure.IsClosed)
            {
                continue;
            }

            List<Vector2> contour = ExtractContour(figure, centerX, centerY, scale);

            if (contour.Count >= 3)
            {
                contours.Add(contour);
            }
        }

        return new TagTextOutlineData(measuredWidth, contours);
    }

    /// <summary>
    /// Copies one flattened WPF path figure while converting Y-down DIPs to centered Y-up millimetres.
    /// </summary>
    private static List<Vector2> ExtractContour(
        PathFigure figure,
        double centerX,
        double centerY,
        float scale)
    {
        List<Vector2> contour = new List<Vector2>();
        AddPoint(contour, figure.StartPoint, centerX, centerY, scale);

        for (int segmentIndex = 0; segmentIndex < figure.Segments.Count; segmentIndex++)
        {
            PathSegment segment = figure.Segments[segmentIndex];

            if (segment is PolyLineSegment polyLine)
            {
                for (int pointIndex = 0; pointIndex < polyLine.Points.Count; pointIndex++)
                {
                    AddPoint(contour, polyLine.Points[pointIndex], centerX, centerY, scale);
                }
            }
            else if (segment is LineSegment line)
            {
                AddPoint(contour, line.Point, centerX, centerY, scale);
            }
        }

        if (contour.Count > 1
            && Vector2.DistanceSquared(contour[0], contour[^1]) <= DuplicatePointToleranceSquared)
        {
            contour.RemoveAt(contour.Count - 1);
        }

        return contour;
    }

    /// <summary>
    /// Adds one transformed point unless it duplicates the prior flattened vertex.
    /// </summary>
    private static void AddPoint(
        List<Vector2> contour,
        Point point,
        double centerX,
        double centerY,
        float scale)
    {
        Vector2 transformed = new Vector2(
            (float)(point.X - centerX) * scale,
            (float)(centerY - point.Y) * scale);

        if (contour.Count == 0
            || Vector2.DistanceSquared(contour[^1], transformed) > DuplicatePointToleranceSquared)
        {
            contour.Add(transformed);
        }
    }

    /// <summary>
    /// Resolves the requested installed family and applies the required Arial fallback.
    /// </summary>
    private static FontFamily ResolveFontFamily(string requestedName, string fallbackName)
    {
        if (InstalledFonts.TryGetValue(requestedName, out FontFamily? requested))
        {
            return requested;
        }

        if (InstalledFonts.TryGetValue(fallbackName, out FontFamily? fallback))
        {
            return fallback;
        }

        return InstalledFonts.Values.FirstOrDefault()
            ?? new FontFamily(fallbackName);
    }

    /// <summary>
    /// Captures installed families once so interactive option updates avoid repeated font enumeration.
    /// </summary>
    private static IReadOnlyDictionary<string, FontFamily> CreateInstalledFontMap()
    {
        Dictionary<string, FontFamily> fonts = new Dictionary<string, FontFamily>(StringComparer.OrdinalIgnoreCase);

        foreach (FontFamily family in Fonts.SystemFontFamilies)
        {
            if (!fonts.ContainsKey(family.Source))
            {
                fonts.Add(family.Source, family);
            }
        }

        return fonts;
    }
}
