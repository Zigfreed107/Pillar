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
    public const float DefaultBoundarySpacingFactor = 0.8f;
    public const float DefaultBoundarySpacing = DefaultSpacing * DefaultBoundarySpacingFactor;
    public const float DefaultConcaveCornerAngleDegrees = 30.0f;

    /// <summary>
    /// Creates validated Area Support generator settings.
    /// </summary>
    public AreaSupportSettings(IReadOnlyCollection<FaceSelectionKey> selectedFaces, float spacing)
        : this(
            selectedFaces,
            spacing,
            CalculateDefaultBoundarySpacing(spacing),
            DefaultConcaveCornerAngleDegrees)
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
        BoundarySpacing = ValidateBoundarySpacing(boundarySpacing);
        ConcaveCornerAngleDegrees = ValidateConcaveCornerAngle(concaveCornerAngleDegrees);
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
    /// Gets the absolute spacing in millimeters used along offset boundary support paths.
    /// </summary>
    public float BoundarySpacing { get; }

    /// <summary>
    /// Gets the concave corner threshold in degrees for adding extra offset-corner supports.
    /// </summary>
    public float ConcaveCornerAngleDegrees { get; }

    /// <summary>
    /// Creates a defensive copy for ownership boundaries and undo snapshots.
    /// </summary>
    public AreaSupportSettings Clone()
    {
        return new AreaSupportSettings(SelectedFaces, Spacing, BoundarySpacing, ConcaveCornerAngleDegrees);
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
}
