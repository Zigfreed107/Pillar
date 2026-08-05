// RaftTextSettings.cs
// Stores validated, renderer-independent settings for one text entity printed into a raft.
using System;

namespace Pillar.Core.RaftTexts;

/// <summary>
/// Captures the editable text, extrusion, and plan-view orientation for one raft text layer.
/// </summary>
public sealed class RaftTextSettings
{
    public const float DefaultFontSize = 5.0f;
    public const float DefaultTextHeight = 1.0f;
    public const float DefaultBorderOffset = 1.0f;
    public const float DefaultOrientationDegrees = 0.0f;
    public const float MinimumTextHeight = 0.1f;
    public const string DefaultFontFamilyName = "Arial";

    /// <summary>
    /// Creates one validated settings snapshot.
    /// </summary>
    public RaftTextSettings(
        string text = "",
        string fontFamilyName = DefaultFontFamilyName,
        float fontSize = DefaultFontSize,
        float textHeight = DefaultTextHeight,
        float borderOffset = DefaultBorderOffset,
        float orientationDegrees = DefaultOrientationDegrees)
    {
        Text = text ?? string.Empty;
        FontFamilyName = string.IsNullOrWhiteSpace(fontFamilyName)
            ? DefaultFontFamilyName
            : fontFamilyName.Trim();
        FontSize = ValidatePositive(fontSize, nameof(fontSize));
        TextHeight = MathF.Max(
            MinimumTextHeight,
            ValidateFinite(textHeight, nameof(textHeight)));
        BorderOffset = ValidateNonNegative(borderOffset, nameof(borderOffset));
        OrientationDegrees = ValidateOrientation(orientationDegrees, nameof(orientationDegrees));
    }

    public string Text { get; }
    public string FontFamilyName { get; }
    public float FontSize { get; }
    public float TextHeight { get; }
    public float BorderOffset { get; }
    public float OrientationDegrees { get; }

    /// <summary>
    /// Gets the layer-panel name required by the Raft Text specification.
    /// </summary>
    public string GetDisplayName()
    {
        return string.IsNullOrEmpty(Text) ? "Text" : $"Text {Text}";
    }

    /// <summary>
    /// Rejects non-finite numeric settings.
    /// </summary>
    private static float ValidateFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Raft text settings must be finite.");
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
            throw new ArgumentOutOfRangeException(parameterName, "Raft text dimensions must be positive.");
        }

        return value;
    }

    /// <summary>
    /// Restricts plan-view orientation to the range exposed by the options panel.
    /// </summary>
    private static float ValidateOrientation(float value, string parameterName)
    {
        value = ValidateFinite(value, parameterName);

        if (value < 0.0f || value > 360.0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Raft text orientation must be between 0 and 360 degrees.");
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
            throw new ArgumentOutOfRangeException(parameterName, "Raft text dimensions cannot be negative.");
        }

        return value;
    }
}
