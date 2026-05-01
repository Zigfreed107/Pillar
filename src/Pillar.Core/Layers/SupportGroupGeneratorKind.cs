// SupportGroupGeneratorKind.cs
// Identifies whether a support group is plain user-managed geometry or generated from a parametric support tool.
namespace Pillar.Core.Layers;

/// <summary>
/// Identifies the parametric generator that owns a support group, when one exists.
/// </summary>
public enum SupportGroupGeneratorKind
{
    None,
    CircleSupport
}
