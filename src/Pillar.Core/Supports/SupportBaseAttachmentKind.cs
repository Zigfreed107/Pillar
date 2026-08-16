// SupportBaseAttachmentKind.cs
// Identifies whether one generated support starts on the build plate or on its owning model.
namespace Pillar.Core.Supports;

/// <summary>
/// Identifies the surface that owns one support's generated base geometry.
/// </summary>
public enum SupportBaseAttachmentKind
{
    BuildPlate = 0,
    Model = 1
}
