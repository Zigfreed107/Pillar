// SupportModifierKind.cs
// Lists renderer-independent modifier operations that can transform generated support-layer output.
namespace Pillar.Core.Layers;

/// <summary>
/// Identifies the editing operation stored by one support-layer modifier definition.
/// </summary>
public enum SupportModifierKind
{
    Cluster,
    Brace,
    Buttress,
    DirectEdit,
    Delete
}
