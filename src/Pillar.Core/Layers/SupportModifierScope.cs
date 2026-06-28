// SupportModifierScope.cs
// Defines whether a support-layer modifier is replayed for a full layer or bound to selected support identities.
namespace Pillar.Core.Layers;

/// <summary>
/// Identifies the support population that a support-layer modifier targets.
/// </summary>
public enum SupportModifierScope
{
    WholeLayer,
    Selection
}
