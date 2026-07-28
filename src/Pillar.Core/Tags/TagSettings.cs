// TagSettings.cs
// Stores validated, renderer-independent settings for one raft tag.
using System;

namespace Pillar.Core.Tags;

/// <summary>
/// Captures the editable body and printable text settings for one raft tag.
/// </summary>
public sealed class TagSettings
{
    public const float DefaultTagHeight = 0.7f;
    public const float DefaultEdgeAngleDegrees = 45.0f;
    public const float DefaultBorderOffset = 1.0f;
    public const float DefaultFontSize = 5.0f;
    public const float DefaultTextHeight = 1.0f;
    public const float DefaultWidthFontScale = 1.5f;
    public const float MinimumEdgeAngleDegrees = 30.0f;
    public const float MaximumEdgeAngleDegrees = 90.0f;
    public const float MinimumTextHeight = 0.1f;
    public const string DefaultFontFamilyName = "Arial";

    /// <summary>
    /// Creates one validated settings snapshot.
    /// </summary>
    public TagSettings(
        float tagHeight = DefaultTagHeight,
        float edgeAngleDegrees = DefaultEdgeAngleDegrees,
        float borderOffset = DefaultBorderOffset,
        string text = "",
        string fontFamilyName = DefaultFontFamilyName,
        float fontSize = DefaultFontSize,
        float textHeight = DefaultTextHeight,
        bool isTextFlipped = false,
        float? outerWidth = null,
        float? innerWidth = null)
    {
        TagHeight = ValidatePositive(tagHeight, nameof(tagHeight));
        EdgeAngleDegrees = Math.Clamp(
            ValidateFinite(edgeAngleDegrees, nameof(edgeAngleDegrees)),
            MinimumEdgeAngleDegrees,
            MaximumEdgeAngleDegrees);
        BorderOffset = MathF.Max(
            TagHeight,
            ValidateNonNegative(borderOffset, nameof(borderOffset)));
        Text = text ?? string.Empty;
        FontFamilyName = string.IsNullOrWhiteSpace(fontFamilyName)
            ? DefaultFontFamilyName
            : fontFamilyName.Trim();
        FontSize = ValidatePositive(fontSize, nameof(fontSize));
        TextHeight = MathF.Max(
            MinimumTextHeight,
            ValidateFinite(textHeight, nameof(textHeight)));
        IsTextFlipped = isTextFlipped;
        float defaultWidth = FontSize * DefaultWidthFontScale + BorderOffset;
        float minimumOuterWidth = FontSize + BorderOffset;
        OuterWidth = MathF.Max(
            minimumOuterWidth,
            outerWidth.HasValue
                ? ValidateNonNegative(outerWidth.Value, nameof(outerWidth))
                : defaultWidth);
        InnerWidth = innerWidth.HasValue
            ? ValidateNonNegative(innerWidth.Value, nameof(innerWidth))
            : defaultWidth;
    }

    public float TagHeight { get; }
    public float EdgeAngleDegrees { get; }
    public float BorderOffset { get; }
    public string Text { get; }
    public string FontFamilyName { get; }
    public float FontSize { get; }
    public float TextHeight { get; }
    public bool IsTextFlipped { get; }
    public float OuterWidth { get; }
    public float InnerWidth { get; }

    /// <summary>
    /// Gets the layer-panel name required by the Tag tool specification.
    /// </summary>
    public string GetDisplayName()
    {
        return string.IsNullOrEmpty(Text) ? "Tag" : $"Tag {Text}";
    }

    /// <summary>
    /// Rejects non-finite numeric settings.
    /// </summary>
    private static float ValidateFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Tag settings must be finite.");
        }

        return value;
    }

    /// <summary>
    /// Rejects zero and negative dimensions.
    /// </summary>
    private static float ValidatePositive(float value, string parameterName)
    {
        value = ValidateFinite(value, parameterName);

        if (value <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Tag dimensions must be positive.");
        }

        return value;
    }

    /// <summary>
    /// Rejects negative dimensions while allowing a zero border.
    /// </summary>
    private static float ValidateNonNegative(float value, string parameterName)
    {
        value = ValidateFinite(value, parameterName);

        if (value < 0.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Tag dimensions cannot be negative.");
        }

        return value;
    }
}
