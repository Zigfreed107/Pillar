using System.Numerics;

namespace Pillar.Core.Snapping
{
    public readonly struct SnapPoint
    {
        public Vector3 Position { get; }
        public SnapType Type { get; }

        public SnapPoint(Vector3 position, SnapType type)
        {
            Position = position;
            Type = type;
        }
    }
}