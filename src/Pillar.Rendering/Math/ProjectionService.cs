using HelixToolkit.Wpf.SharpDX;
using System.Numerics;
using System.Windows;

namespace Pillar.Rendering.Math;

/// <summary>
/// Provides methods for projecting screen coordinates into world space within a 3D viewport.
/// </summary>
/// <remarks>This service utilises a viewport to obtain mouse ray information and a workplane to calculate
/// intersections. It is designed to facilitate interactions in 3D environments, such as selecting objects based on
/// mouse input.</remarks>
public class ProjectionService
{
    private readonly Viewport3DX _viewport;
    private readonly Workplane _workplane = new();

    /// <summary>
    /// Initializes a new instance of the ProjectionService class using the specified viewport for 3D rendering
    /// operations.
    /// </summary>
    /// <param name="viewport">The viewport used to render 3D projections. This parameter cannot be null.</param>
    public ProjectionService(Viewport3DX viewport)
    {
        _viewport = viewport;
    }

    /// <summary>
    /// Attempts to convert a 2D screen coordinate to its corresponding 3D world position.
    /// </summary>
    /// <remarks>Use this method to translate user input or UI positions from screen space into world space,
    /// such as for object picking or placing objects in a 3D scene. The conversion may fail if the screen position does
    /// not map to a valid world coordinate.</remarks>
    /// <param name="mousePos">The screen coordinates of the mouse pointer, used to determine the ray for intersection with the workplane.</param>
    /// <param name="worldPoint">When this method returns <see langword="true"/>, contains the 3D world coordinates that correspond to the
    /// specified mouse position. Otherwise, set to <see cref="Vector3.Zero"/>.</param>
    /// <returns><see langword="true"/> if the conversion is successful and the ray intersects the workplane; otherwise, <see
    /// langword="false"/>.</returns>
    public bool TryGetWorldPoint(Point mousePos, out Vector3 worldPoint)
    {
        if (!_viewport.GetMouseRay(mousePos, out var rayOrigin, out var rayDir))
        {
            worldPoint = Vector3.Zero;
            return false;
        }

        return _workplane.IntersectRay(rayOrigin, rayDir, out worldPoint);
    }

    /// <summary>
    /// Attempts to convert a 2D screen coordinate to its corresponding 3D world position.
    /// </summary>
    /// <remarks>Use this method to translate user input or UI positions from screen space into world space,
    /// such as for object picking or placing objects in a 3D scene. The conversion may fail if the screen position does
    /// not map to a valid world coordinate.</remarks>
    /// <param name="screenPosition">The screen-space coordinates, in pixels, to convert. The origin is typically at the top-left corner of the
    /// screen.</param>
    /// <param name="worldPoint">When this method returns <see langword="true"/>, contains the 3D world-space position corresponding to the
    /// specified screen coordinate; otherwise, contains <see cref="Vector3.Zero"/>.</param>
    /// <returns><see langword="true"/> if the conversion succeeds and a valid world position is found; otherwise, <see
    /// langword="false"/>.</returns>
    public bool TryGetWorldPoint(Vector2 screenPosition, out Vector3 worldPoint)
    {
        return TryGetWorldPoint(new Point(screenPosition.X, screenPosition.Y), out worldPoint);
    }
}
