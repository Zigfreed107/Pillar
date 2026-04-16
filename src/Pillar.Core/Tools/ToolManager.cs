namespace Pillar.Core.Tools;

public class ToolManager
{
    public ITool? ActiveTool { get; private set; }

    public void SetTool(ITool tool)
    {
        ActiveTool = tool;
    }
}