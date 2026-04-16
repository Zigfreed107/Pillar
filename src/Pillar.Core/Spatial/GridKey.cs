using System;

namespace Pillar.Core.Spatial
{
    public readonly struct GridKey : IEquatable<GridKey>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public GridKey(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(GridKey other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is GridKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            // Fast hash for 3 ints
            return HashCode.Combine(X, Y, Z);
        }
    }
}