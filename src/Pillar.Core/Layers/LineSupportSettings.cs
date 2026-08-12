// LineSupportSettings.cs
// Stores the editable parametric definition used to regenerate a Line Support group.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using Pillar.Core.Selection;

namespace Pillar.Core.Layers;

/// <summary>
/// Describes the persistent settings used by the Line Support tool to regenerate one support group.
/// </summary>
public sealed class LineSupportSettings
{
    public const bool DefaultPlaceSupportsAtBends = true;
    public const LineSupportSurfaceTargetMode DefaultSurfaceTargetMode = LineSupportSurfaceTargetMode.FirstReachable;

    private readonly List<Vector3> _points;
    private readonly ReadOnlyCollection<FaceSelectionKey> _selectedFaces;

    /// <summary>
    /// Creates validated Line Support generator settings with the default bend behavior.
    /// </summary>
    public LineSupportSettings(IReadOnlyList<Vector3> points, float spacing)
        : this(points, spacing, DefaultPlaceSupportsAtBends, DefaultSurfaceTargetMode, Array.Empty<FaceSelectionKey>())
    {
    }

    /// <summary>
    /// Creates validated Line Support generator settings.
    /// </summary>
    public LineSupportSettings(IReadOnlyList<Vector3> points, float spacing, bool placeSupportsAtBends)
        : this(points, spacing, placeSupportsAtBends, DefaultSurfaceTargetMode, Array.Empty<FaceSelectionKey>())
    {
    }

    /// <summary>
    /// Creates validated Line Support generator settings with an explicit surface-targeting policy.
    /// </summary>
    public LineSupportSettings(
        IReadOnlyList<Vector3> points,
        float spacing,
        bool placeSupportsAtBends,
        LineSupportSurfaceTargetMode surfaceTargetMode)
        : this(points, spacing, placeSupportsAtBends, surfaceTargetMode, Array.Empty<FaceSelectionKey>())
    {
    }

    /// <summary>
    /// Creates validated Line Support generator settings with explicit surface targeting and selected faces.
    /// </summary>
    public LineSupportSettings(
        IReadOnlyList<Vector3> points,
        float spacing,
        bool placeSupportsAtBends,
        LineSupportSurfaceTargetMode surfaceTargetMode,
        IReadOnlyCollection<FaceSelectionKey> selectedFaces)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        if (points.Count < 2)
        {
            throw new ArgumentException("Line Support settings require at least two polyline points.", nameof(points));
        }

        if (selectedFaces == null)
        {
            throw new ArgumentNullException(nameof(selectedFaces));
        }

        _points = new List<Vector3>(points.Count);

        for (int i = 0; i < points.Count; i++)
        {
            ValidatePoint(points[i], nameof(points));
            _points.Add(points[i]);
        }

        Spacing = ValidateSpacing(spacing);
        PlaceSupportsAtBends = placeSupportsAtBends;
        SurfaceTargetMode = ValidateSurfaceTargetMode(surfaceTargetMode);

        if (SurfaceTargetMode == LineSupportSurfaceTargetMode.SelectedFacesOnly && selectedFaces.Count == 0)
        {
            throw new ArgumentException("Selected Faces Only targeting requires at least one selected face.", nameof(selectedFaces));
        }

        _selectedFaces = new ReadOnlyCollection<FaceSelectionKey>(new List<FaceSelectionKey>(selectedFaces));
    }

    /// <summary>
    /// Gets the selected model-surface points that define the Line Support polyline.
    /// </summary>
    public IReadOnlyList<Vector3> Points
    {
        get { return _points; }
    }

    /// <summary>
    /// Gets the requested maximum distance between generated supports along the line.
    /// </summary>
    public float Spacing { get; }

    /// <summary>
    /// Gets whether clicked polyline vertices must be emitted as support locations.
    /// </summary>
    public bool PlaceSupportsAtBends { get; }

    /// <summary>
    /// Gets how sampled line points choose among mesh surfaces that overlap in XY.
    /// </summary>
    public LineSupportSurfaceTargetMode SurfaceTargetMode { get; }

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
    public LineSupportSettings Clone()
    {
        return new LineSupportSettings(_points, Spacing, PlaceSupportsAtBends, SurfaceTargetMode, _selectedFaces);
    }

    /// <summary>
    /// Rejects unknown surface-targeting policies before they enter document state.
    /// </summary>
    private static LineSupportSurfaceTargetMode ValidateSurfaceTargetMode(LineSupportSurfaceTargetMode surfaceTargetMode)
    {
        if (!Enum.IsDefined(surfaceTargetMode))
        {
            throw new ArgumentOutOfRangeException(nameof(surfaceTargetMode), "Line Support surface target mode is not supported.");
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
            throw new ArgumentOutOfRangeException(nameof(spacing), "Line Support spacing must be finite and positive.");
        }

        return spacing;
    }

    /// <summary>
    /// Rejects non-finite polyline coordinates before they become saved generator metadata.
    /// </summary>
    private static void ValidatePoint(Vector3 point, string parameterName)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
        {
            throw new ArgumentException("Line Support points must be finite.", parameterName);
        }
    }
}
