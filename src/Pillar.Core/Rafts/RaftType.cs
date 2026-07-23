// RaftType.cs
// Identifies the procedural raft shape selected by the user.
namespace Pillar.Core.Rafts;

/// <summary>
/// Lists the supported resin-print raft generation strategies.
/// </summary>
public enum RaftType
{
    Footprint,
    Mesh,
    Feet
}
