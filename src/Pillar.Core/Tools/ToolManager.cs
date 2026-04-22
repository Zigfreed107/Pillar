// ToolManager.cs
// Owns active viewport tool lifecycle so mode changes cancel transient CAD interaction state in one place.
namespace Pillar.Core.Tools;

/// <summary>
/// Owns the active viewport tool and cancels transient tool state during mode changes.
/// </summary>
public class ToolManager
{
    public ITool? ActiveTool { get; private set; }

    /// <summary>
    /// Activates the requested tool and cancels the previous tool when the mode changes.
    /// </summary>
    public void SetTool(ITool tool)
    {
        if (ReferenceEquals(ActiveTool, tool))
        {
            return;
        }

        ActiveTool?.Cancel();
        ActiveTool = tool;
    }

    /// <summary>
    /// Cancels transient state on the current tool without changing the active mode.
    /// </summary>
    public void CancelActiveTool()
    {
        ActiveTool?.Cancel();
    }
}
