// AreaSupportSettings.cs
// Stores the editable parametric definition used to regenerate an Area Support group from selected mesh faces.
using Pillar.Core.Selection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Pillar.Core.Layers;

/// <summary>
/// Describes the persistent face-area definition used by the Area Support tool to regenerate one support group.
/// </summary>
public sealed class AreaSupportSettings
{
    public const float DefaultSpacing = 3.0f;
    public const float DefaultBoundaryOffsetFactor = 0.5f;
    public const float DefaultBoundaryOffset = DefaultSpacing * DefaultBoundaryOffsetFactor;
    public const float DefaultBoundarySpacingFactor = 0.8f;
    public const float DefaultBoundarySpacing = DefaultSpacing * DefaultBoundarySpacingFactor;
    public const float DefaultConcaveCornerAngleDegrees = 30.0f;
    public const bool DefaultSupportThinRegions = true;
    public const float DefaultMinimumThinRegionThickness = 1.0f;
    public const AreaSupportFillMode DefaultFillMode = AreaSupportFillMode.HexGrid;
    public const int DefaultAdditionalOffsetCount = 1;
    public const int MaximumAdditionalOffsetCount = 100;

    /// <summary>
    /// Creates validated Area Support generator settings.
    /// </summary>
    public AreaSupportSettings(IReadOnlyCollection<FaceSelectionKey> selectedFaces, float spacing)
        : this(
            selectedFaces,
            spacing,
            CalculateDefaultBoundaryOffset(spacing),
            CalculateDefaultBoundarySpacing(spacing),
            DefaultConcaveCornerAngleDegrees,
            DefaultSupportThinRegions,
            DefaultMinimumThinRegionThickness,
            DefaultFillMode,
            DefaultAdditionalOffsetCount)
    {
    }

    /// <summary>
    /// Creates validated Area Support generator settings.
    /// </summary>
    public AreaSupportSettings(
        IReadOnlyCollection<FaceSelectionKey> selectedFaces,
        float spacing,
        float boundarySpacing,
        float concaveCornerAngleDegrees)
        : this(
            selectedFaces,
            spacing,
            CalculateDefaultBoundaryOffset(spacing),
            boundarySpacing,
            concaveCornerAngleDegrees,
            DefaultSupportThinRegions,
            DefaultMinimumThinRegionThickness,
            DefaultFillMode,
            DefaultAdditionalOffsetCount)
    {
    }

    /// <summary>
    /// Creates validated Area Support generator settings.
    /// </summary>
    public AreaSupportSettings(
        IReadOnlyCollection<FaceSelectionKey> selectedFaces,
        float spacing,
        float boundarySpacing,
        float concaveCornerAngleDegrees,
        bool supportThinRegions,
        float minimumThinRegionThickness)
        : this(
            selectedFaces,
            spacing,
            CalculateDefaultBoundaryOffset(spacing),
            boundarySpacing,
            concaveCornerAngleDegrees,
            supportThinRegions,
            minimumThinRegionThickness,
            DefaultFillMode,
            DefaultAdditionalOffsetCount)
    {
    }

    /// <summary>
    /// Creates validated Area Support generator settings.
    /// </summary>
    public AreaSupportSettings(
        IReadOnlyCollection<FaceSelectionKey> selectedFaces,
        float spacing,
        float boundaryOffset,
        float boundarySpacing,
        float concaveCornerAngleDegrees,
        bool supportThinRegions,
        float minimumThinRegionThickness,
        AreaSupportFillMode fillMode,
        int additionalOffsetCount)
    {
        if (selectedFaces == null)
        {
            throw new ArgumentNullException(nameof(selectedFaces));
        }

        if (selectedFaces.Count == 0)
        {
            throw new ArgumentException("Area Support requires at least one selected face.", nameof(selectedFaces));
        }

        List<FaceSelectionKey> copiedFaces = new List<FaceSelectionKey>(selectedFaces.Count);

        foreach (FaceSelectionKey selectedFace in selectedFaces)
        {
            copiedFaces.Add(selectedFace);
        }

        SelectedFaces = new ReadOnlyCollection<FaceSelectionKey>(copiedFaces);
        Spacing = ValidateSpacing(spacing);
        BoundaryOffset = ValidateBoundaryOffset(boundaryOffset);
        BoundarySpacing = ValidateBoundarySpacing(boundarySpacing);
        ConcaveCornerAngleDegrees = ValidateConcaveCornerAngle(concaveCornerAngleDegrees);
        SupportThinRegions = supportThinRegions;
        MinimumThinRegionThickness = ValidateMinimumThinRegionThickness(minimumThinRegionThickness);
        FillMode = ValidateFillMode(fillMode);
        AdditionalOffsetCount = ValidateAdditionalOffsetCount(additionalOffsetCount);
    }

    /// <summary>
    /// Gets the source mesh faces that define the supportable area.
    /// </summary>
    public IReadOnlyList<FaceSelectionKey> SelectedFaces { get; }

    /// <summary>
    /// Gets the requested maximum support coverage spacing in millimeters.
    /// </summary>
    public float Spacing { get; }

    /// <summary>
    /// Gets the inward distance from source boundaries to the generated offset boundary.
    /// </summary>
    public float BoundaryOffset { get; }

    /// <summary>
    /// Gets the absolute spacing in millimeters used along offset boundary support paths.
    /// </summary>
    public float BoundarySpacing { get; }

    /// <summary>
    /// Gets the concave corner threshold in degrees for adding extra offset-corner supports.
    /// </summary>
    public float ConcaveCornerAngleDegrees { get; }

    /// <summary>
    /// Gets whether collapsed thin regions should receive centreline fallback supports.
    /// </summary>
    public bool SupportThinRegions { get; }

    /// <summary>
    /// Gets the minimum local area thickness required for centreline fallback supports.
    /// </summary>
    public float MinimumThinRegionThickness { get; }

    /// <summary>
    /// Gets the strategy used to distribute supports inside the selected area.
    /// </summary>
    public AreaSupportFillMode FillMode { get; }

    /// <summary>
    /// Gets how many inward rings are generated after the original offset boundary.
    /// </summary>
    public int AdditionalOffsetCount { get; }

    /// <summary>
    /// Creates a defensive copy for ownership boundaries and undo snapshots.
    /// </summary>
    public AreaSupportSettings Clone()
    {
        return new AreaSupportSettings(SelectedFaces, Spacing, BoundaryOffset, BoundarySpacing, ConcaveCornerAngleDegrees, SupportThinRegions, MinimumThinRegionThickness, FillMode, AdditionalOffsetCount);
    }

    /// <summary>
    /// Calculates the default boundary offset from the support spacing.
    /// </summary>
    public static float CalculateDefaultBoundaryOffset(float spacing)
    {
        return ValidateSpacing(spacing) * DefaultBoundaryOffsetFactor;
    }

    /// <summary>
    /// Calculates the default absolute boundary spacing from the support spacing.
    /// </summary>
    public static float CalculateDefaultBoundarySpacing(float spacing)
    {
        return ValidateSpacing(spacing) * DefaultBoundarySpacingFactor;
    }

    /// <summary>
    /// Rejects invalid spacing before generator settings reach document state.
    /// </summary>
    private static float ValidateSpacing(float spacing)
    {
        if (!float.IsFinite(spacing) || spacing <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(spacing), "Area Support spacing must be finite and positive.");
        }

        return spacing;
    }

    /// <summary>
    /// Rejects invalid boundary offsets before generator settings reach document state.
    /// </summary>
    private static float ValidateBoundaryOffset(float boundaryOffset)
    {
        if (!float.IsFinite(boundaryOffset) || boundaryOffset <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(boundaryOffset), "Area Support boundary offset must be finite and positive.");
        }

        return boundaryOffset;
    }

    /// <summary>
    /// Rejects invalid boundary spacing before generator settings reach document state.
    /// </summary>
    private static float ValidateBoundarySpacing(float boundarySpacing)
    {
        if (!float.IsFinite(boundarySpacing) || boundarySpacing <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(boundarySpacing), "Area Support boundary spacing must be finite and positive.");
        }

        return boundarySpacing;
    }

    /// <summary>
    /// Rejects invalid concave corner thresholds before generator settings reach document state.
    /// </summary>
    private static float ValidateConcaveCornerAngle(float concaveCornerAngleDegrees)
    {
        if (!float.IsFinite(concaveCornerAngleDegrees) || concaveCornerAngleDegrees < 0.0f || concaveCornerAngleDegrees > 180.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(concaveCornerAngleDegrees), "Area Support concave corner angle must be from 0 to 180 degrees.");
        }

        return concaveCornerAngleDegrees;
    }

    /// <summary>
    /// Rejects invalid minimum thin-region thickness values before generator settings reach document state.
    /// </summary>
    private static float ValidateMinimumThinRegionThickness(float minimumThinRegionThickness)
    {
        if (!float.IsFinite(minimumThinRegionThickness) || minimumThinRegionThickness < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumThinRegionThickness), "Area Support minimum thin-region thickness must be finite and non-negative.");
        }

        return minimumThinRegionThickness;
    }

    /// <summary>
    /// Rejects unknown fill strategies before they reach generated document state.
    /// </summary>
    private static AreaSupportFillMode ValidateFillMode(AreaSupportFillMode fillMode)
    {
        if (!Enum.IsDefined(fillMode))
        {
            throw new ArgumentOutOfRangeException(nameof(fillMode), "Area Support fill mode is not supported.");
        }

        return fillMode;
    }

    /// <summary>
    /// Bounds repeated offsets so live preview work remains predictable on complex selected areas.
    /// </summary>
    private static int ValidateAdditionalOffsetCount(int additionalOffsetCount)
    {
        if (additionalOffsetCount < 0 || additionalOffsetCount > MaximumAdditionalOffsetCount)
        {
            throw new ArgumentOutOfRangeException(nameof(additionalOffsetCount), $"Area Support additional offset count must be from 0 to {MaximumAdditionalOffsetCount}.");
        }

        return additionalOffsetCount;
    }
}
