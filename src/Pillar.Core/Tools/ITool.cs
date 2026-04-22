// ITool.cs
// Defines the viewport interaction contract used by workspace modes without depending on WPF or rendering UI.
namespace Pillar.Core.Tools;

/// <summary>
/// Defines the viewport interaction contract used by workspace modes.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Handles a primary mouse press at the supplied viewport screen position.
    /// </summary>
    void OnMouseDown(System.Numerics.Vector2 screenPosition);

    /// <summary>
    /// Handles pointer movement at the supplied viewport screen position.
    /// </summary>
    void OnMouseMove(System.Numerics.Vector2 screenPosition);

    /// <summary>
    /// Handles a primary mouse release at the supplied viewport screen position.
    /// </summary>
    void OnMouseUp(System.Numerics.Vector2 screenPosition);

    /// <summary>
    /// Cancels transient gesture, preview, or overlay state owned by the tool.
    /// </summary>
    void Cancel();
}
