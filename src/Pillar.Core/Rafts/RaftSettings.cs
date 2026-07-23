// RaftSettings.cs
// Stores validated, renderer-independent settings for one procedural raft.
using System;

namespace Pillar.Core.Rafts;

/// <summary>
/// Captures all raft parameters so changing type does not discard per-type values.
/// </summary>
public sealed class RaftSettings
{
    public const float DefaultRaftHeight = 0.7f;
    public const float DefaultLipHeight = 15.0f;
    public const float DefaultLipWidth = 0.7f;
    public const float DefaultRaftThickness = 0.7f;
    public const float DefaultLineThickness = 1.5f;
    public const float DefaultMaxSideLength = 50.0f;
    public const float DefaultFootSize = 10.0f;
    public const float DefaultEdgeAngleDegrees = 45.0f;
    public const float MinimumEdgeAngleDegrees = 30.0f;
    public const float MaximumEdgeAngleDegrees = 90.0f;

    public RaftType Type { get; }
    public float RaftHeight { get; }
    public float LipHeight { get; }
    public float LipWidth { get; }
    public float FootprintOffset { get; }
    public float RaftThickness { get; }
    public float LineThickness { get; }
    public float MaxSideLength { get; }
    public float FootSize { get; }
    public float EdgeAngleDegrees { get; }

    /// <summary>
    /// Creates one validated settings snapshot.
    /// </summary>
    public RaftSettings(
        RaftType type = RaftType.Footprint,
        float raftHeight = DefaultRaftHeight,
        float lipHeight = DefaultLipHeight,
        float lipWidth = DefaultLipWidth,
        float footprintOffset = 0.0f,
        float raftThickness = DefaultRaftThickness,
        float lineThickness = DefaultLineThickness,
        float footSize = DefaultFootSize,
        float edgeAngleDegrees = DefaultEdgeAngleDegrees,
        float maxSideLength = DefaultMaxSideLength)
    {
        Type = type;
        RaftHeight = ValidatePositive(raftHeight, nameof(raftHeight));
        LipHeight = ValidateNonNegative(lipHeight, nameof(lipHeight));
        LipWidth = ValidatePositive(lipWidth, nameof(lipWidth));
        FootprintOffset = ValidateNonNegative(footprintOffset, nameof(footprintOffset));
        RaftThickness = ValidatePositive(raftThickness, nameof(raftThickness));
        LineThickness = ValidatePositive(lineThickness, nameof(lineThickness));
        MaxSideLength = ValidatePositive(maxSideLength, nameof(maxSideLength));
        FootSize = ValidatePositive(footSize, nameof(footSize));
        EdgeAngleDegrees = Math.Clamp(
            ValidateFinite(edgeAngleDegrees, nameof(edgeAngleDegrees)),
            MinimumEdgeAngleDegrees,
            MaximumEdgeAngleDegrees);
    }

    /// <summary>
    /// Gets the layer-panel name for the selected generation strategy.
    /// </summary>
    public string GetDisplayName()
    {
        return Type switch
        {
            RaftType.Mesh => "Mesh",
            RaftType.Feet => "Feet",
            _ => "Footprint"
        };
    }

    /// <summary>
    /// Rejects non-finite numeric settings.
    /// </summary>
    private static float ValidateFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Raft settings must be finite.");
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
            throw new ArgumentOutOfRangeException(parameterName, "Raft dimensions must be positive.");
        }

        return value;
    }

    /// <summary>
    /// Rejects negative dimensions while allowing a disabled lip height.
    /// </summary>
    private static float ValidateNonNegative(float value, string parameterName)
    {
        value = ValidateFinite(value, parameterName);

        if (value < 0.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Raft dimensions cannot be negative.");
        }

        return value;
    }
}
