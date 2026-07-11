// SupportButtressModifierSettings.cs
// Stores renderer-independent parameters for generated support buttress modifiers.
using System;

namespace Pillar.Core.Layers;

/// <summary>
/// Describes saved parameters for a Buttress support modifier.
/// </summary>
public sealed class SupportButtressModifierSettings
{
    public const float DefaultMinimumButtressHeight = 10.0f;
    public const float DefaultButtressSpacing = 2.0f;

    /// <summary>
    /// Creates one validated buttress parameter set with default bracing parameters for legacy callers.
    /// </summary>
    public SupportButtressModifierSettings(float minimumButtressHeight, float buttressSpacing)
        : this(minimumButtressHeight, buttressSpacing, SupportBraceModifierSettings.CreateDefault())
    {
    }

    /// <summary>
    /// Creates one validated buttress modifier parameter set.
    /// </summary>
    public SupportButtressModifierSettings(
        float minimumButtressHeight,
        float buttressSpacing,
        SupportBraceModifierSettings braceSettings)
    {
        MinimumButtressHeight = ValidateNonNegativeFinite(minimumButtressHeight, nameof(minimumButtressHeight));
        ButtressSpacing = ValidateNonNegativeFinite(buttressSpacing, nameof(buttressSpacing));
        BraceSettings = braceSettings?.Clone() ?? throw new ArgumentNullException(nameof(braceSettings));
    }

    /// <summary>
    /// Gets the minimum support height eligible for buttressing.
    /// </summary>
    public float MinimumButtressHeight { get; }

    /// <summary>
    /// Gets the equilateral-triangle side length between the original support and both buttress bases.
    /// </summary>
    public float ButtressSpacing { get; }

    /// <summary>
    /// Gets the captured bracing parameters used for branch angle and reinforcement members.
    /// </summary>
    public SupportBraceModifierSettings BraceSettings { get; }

    /// <summary>
    /// Creates default settings for a new Buttress tool session.
    /// </summary>
    public static SupportButtressModifierSettings CreateDefault()
    {
        return new SupportButtressModifierSettings(
            DefaultMinimumButtressHeight,
            DefaultButtressSpacing,
            SupportBraceModifierSettings.CreateDefault());
    }

    /// <summary>
    /// Creates a defensive copy.
    /// </summary>
    public SupportButtressModifierSettings Clone()
    {
        return new SupportButtressModifierSettings(MinimumButtressHeight, ButtressSpacing, BraceSettings);
    }

    /// <summary>
    /// Validates finite non-negative dimensions.
    /// </summary>
    private static float ValidateNonNegativeFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value < 0.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Buttress settings must be finite and non-negative.");
        }

        return value;
    }
}