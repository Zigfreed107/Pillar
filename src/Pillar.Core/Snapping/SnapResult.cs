using System.Numerics;

namespace Pillar.Core.Snapping
{
    public readonly struct SnapResult
    {
        public Vector3 Position { get; }
        public SnapType Type { get; }
        public float Distance { get; }

        public SnapResult(Vector3 position, SnapType type, float distance)
        {
            Position = position;
            Type = type;
            Distance = distance;
        }
    }
}