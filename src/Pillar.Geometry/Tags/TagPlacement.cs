// TagPlacement.cs
// Carries one closest-point attachment and tangent on a generated raft boundary.
using System.Numerics;

namespace Pillar.Geometry.Tags;

/// <summary>
/// Describes where a tag's tangential axis intersects a raft edge.
/// </summary>
public readonly record struct TagPlacement(Vector3 AttachmentPoint, Vector2 Tangent);
