// ModelTranslateTool.cs
// Owns hit testing and allocation-free world-axis dragging for the model translation gizmo.
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Entities;
using Pillar.Core.Tools;
using Pillar.Rendering.Preview;
using Pillar.Rendering.Scene;
using System;
using System.Numerics;
using System.Windows;
using System.Windows.Media.Media3D;

namespace Pillar.Rendering.Tools;

/// <summary>
/// Converts model translation arrow drags into constrained transform preview requests.
/// </summary>
public sealed class ModelTranslateTool : ITool
{
    private const float ArrowLengthModelSizeFactor = 1.5f;
    private const float MinimumScreenAxisLength = 0.001f;
    private const float DefaultArrowLength = 1.0f;
    private readonly Viewport3DX _viewport;
    private readonly SceneManager _scene;
    private MeshEntity? _mesh;
    private Vector3 _importSpaceOrigin;
    private MeshTranslationLimits _limits;
    private ModelTranslateGizmoHandleKind _dragHandle;
    private Vector2 _dragStartScreenPosition;
    private Transform3DData _dragStartTransform;
    private Vector3 _dragStartWorldOrigin;
    private Vector3 _arrowLengths;

    /// <summary>
    /// Creates one reusable viewport interaction controller.
    /// </summary>
    public ModelTranslateTool(Viewport3DX viewport, SceneManager scene)
    {
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
    }

    /// <summary>
    /// Raised when a pointer gesture has produced a valid constrained transform preview.
    /// </summary>
    public event Action<Transform3DData, Vector3>? PreviewTransformRequested;

    /// <summary>
    /// Starts one translation session and displays arrows at the supplied stable model origin.
    /// </summary>
    public void Begin(
        MeshEntity mesh,
        Vector3 importSpaceOrigin,
        MeshTranslationLimits limits,
        Vector3 worldOrigin)
    {
        Cancel();
        _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        _importSpaceOrigin = importSpaceOrigin;
        _limits = limits;
        _arrowLengths = limits.ModelSize * ArrowLengthModelSizeFactor;
        ShowGizmo(worldOrigin);
    }

    /// <summary>
    /// Synchronizes the persistent gizmo after numeric or button-driven preview changes.
    /// </summary>
    public void UpdatePreview(Vector3 worldOrigin)
    {
        if (_mesh == null)
        {
            return;
        }

        ShowGizmo(worldOrigin);
    }

    /// <summary>
    /// Starts a drag only when one of the three always-on-top arrow meshes is hit.
    /// </summary>
    public void OnMouseDown(Vector2 screenPosition)
    {
        if (_mesh == null
            || !_scene.TryHitModelTranslateGizmo(screenPosition, out ModelTranslateGizmoHandleKind handleKind))
        {
            return;
        }

        _dragHandle = handleKind;
        _dragStartScreenPosition = screenPosition;
        _dragStartTransform = _mesh.UserTransform;
        _dragStartWorldOrigin = CalculateCurrentWorldOrigin();
    }

    /// <summary>
    /// Projects pointer displacement onto the selected world axis and requests a constrained preview.
    /// </summary>
    public void OnMouseMove(Vector2 screenPosition)
    {
        if (_mesh == null || _dragHandle == ModelTranslateGizmoHandleKind.None)
        {
            return;
        }

        Vector3 axis = GetWorldAxis(_dragHandle);
        float arrowLength = GetArrowLength(_dragHandle);
        Point originScreen = _viewport.Project(new Point3D(
            _dragStartWorldOrigin.X,
            _dragStartWorldOrigin.Y,
            _dragStartWorldOrigin.Z));
        Vector3 arrowEnd = _dragStartWorldOrigin + (axis * arrowLength);
        Point endScreen = _viewport.Project(new Point3D(arrowEnd.X, arrowEnd.Y, arrowEnd.Z));
        Vector2 screenAxis = new Vector2(
            (float)(endScreen.X - originScreen.X),
            (float)(endScreen.Y - originScreen.Y));
        float screenLength = screenAxis.Length();

        if (!float.IsFinite(screenLength) || screenLength <= MinimumScreenAxisLength)
        {
            return;
        }

        float pixelOffset = Vector2.Dot(
            screenPosition - _dragStartScreenPosition,
            screenAxis / screenLength);
        float worldOffset = pixelOffset * arrowLength / screenLength;

        if (!float.IsFinite(worldOffset))
        {
            return;
        }

        Vector3 requestedOrigin = _dragStartWorldOrigin + (axis * worldOffset);

        if (!MeshTranslationTransform.TryCreateUserTransformForWorldOrigin(
                _dragStartTransform,
                _dragStartWorldOrigin,
                requestedOrigin,
                _limits,
                out Transform3DData transform,
                out Vector3 constrainedOrigin))
        {
            return;
        }

        PreviewTransformRequested?.Invoke(transform, constrainedOrigin);
    }

    /// <summary>
    /// Ends the active pointer gesture while retaining the tool-session gizmo.
    /// </summary>
    public void OnMouseUp(Vector2 screenPosition)
    {
        _ = screenPosition;
        _dragHandle = ModelTranslateGizmoHandleKind.None;
    }

    /// <summary>
    /// Drops all transient drag and gizmo state without mutating the model.
    /// </summary>
    public void Cancel()
    {
        _mesh = null;
        _importSpaceOrigin = Vector3.Zero;
        _dragHandle = ModelTranslateGizmoHandleKind.None;
        _dragStartScreenPosition = Vector2.Zero;
        _dragStartTransform = Transform3DData.Identity;
        _dragStartWorldOrigin = Vector3.Zero;
        _arrowLengths = Vector3.Zero;
        _scene.HideModelTranslateGizmo();
    }

    /// <summary>
    /// Calculates the selected model origin from its current preview transform.
    /// </summary>
    private Vector3 CalculateCurrentWorldOrigin()
    {
        if (_mesh == null)
        {
            return Vector3.Zero;
        }

        return MeshTranslationTransform.CalculateWorldOrigin(_mesh, _importSpaceOrigin);
    }

    /// <summary>
    /// Displays all three arrows with their required per-axis model-relative lengths.
    /// </summary>
    private void ShowGizmo(Vector3 worldOrigin)
    {
        _scene.ShowModelTranslateGizmo(worldOrigin, new Vector3(
            SanitizeArrowLength(_arrowLengths.X),
            SanitizeArrowLength(_arrowLengths.Y),
            SanitizeArrowLength(_arrowLengths.Z)));
    }

    /// <summary>
    /// Maps one handle to its unit world axis.
    /// </summary>
    private static Vector3 GetWorldAxis(ModelTranslateGizmoHandleKind handleKind)
    {
        return handleKind switch
        {
            ModelTranslateGizmoHandleKind.XAxis => Vector3.UnitX,
            ModelTranslateGizmoHandleKind.YAxis => Vector3.UnitY,
            ModelTranslateGizmoHandleKind.ZAxis => Vector3.UnitZ,
            _ => Vector3.Zero
        };
    }

    /// <summary>
    /// Gets the displayed arrow length used to convert projected pixels back into world distance.
    /// </summary>
    private float GetArrowLength(ModelTranslateGizmoHandleKind handleKind)
    {
        float length = handleKind switch
        {
            ModelTranslateGizmoHandleKind.XAxis => _arrowLengths.X,
            ModelTranslateGizmoHandleKind.YAxis => _arrowLengths.Y,
            ModelTranslateGizmoHandleKind.ZAxis => _arrowLengths.Z,
            _ => DefaultArrowLength
        };
        return SanitizeArrowLength(length);
    }

    /// <summary>
    /// Keeps degenerate model dimensions interactive.
    /// </summary>
    private static float SanitizeArrowLength(float length)
    {
        return float.IsFinite(length) && length > 0.0f ? length : DefaultArrowLength;
    }
}
