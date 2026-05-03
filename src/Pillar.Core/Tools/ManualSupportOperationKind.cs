// ManualSupportOperationKind.cs
// Defines the selectable operations available inside the Manual Support tool.
namespace Pillar.Core.Tools;

/// <summary>
/// Identifies the active support creation operation selected within Manual Support mode.
/// </summary>
public enum ManualSupportOperationKind
{
    None,
    Point,
    Line,
    Ring
}
