namespace Pillar.Core.Tools;

public interface ITool
{
    void OnMouseDown(System.Numerics.Vector2 screenPosition);
    void OnMouseMove(System.Numerics.Vector2 screenPosition);
    void OnMouseUp(System.Numerics.Vector2 screenPosition);
}
