// RotationCoordinateSpace.cs
// Defines how Transform Rotate input axes are interpreted without introducing UI or rendering dependencies.
namespace Pillar.Core.Entities;

/// <summary>
/// Identifies whether a rotation delta uses fixed scene axes or the model's changing axes.
/// </summary>
public enum RotationCoordinateSpace
{
    World,
    Local
}
