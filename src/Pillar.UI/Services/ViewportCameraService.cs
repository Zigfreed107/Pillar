// ViewportCameraService.cs
// Centralizes Helix camera navigation configuration and dynamic orthographic clip-plane management so the shell does not own viewport movement logic.
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using HelixOrthographicCamera = HelixToolkit.Wpf.SharpDX.OrthographicCamera;

namespace Pillar.UI.Services;

/// <summary>
/// Owns Helix camera navigation configuration and dynamic clip-plane updates for the main viewport.
/// </summary>
public sealed class ViewportCameraService : IDisposable
{
    private const double MinimumSceneDiagonal = 0.001;
    private readonly Viewport3DX _viewport;
    private readonly CadDocument _document;
    private readonly Func<Rect3D> _getFallbackBounds;
    private readonly HashSet<CadEntity> _subscribedEntities = new HashSet<CadEntity>();
    private Rect3D _cachedSceneBounds;
    private bool _hasCachedSceneBounds;
    private CameraPoseSnapshot _lastCameraPose;
    private bool _hasLastCameraPose;
    private bool _isDisposed;

    /// <summary>
    /// Stores the camera values that affect orthographic clip-plane calculations.
    /// </summary>
    private readonly struct CameraPoseSnapshot
    {
        /// <summary>
        /// Creates one snapshot of the orthographic camera pose used for frustum comparisons.
        /// </summary>
        public CameraPoseSnapshot(Point3D position, Vector3D lookDirection, double width)
        {
            Position = position;
            LookDirection = lookDirection;
            Width = width;
        }

        /// <summary>
        /// Gets the camera position.
        /// </summary>
        public Point3D Position { get; }

        /// <summary>
        /// Gets the camera forward direction and distance.
        /// </summary>
        public Vector3D LookDirection { get; }

        /// <summary>
        /// Gets the orthographic view width, which changes during zoom.
        /// </summary>
        public double Width { get; }
    }

    /// <summary>
    /// Creates the viewport camera service for one document-backed Helix viewport.
    /// </summary>
    public ViewportCameraService(Viewport3DX viewport, CadDocument document, Func<Rect3D> getFallbackBounds)
    {
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _getFallbackBounds = getFallbackBounds ?? throw new ArgumentNullException(nameof(getFallbackBounds));

        ConfigureHelixNavigation();
        SubscribeToDocument();
        RebuildSceneBounds();
        _viewport.CameraChanged += Viewport_CameraChanged;
        UpdateCameraConfiguration(true);
    }

    /// <summary>
    /// Releases event subscriptions held by the service.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _viewport.CameraChanged -= Viewport_CameraChanged;
        _document.EntitiesChanged -= Document_EntitiesChanged;

        foreach (CadEntity entity in _subscribedEntities)
        {
            entity.PropertyChanged -= Entity_PropertyChanged;
        }

        _subscribedEntities.Clear();
    }

    /// <summary>
    /// Configures Helix as the sole owner of viewport navigation and remaps pan to middle mouse when possible.
    /// </summary>
    private void ConfigureHelixNavigation()
    {
        _viewport.UseDefaultGestures = true;
        _viewport.IsPanEnabled = true;
        _viewport.IsRotationEnabled = true;
        _viewport.IsZoomEnabled = true;
        _viewport.IsInertiaEnabled = false;
        _viewport.RotateAroundMouseDownPoint = true;
        _viewport.ZoomAroundMouseDownPoint = false;
        RemapPanGestureToMiddleMouse();
    }

    /// <summary>
    /// Replaces Helix's default Shift+Right pan gesture with middle mouse drag when that binding exists.
    /// </summary>
    private void RemapPanGestureToMiddleMouse()
    {
        foreach (InputBinding inputBinding in _viewport.InputBindings)
        {
            if (inputBinding is not MouseBinding mouseBinding)
            {
                continue;
            }

            if (mouseBinding.Gesture is not MouseGesture mouseGesture)
            {
                continue;
            }

            if (mouseGesture.MouseAction != MouseAction.RightClick || mouseGesture.Modifiers != ModifierKeys.Shift)
            {
                continue;
            }

            mouseBinding.Gesture = new MouseGesture(MouseAction.MiddleClick);
            return;
        }
    }

    /// <summary>
    /// Subscribes to document entity changes so cached bounds stay aligned with imported models and supports.
    /// </summary>
    private void SubscribeToDocument()
    {
        _document.EntitiesChanged += Document_EntitiesChanged;

        foreach (CadEntity entity in _document.Entities)
        {
            SubscribeToEntity(entity);
        }
    }

    /// <summary>
    /// Responds to camera changes by updating clip planes only when the effective orthographic pose changed.
    /// </summary>
    private void Viewport_CameraChanged(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        UpdateCameraConfiguration(false);
    }

    /// <summary>
    /// Rebuilds scene bounds when document entities are added or removed.
    /// </summary>
    private void Document_EntitiesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;

        if (e.NewItems != null)
        {
            foreach (CadEntity entity in e.NewItems)
            {
                SubscribeToEntity(entity);
            }
        }

        if (e.OldItems != null)
        {
            foreach (CadEntity entity in e.OldItems)
            {
                UnsubscribeFromEntity(entity);
            }
        }

        RebuildSceneBounds();
        UpdateCameraConfiguration(true);
    }

    /// <summary>
    /// Rebuilds scene bounds when one entity changes in a way that may affect its world-space extents.
    /// </summary>
    private void Entity_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RebuildSceneBounds();
        UpdateCameraConfiguration(true);
    }

    /// <summary>
    /// Subscribes to one entity exactly once.
    /// </summary>
    private void SubscribeToEntity(CadEntity entity)
    {
        if (_subscribedEntities.Add(entity))
        {
            entity.PropertyChanged += Entity_PropertyChanged;
        }
    }

    /// <summary>
    /// Removes the bounds subscription for one entity that left the document.
    /// </summary>
    private void UnsubscribeFromEntity(CadEntity entity)
    {
        if (_subscribedEntities.Remove(entity))
        {
            entity.PropertyChanged -= Entity_PropertyChanged;
        }
    }

    /// <summary>
    /// Rebuilds cached scene bounds from document entities and the shell-supplied background grid bounds.
    /// </summary>
    private void RebuildSceneBounds()
    {
        bool hasBounds = false;
        Rect3D rebuiltBounds = Rect3D.Empty;

        foreach (CadEntity entity in _document.Entities)
        {
            Rect3D entityBounds = CreateRect3D(entity.GetBounds());

            if (!IsUsableBounds(entityBounds))
            {
                continue;
            }

            if (!hasBounds)
            {
                rebuiltBounds = entityBounds;
                hasBounds = true;
                continue;
            }

            rebuiltBounds.Union(entityBounds);
        }

        Rect3D fallbackBounds = _getFallbackBounds();

        if (IsUsableBounds(fallbackBounds))
        {
            if (!hasBounds)
            {
                rebuiltBounds = fallbackBounds;
                hasBounds = true;
            }
            else
            {
                rebuiltBounds.Union(fallbackBounds);
            }
        }

        _cachedSceneBounds = rebuiltBounds;
        _hasCachedSceneBounds = hasBounds;

        if (hasBounds)
        {
            UpdateZoomDistanceLimits(GetSceneDiagonal(rebuiltBounds));
        }
    }

    /// <summary>
    /// Updates zoom guard rails and orthographic clip planes for the current camera state.
    /// </summary>
    private void UpdateCameraConfiguration(bool force)
    {
        if (!_hasCachedSceneBounds)
        {
            return;
        }

        if (_viewport.Camera is not HelixOrthographicCamera orthographicCamera)
        {
            return;
        }

        double sceneDiagonal = GetSceneDiagonal(_cachedSceneBounds);
        CameraPoseSnapshot currentPose = new CameraPoseSnapshot(
            orthographicCamera.Position,
            orthographicCamera.LookDirection,
            orthographicCamera.Width);

        if (!force && _hasLastCameraPose && !HasCameraPoseChanged(_lastCameraPose, currentPose, sceneDiagonal))
        {
            return;
        }

        _lastCameraPose = currentPose;
        _hasLastCameraPose = true;
        UpdateZoomDistanceLimits(sceneDiagonal);
        UpdateOrthographicClipPlanes(orthographicCamera, sceneDiagonal);
    }

    /// <summary>
    /// Applies dynamic near and far clip planes derived from cached scene bounds and the current orthographic camera pose.
    /// </summary>
    private void UpdateOrthographicClipPlanes(HelixOrthographicCamera camera, double sceneDiagonal)
    {
        Vector3D forward = camera.LookDirection;

        if (forward.LengthSquared < double.Epsilon)
        {
            return;
        }

        forward.Normalize();

        double nearFloor = Math.Max(sceneDiagonal * 1e-5, 0.001);
        double frontMargin = Math.Max(sceneDiagonal * 0.05, nearFloor * 10.0);
        double backMargin = Math.Max(sceneDiagonal * 0.10, nearFloor * 20.0);
        double nearestPositiveDepth = double.MaxValue;
        double farthestPositiveDepth = 0.0;
        bool hasPositiveDepth = false;

        foreach (Point3D corner in EnumerateBoundsCorners(_cachedSceneBounds))
        {
            Vector3D toCorner = corner - camera.Position;
            double depth = Vector3D.DotProduct(toCorner, forward);

            if (depth <= 0.0)
            {
                continue;
            }

            hasPositiveDepth = true;
            nearestPositiveDepth = Math.Min(nearestPositiveDepth, depth);
            farthestPositiveDepth = Math.Max(farthestPositiveDepth, depth);
        }

        double newNearPlaneDistance;
        double newFarPlaneDistance;

        if (hasPositiveDepth)
        {
            newNearPlaneDistance = Math.Max(nearFloor, nearestPositiveDepth - frontMargin);
            newFarPlaneDistance = Math.Max(newNearPlaneDistance + (nearFloor * 10.0), farthestPositiveDepth + backMargin);
        }
        else
        {
            newNearPlaneDistance = nearFloor;
            newFarPlaneDistance = Math.Max(newNearPlaneDistance + backMargin, sceneDiagonal + backMargin);
        }

        AssignClipPlaneDistances(camera, newNearPlaneDistance, newFarPlaneDistance, nearFloor);
    }

    /// <summary>
    /// Applies conservative Helix zoom-distance limits that scale with the current scene size.
    /// </summary>
    private void UpdateZoomDistanceLimits(double sceneDiagonal)
    {
        double nearLimit = Math.Max(sceneDiagonal * 0.001, 0.01);
        double farLimit = Math.Max(sceneDiagonal * 100.0, nearLimit * 10.0);

        AssignViewportDouble(refValue: _viewport.ZoomDistanceLimitNear, newValue: nearLimit, setter: value => _viewport.ZoomDistanceLimitNear = value);
        AssignViewportDouble(refValue: _viewport.ZoomDistanceLimitFar, newValue: farLimit, setter: value => _viewport.ZoomDistanceLimitFar = value);
    }

    /// <summary>
    /// Writes clip-plane distances only when the new values differ meaningfully from the current camera settings.
    /// </summary>
    private static void AssignClipPlaneDistances(HelixOrthographicCamera camera, double nearPlaneDistance, double farPlaneDistance, double nearFloor)
    {
        double tolerance = Math.Max(nearFloor * 0.1, 0.0001);

        if (Math.Abs(camera.NearPlaneDistance - nearPlaneDistance) > tolerance)
        {
            camera.NearPlaneDistance = nearPlaneDistance;
        }

        if (Math.Abs(camera.FarPlaneDistance - farPlaneDistance) > tolerance)
        {
            camera.FarPlaneDistance = farPlaneDistance;
        }
    }

    /// <summary>
    /// Writes one viewport double property only when the value changed meaningfully.
    /// </summary>
    private static void AssignViewportDouble(double refValue, double newValue, Action<double> setter)
    {
        if (Math.Abs(refValue - newValue) <= 0.0001)
        {
            return;
        }

        setter(newValue);
    }

    /// <summary>
    /// Checks whether the orthographic camera changed enough to justify recalculating clip planes.
    /// </summary>
    private static bool HasCameraPoseChanged(CameraPoseSnapshot previousPose, CameraPoseSnapshot currentPose, double sceneDiagonal)
    {
        double positionTolerance = Math.Max(sceneDiagonal * 1e-6, 0.0001);
        double directionTolerance = 0.0001;
        double widthTolerance = Math.Max(sceneDiagonal * 1e-6, 0.0001);

        if (GetDistanceSquared(previousPose.Position, currentPose.Position) > (positionTolerance * positionTolerance))
        {
            return true;
        }

        if (GetDistanceSquared(previousPose.LookDirection, currentPose.LookDirection) > (directionTolerance * directionTolerance))
        {
            return true;
        }

        return Math.Abs(previousPose.Width - currentPose.Width) > widthTolerance;
    }

    /// <summary>
    /// Converts one domain bounds tuple into a WPF Rect3D.
    /// </summary>
    private static Rect3D CreateRect3D((System.Numerics.Vector3 Min, System.Numerics.Vector3 Max) bounds)
    {
        double width = Math.Max(bounds.Max.X - bounds.Min.X, 0.0f);
        double height = Math.Max(bounds.Max.Y - bounds.Min.Y, 0.0f);
        double depth = Math.Max(bounds.Max.Z - bounds.Min.Z, 0.0f);
        return new Rect3D(bounds.Min.X, bounds.Min.Y, bounds.Min.Z, width, height, depth);
    }

    /// <summary>
    /// Checks whether one Rect3D is valid enough to use for zoom limits and clip-plane calculations.
    /// </summary>
    private static bool IsUsableBounds(Rect3D bounds)
    {
        if (bounds.IsEmpty)
        {
            return false;
        }

        return !double.IsNaN(bounds.SizeX)
            && !double.IsNaN(bounds.SizeY)
            && !double.IsNaN(bounds.SizeZ)
            && !double.IsInfinity(bounds.SizeX)
            && !double.IsInfinity(bounds.SizeY)
            && !double.IsInfinity(bounds.SizeZ);
    }

    /// <summary>
    /// Gets a stable scene diagonal used to scale clip-plane margins and zoom limits.
    /// </summary>
    private static double GetSceneDiagonal(Rect3D bounds)
    {
        double diagonal = Math.Sqrt((bounds.SizeX * bounds.SizeX) + (bounds.SizeY * bounds.SizeY) + (bounds.SizeZ * bounds.SizeZ));
        return Math.Max(diagonal, MinimumSceneDiagonal);
    }

    /// <summary>
    /// Enumerates the eight corners of one axis-aligned bounding box.
    /// </summary>
    private static IEnumerable<Point3D> EnumerateBoundsCorners(Rect3D bounds)
    {
        double minX = bounds.X;
        double minY = bounds.Y;
        double minZ = bounds.Z;
        double maxX = bounds.X + bounds.SizeX;
        double maxY = bounds.Y + bounds.SizeY;
        double maxZ = bounds.Z + bounds.SizeZ;

        yield return new Point3D(minX, minY, minZ);
        yield return new Point3D(minX, minY, maxZ);
        yield return new Point3D(minX, maxY, minZ);
        yield return new Point3D(minX, maxY, maxZ);
        yield return new Point3D(maxX, minY, minZ);
        yield return new Point3D(maxX, minY, maxZ);
        yield return new Point3D(maxX, maxY, minZ);
        yield return new Point3D(maxX, maxY, maxZ);
    }

    /// <summary>
    /// Gets squared distance between two camera positions without allocating temporary vectors.
    /// </summary>
    private static double GetDistanceSquared(Point3D a, Point3D b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        double dz = a.Z - b.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    /// <summary>
    /// Gets squared distance between two direction vectors without allocating temporary vectors.
    /// </summary>
    private static double GetDistanceSquared(Vector3D a, Vector3D b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        double dz = a.Z - b.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }
}
