// MeshTranslationTransform.cs
// Provides renderer-independent absolute-origin translation math and printable-bed movement constraints.
using System;
using System.Numerics;

namespace Pillar.Core.Entities;

/// <summary>
/// Stores translation-only limits derived from one model orientation and the current printable bed.
/// </summary>
public readonly struct MeshTranslationLimits
{
    /// <summary>
    /// Creates immutable origin limits and the model's current world-axis size.
    /// </summary>
    public MeshTranslationLimits(
        float minimumOriginX,
        float maximumOriginX,
        float minimumOriginY,
        float maximumOriginY,
        float minimumOriginZ,
        Vector3 modelSize,
        bool canFitPrintableX,
        bool canFitPrintableY)
    {
        MinimumOriginX = minimumOriginX;
        MaximumOriginX = maximumOriginX;
        MinimumOriginY = minimumOriginY;
        MaximumOriginY = maximumOriginY;
        MinimumOriginZ = minimumOriginZ;
        ModelSize = modelSize;
        CanFitPrintableX = canFitPrintableX;
        CanFitPrintableY = canFitPrintableY;
    }

    public float MinimumOriginX { get; }
    public float MaximumOriginX { get; }
    public float MinimumOriginY { get; }
    public float MaximumOriginY { get; }
    public float MinimumOriginZ { get; }
    public Vector3 ModelSize { get; }
    public bool CanFitPrintableX { get; }
    public bool CanFitPrintableY { get; }

    /// <summary>
    /// Gets whether the model has at least one valid translation inside the printable XY rectangle.
    /// </summary>
    public bool CanFitPrintableArea
    {
        get { return CanFitPrintableX && CanFitPrintableY; }
    }

    /// <summary>
    /// Clamps an absolute model-origin position to every available movement boundary.
    /// </summary>
    public Vector3 ClampOrigin(Vector3 requestedOrigin)
    {
        float x = CanFitPrintableX
            ? Math.Clamp(requestedOrigin.X, MinimumOriginX, MaximumOriginX)
            : requestedOrigin.X;
        float y = CanFitPrintableY
            ? Math.Clamp(requestedOrigin.Y, MinimumOriginY, MaximumOriginY)
            : requestedOrigin.Y;
        float z = MathF.Max(requestedOrigin.Z, MinimumOriginZ);
        return new Vector3(x, y, z);
    }
}

/// <summary>
/// Calculates absolute model-origin positions and constrained translation-only transform changes.
/// </summary>
public static class MeshTranslationTransform
{
    private const float FitTolerance = 0.0001f;

    /// <summary>
    /// Calculates the stable model transform origin used by the scale and rotation tools.
    /// </summary>
    public static Vector3 CalculateImportSpaceOrigin(MeshEntity mesh)
    {
        return MeshScaleTransform.CalculateImportSpaceOrigin(mesh);
    }

    /// <summary>
    /// Calculates the model origin's current absolute world position.
    /// </summary>
    public static Vector3 CalculateWorldOrigin(MeshEntity mesh, Vector3 importSpaceOrigin)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        return MeshRotationTransform.CalculateWorldOrigin(mesh.UserTransform, importSpaceOrigin);
    }

    /// <summary>
    /// Scans transformed vertices once to create reusable translation limits for a drag session.
    /// </summary>
    public static MeshTranslationLimits CreateLimits(
        MeshEntity mesh,
        Vector3 importSpaceOrigin,
        float printableX,
        float printableY)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (!float.IsFinite(printableX) || printableX <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(printableX), "Printable X size must be finite and greater than zero.");
        }

        if (!float.IsFinite(printableY) || printableY <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(printableY), "Printable Y size must be finite and greater than zero.");
        }

        Matrix4x4 worldTransform = mesh.WorldTransform;
        Vector3 firstWorldVertex = Vector3.Transform(mesh.Vertices[0], worldTransform);
        Vector3 worldMin = firstWorldVertex;
        Vector3 worldMax = firstWorldVertex;

        for (int i = 1; i < mesh.Vertices.Count; i++)
        {
            Vector3 worldVertex = Vector3.Transform(mesh.Vertices[i], worldTransform);
            worldMin = Vector3.Min(worldMin, worldVertex);
            worldMax = Vector3.Max(worldMax, worldVertex);
        }

        Vector3 worldOrigin = CalculateWorldOrigin(mesh, importSpaceOrigin);
        Vector3 minimumOffset = worldMin - worldOrigin;
        Vector3 maximumOffset = worldMax - worldOrigin;
        float halfPrintableX = printableX * 0.5f;
        float halfPrintableY = printableY * 0.5f;
        float minimumOriginX = -halfPrintableX - minimumOffset.X;
        float maximumOriginX = halfPrintableX - maximumOffset.X;
        float minimumOriginY = -halfPrintableY - minimumOffset.Y;
        float maximumOriginY = halfPrintableY - maximumOffset.Y;
        bool canFitPrintableX = minimumOriginX <= maximumOriginX + FitTolerance;
        bool canFitPrintableY = minimumOriginY <= maximumOriginY + FitTolerance;

        if (canFitPrintableX && minimumOriginX > maximumOriginX)
        {
            float midpointX = (minimumOriginX + maximumOriginX) * 0.5f;
            minimumOriginX = midpointX;
            maximumOriginX = midpointX;
        }

        if (canFitPrintableY && minimumOriginY > maximumOriginY)
        {
            float midpointY = (minimumOriginY + maximumOriginY) * 0.5f;
            minimumOriginY = midpointY;
            maximumOriginY = midpointY;
        }

        return new MeshTranslationLimits(
            minimumOriginX,
            maximumOriginX,
            minimumOriginY,
            maximumOriginY,
            -minimumOffset.Z,
            worldMax - worldMin,
            canFitPrintableX,
            canFitPrintableY);
    }

    /// <summary>
    /// Creates a translation-only transform that places the model origin at a constrained absolute position.
    /// </summary>
    public static bool TryCreateUserTransformForWorldOrigin(
        Transform3DData baseTransform,
        Vector3 baseWorldOrigin,
        Vector3 requestedWorldOrigin,
        MeshTranslationLimits limits,
        out Transform3DData transform,
        out Vector3 constrainedWorldOrigin)
    {
        constrainedWorldOrigin = limits.ClampOrigin(requestedWorldOrigin);

        if (!limits.CanFitPrintableArea)
        {
            transform = baseTransform;
            return false;
        }

        Vector3 translation = baseTransform.Translation + constrainedWorldOrigin - baseWorldOrigin;
        transform = new Transform3DData(translation, baseTransform.Rotation, baseTransform.Scale);
        return true;
    }
}
