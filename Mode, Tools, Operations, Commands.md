
The naming pattern used is:

Mode
  -> Tool
      -> Operation
          -> Command
For Manual Support:

ManualSupportModeOverlay
ManualSupportTool
ManualSupportOperationKind
IToolOperation
PointSupportOperation
LineSupportOperation
CircleSupportOperation
AddSupportCommand
Where each concept means:

Mode
The workflow area the user has entered, mostly UI-level: Select, Transform, Manual Support.

Tool
The active viewport input controller for that mode. It receives mouse events from ToolManager.

GUI

Layer Panel
Mode Panel 
    -Tabs (Transform, Supprot, ..
        - Buttons to select Tool
ToolOptionsPanel (settings for the tool)

Operation
The selected behavior inside the tool. This is what the overlay buttons choose.

Command
The undoable document change after the operation has enough information to commit.

So in code I’d eventually expect something like:

public enum ManualSupportOperationKind
{
    None,
    Point,
    Line,
    Circle
}
public interface IToolOperation
{
    void OnMouseDown(Vector2 screenPosition);
    void OnMouseMove(Vector2 screenPosition);
    void OnMouseUp(Vector2 screenPosition);
    void Cancel();
}
public sealed class ManualSupportTool : ITool
{
    private IToolOperation? _activeOperation;

    public void SetActiveOperation(ManualSupportOperationKind operationKind)
    {
        _activeOperation?.Cancel();

        // Create or select PointSupportOperation, LineSupportOperation, etc.
    }
}
The main pitfall to avoid is letting the overlay button directly create supports. The overlay should only say, “make Point Support operation active.” The operation handles click/preview state, and the command performs the final undoable document mutation.