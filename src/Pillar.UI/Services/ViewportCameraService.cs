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
    private const double MinimumNearPlaneDistance = 0.05;
    private const double WorkingEnvelopeMultiplier = 3.0;
    private const double OversizedModelExpansionMultiplier = 1.25;
    private const double MinimumClipSpanMultiplier = 0.10;
    private const double ZoomFarLimitRadiusMultiplier = 4.0;
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
        _document.EntityBatchUpdateCompleted -= Document_EntityBatchUpdateCompleted;

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
        _viewport.ZoomAroundMouseDownPoint = true;
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
        _document.EntityBatchUpdateCompleted += Document_EntityBatchUpdateCompleted;

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

        if (!_document.IsEntityBatchUpdateActive)
        {
            RebuildSceneBounds();
            UpdateCameraConfiguration(true);
        }
    }

    /// <summary>
    /// Rebuilds cached bounds once after a grouped document mutation.
    /// </summary>
    private void Document_EntityBatchUpdateCompleted(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
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
    /// Rebuilds cached working-envelope bounds from document entities and the shell-supplied background grid bounds.
    /// </summary>
    private void RebuildSceneBounds()
    {
        bool hasEntityBounds = false;
        Rect3D entityBoundsUnion = Rect3D.Empty;

        foreach (CadEntity entity in _document.Entities)
        {
            Rect3D entityBounds = CreateRect3D(entity.GetBounds());

            if (!IsUsableBounds(entityBounds))
            {
                continue;
            }

            if (!hasEntityBounds)
            {
                entityBoundsUnion = entityBounds;
                hasEntityBounds = true;
                continue;
            }

            entityBoundsUnion.Union(entityBounds);
        }

        bool hasBounds = false;
        Rect3D rebuiltBounds = Rect3D.Empty;
        Rect3D fallbackBounds = _getFallbackBounds();

        if (IsUsableBounds(fallbackBounds))
        {
            rebuiltBounds = fallbackBounds;
            hasBounds = true;
        }

        if (hasEntityBounds)
        {
            Rect3D expandedEntityBounds = ExpandBoundsAboutCenter(entityBoundsUnion, OversizedModelExpansionMultiplier);

            if (!hasBounds)
            {
                rebuiltBounds = expandedEntityBounds;
                hasBounds = true;
            }
            else
            {
                rebuiltBounds.Union(expandedEntityBounds);
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
    /// Applies stable near and far clip planes derived from the cached working envelope and current camera pose.
    /// </summary>
    private void UpdateOrthographicClipPlanes(HelixOrthographicCamera camera, double envelopeDiagonal)
    {
        Vector3D forward = camera.LookDirection;

        if (forward.LengthSquared < double.Epsilon)
        {
            return;
        }

        forward.Normalize();

        Point3D envelopeCenter = GetBoundsCenter(_cachedSceneBounds);
        Vector3D toEnvelopeCenter = envelopeCenter - camera.Position;
        double centerDepth = Vector3D.DotProduct(toEnvelopeCenter, forward);
        double envelopeRadius = GetWorkingEnvelopeRadius(envelopeDiagonal);
        double minimumClipSpan = Math.Max(envelopeRadius * MinimumClipSpanMultiplier, MinimumNearPlaneDistance * 10.0);
        double newNearPlaneDistance = Math.Max(MinimumNearPlaneDistance, centerDepth - envelopeRadius);
        double newFarPlaneDistance = Math.Max(newNearPlaneDistance + minimumClipSpan, centerDepth + envelopeRadius);

        AssignClipPlaneDistances(camera, newNearPlaneDistance, newFarPlaneDistance, MinimumNearPlaneDistance);
    }

    /// <summary>
    /// Applies conservative Helix zoom-distance limits that scale with the current working envelope.
    /// </summary>
    private void UpdateZoomDistanceLimits(double envelopeDiagonal)
    {
        double envelopeRadius = GetWorkingEnvelopeRadius(envelopeDiagonal);
        double nearLimit = Math.Max(envelopeDiagonal * 0.001, MinimumNearPlaneDistance);
        double farLimit = Math.Max(envelopeRadius * ZoomFarLimitRadiusMultiplier, nearLimit * 10.0);

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
    /// Gets the stable radius used for camera clipping and navigation limits.
    /// </summary>
    private static double GetWorkingEnvelopeRadius(double envelopeDiagonal)
    {
        return Math.Max((envelopeDiagonal * 0.5) * WorkingEnvelopeMultiplier, MinimumNearPlaneDistance * 10.0);
    }

    /// <summary>
    /// Gets the center point of one working-envelope bounds.
    /// </summary>
    private static Point3D GetBoundsCenter(Rect3D bounds)
    {
        return new Point3D(
            bounds.X + (bounds.SizeX / 2.0),
            bounds.Y + (bounds.SizeY / 2.0),
            bounds.Z + (bounds.SizeZ / 2.0));
    }

    /// <summary>
    /// Expands bounds around their center so unusually large imports get breathing room without changing normal grid framing.
    /// </summary>
    private static Rect3D ExpandBoundsAboutCenter(Rect3D bounds, double multiplier)
    {
        if (multiplier <= 1.0)
        {
            return bounds;
        }

        Point3D center = GetBoundsCenter(bounds);
        double expandedSizeX = Math.Max(bounds.SizeX * multiplier, MinimumSceneDiagonal);
        double expandedSizeY = Math.Max(bounds.SizeY * multiplier, MinimumSceneDiagonal);
        double expandedSizeZ = Math.Max(bounds.SizeZ * multiplier, MinimumSceneDiagonal);

        return new Rect3D(
            center.X - (expandedSizeX / 2.0),
            center.Y - (expandedSizeY / 2.0),
            center.Z - (expandedSizeZ / 2.0),
            expandedSizeX,
            expandedSizeY,
            expandedSizeZ);
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
