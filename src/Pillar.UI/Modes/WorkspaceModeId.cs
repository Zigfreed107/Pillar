// WorkspaceModeId.cs
// Defines stable identifiers for UI workspace modes so toolbar, tools, and overlays can stay synchronized.
namespace Pillar.UI.Modes;

/// <summary>
/// Identifies the high-level application mode selected from the mode toolbar.
/// </summary>
public enum WorkspaceModeId
{
    Select,
    Line,
    Transform,
    ManualSupport
}
