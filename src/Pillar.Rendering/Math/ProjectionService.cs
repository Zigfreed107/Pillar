// ProjectionService.cs
// Provides viewport projection and hit-testing helpers for rendering-layer interaction tools without leaking WPF shell logic.
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;

namespace Pillar.Rendering.Math;

/// <summary>
/// Describes one mesh-surface hit returned from viewport hit testing.
/// </summary>
public readonly struct MeshSurfaceHit
{
    /// <summary>
    /// Creates one immutable mesh-surface hit payload.
    /// </summary>
    public MeshSurfaceHit(Element3D hitModel, Vector3 hitPosition)
    {
        HitModel = hitModel;
        HitPosition = hitPosition;
    }

    /// <summary>
    /// Gets the viewport model that was hit.
    /// </summary>
    public Element3D HitModel { get; }

    /// <summary>
    /// Gets the world-space hit position.
    /// </summary>
    public Vector3 HitPosition { get; }
}

/// <summary>
/// Provides methods for projecting screen coordinates into world space within a 3D viewport.
/// </summary>
public class ProjectionService
{
    private readonly Viewport3DX _viewport;
    private readonly Workplane _workplane = new Workplane();

    /// <summary>
    /// Initializes the projection service for one viewport.
    /// </summary>
    public ProjectionService(Viewport3DX viewport)
    {
        _viewport = viewport;
    }

    /// <summary>
    /// Attempts to convert a 2D screen coordinate to its corresponding 3D world position on the workplane.
    /// </summary>
    public bool TryGetWorldPoint(Point mousePos, out Vector3 worldPoint)
    {
        Vector3 rayOrigin;
        Vector3 rayDirection;

        if (!_viewport.GetMouseRay(mousePos, out rayOrigin, out rayDirection))
        {
            worldPoint = Vector3.Zero;
            return false;
        }

        return _workplane.IntersectRay(rayOrigin, rayDirection, out worldPoint);
    }

    /// <summary>
    /// Attempts to convert a 2D screen coordinate to its corresponding 3D world position on the workplane.
    /// </summary>
    public bool TryGetWorldPoint(Vector2 screenPosition, out Vector3 worldPoint)
    {
        return TryGetWorldPoint(new Point(screenPosition.X, screenPosition.Y), out worldPoint);
    }

    /// <summary>
    /// Attempts to convert a 2D screen coordinate to a world position on a horizontal XY plane at the supplied Z height.
    /// </summary>
    public bool TryGetWorldPointOnHorizontalPlane(Vector2 screenPosition, float z, out Vector3 worldPoint)
    {
        Vector3 rayOrigin;
        Vector3 rayDirection;

        if (!_viewport.GetMouseRay(new Point(screenPosition.X, screenPosition.Y), out rayOrigin, out rayDirection))
        {
            worldPoint = Vector3.Zero;
            return false;
        }

        if (System.Math.Abs(rayDirection.Z) < 0.0001f)
        {
            worldPoint = Vector3.Zero;
            return false;
        }

        float distanceAlongRay = (z - rayOrigin.Z) / rayDirection.Z;

        if (distanceAlongRay < 0.0f)
        {
            worldPoint = Vector3.Zero;
            return false;
        }

        worldPoint = rayOrigin + rayDirection * distanceAlongRay;
        return true;
    }

    /// <summary>
    /// Attempts to hit-test one viewport model and return its world-space hit position.
    /// </summary>
    public bool TryGetMeshSurfaceHit(Vector2 screenPosition, out MeshSurfaceHit hit)
    {
        return TryGetMeshSurfaceHit(screenPosition, null, out hit);
    }

    /// <summary>
    /// Attempts to hit-test the viewport and return the first world-space hit accepted by the supplied model filter.
    /// </summary>
    public bool TryGetMeshSurfaceHit(Vector2 screenPosition, Predicate<Element3D>? acceptModel, out MeshSurfaceHit hit)
    {
        Point hitPoint = new Point(screenPosition.X, screenPosition.Y);
        IList<HitTestResult> hits = _viewport.FindHits(hitPoint);

        for (int i = 0; i < hits.Count; i++)
        {
            if (hits[i].ModelHit is Element3D hitModel)
            {
                if (acceptModel != null && !acceptModel(hitModel))
                {
                    continue;
                }

                hit = new MeshSurfaceHit(
                    hitModel,
                    new Vector3(hits[i].PointHit.X, hits[i].PointHit.Y, hits[i].PointHit.Z));

                return true;
            }
        }

        hit = default;
        return false;
    }
}
