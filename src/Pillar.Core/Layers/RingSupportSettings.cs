// RingSupportSettings.cs
// Stores the editable parametric definition used to regenerate a Ring Support group.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using Pillar.Core.Selection;
using Pillar.Core.Supports;

namespace Pillar.Core.Layers;

/// <summary>
/// Describes the persistent settings used by the Ring Support tool to regenerate one support group.
/// </summary>
public sealed class RingSupportSettings
{
    public const RingSupportSurfaceTargetMode DefaultSurfaceTargetMode = RingSupportSurfaceTargetMode.FirstReachable;
    public const SupportBaseGenerationMode DefaultBaseGenerationMode = SupportBaseGenerationMode.BuildPlateOnly;

    private readonly ReadOnlyCollection<FaceSelectionKey> _selectedFaces;

    /// <summary>
    /// Creates validated Ring Support generator settings.
    /// </summary>
    public RingSupportSettings(Vector3 firstPoint, Vector3 secondPoint, Vector3 thirdPoint, float spacing)
        : this(
            firstPoint,
            secondPoint,
            thirdPoint,
            spacing,
            DefaultSurfaceTargetMode,
            Array.Empty<FaceSelectionKey>())
    {
    }

    /// <summary>
    /// Creates validated Ring Support generator settings with explicit surface targeting and selected faces.
    /// </summary>
    public RingSupportSettings(
        Vector3 firstPoint,
        Vector3 secondPoint,
        Vector3 thirdPoint,
        float spacing,
        RingSupportSurfaceTargetMode surfaceTargetMode,
        IReadOnlyCollection<FaceSelectionKey> selectedFaces,
        SupportBaseGenerationMode baseGenerationMode = DefaultBaseGenerationMode)
    {
        if (selectedFaces == null)
        {
            throw new ArgumentNullException(nameof(selectedFaces));
        }

        FirstPoint = firstPoint;
        SecondPoint = secondPoint;
        ThirdPoint = thirdPoint;
        Spacing = ValidateSpacing(spacing);
        SurfaceTargetMode = ValidateSurfaceTargetMode(surfaceTargetMode);
        BaseGenerationMode = ValidateBaseGenerationMode(baseGenerationMode);

        if (SurfaceTargetMode == RingSupportSurfaceTargetMode.SelectedFacesOnly && selectedFaces.Count == 0)
        {
            throw new ArgumentException("Selected Faces Only targeting requires at least one selected face.", nameof(selectedFaces));
        }

        _selectedFaces = new ReadOnlyCollection<FaceSelectionKey>(new List<FaceSelectionKey>(selectedFaces));
    }

    /// <summary>
    /// Gets the first picked circumference point, which also locks the Ring Support construction plane.
    /// </summary>
    public Vector3 FirstPoint { get; }

    /// <summary>
    /// Gets the second picked circumference point projected onto the locked construction plane.
    /// </summary>
    public Vector3 SecondPoint { get; }

    /// <summary>
    /// Gets the third picked circumference point projected onto the locked construction plane.
    /// </summary>
    public Vector3 ThirdPoint { get; }

    /// <summary>
    /// Gets the requested distance between supports around the circumference.
    /// </summary>
    public float Spacing { get; }

    /// <summary>
    /// Gets how sampled ring points choose among mesh surfaces that overlap in XY.
    /// </summary>
    public RingSupportSurfaceTargetMode SurfaceTargetMode { get; }

    /// <summary>
    /// Gets where support bases should generate and the requested fallback order.
    /// </summary>
    public SupportBaseGenerationMode BaseGenerationMode { get; }

    /// <summary>
    /// Gets the source mesh faces allowed by Selected Faces Only targeting.
    /// </summary>
    public IReadOnlyList<FaceSelectionKey> SelectedFaces
    {
        get { return _selectedFaces; }
    }

    /// <summary>
    /// Creates a defensive copy for ownership boundaries and undo snapshots.
    /// </summary>
    public RingSupportSettings Clone()
    {
        return new RingSupportSettings(
            FirstPoint,
            SecondPoint,
            ThirdPoint,
            Spacing,
            SurfaceTargetMode,
            _selectedFaces,
            BaseGenerationMode);
    }

    /// <summary>
    /// Rejects unknown support-base generation policies before they enter document state.
    /// </summary>
    private static SupportBaseGenerationMode ValidateBaseGenerationMode(SupportBaseGenerationMode baseGenerationMode)
    {
        if (!Enum.IsDefined(baseGenerationMode))
        {
            throw new ArgumentOutOfRangeException(nameof(baseGenerationMode), "Support base generation mode is not supported.");
        }

        return baseGenerationMode;
    }

    /// <summary>
    /// Rejects unknown surface-targeting policies before they enter document state.
    /// </summary>
    private static RingSupportSurfaceTargetMode ValidateSurfaceTargetMode(RingSupportSurfaceTargetMode surfaceTargetMode)
    {
        if (!Enum.IsDefined(surfaceTargetMode))
        {
            throw new ArgumentOutOfRangeException(nameof(surfaceTargetMode), "Ring Support surface target mode is not supported.");
        }

        return surfaceTargetMode;
    }

    /// <summary>
    /// Rejects invalid spacing before generator settings reach document state.
    /// </summary>
    private static float ValidateSpacing(float spacing)
    {
        if (float.IsNaN(spacing) || float.IsInfinity(spacing) || spacing <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(spacing), "Ring Support spacing must be finite and positive.");
        }

        return spacing;
    }
}
