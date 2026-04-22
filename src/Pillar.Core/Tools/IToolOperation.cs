// IToolOperation.cs
// Defines the shared interaction contract for selectable operations owned by viewport tools.
using System.Numerics;

namespace Pillar.Core.Tools;

/// <summary>
/// Defines the viewport interaction contract for an operation running inside a larger tool.
/// </summary>
public interface IToolOperation
{
    /// <summary>
    /// Handles a primary mouse press at the supplied viewport screen position.
    /// </summary>
    void OnMouseDown(Vector2 screenPosition);

    /// <summary>
    /// Handles pointer movement at the supplied viewport screen position.
    /// </summary>
    void OnMouseMove(Vector2 screenPosition);

    /// <summary>
    /// Handles a primary mouse release at the supplied viewport screen position.
    /// </summary>
    void OnMouseUp(Vector2 screenPosition);

    /// <summary>
    /// Cancels transient gesture, preview, or overlay state owned by the operation.
    /// </summary>
    void Cancel();
}
