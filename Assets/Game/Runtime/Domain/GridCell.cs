using System;

namespace GreatVoyage.Domain
{
    public readonly struct GridCell : IEquatable<GridCell>
    {
        public int X { get; }
        public int Y { get; }
        public GridCell(int x, int y) { X = x; Y = y; }
        public bool Equals(GridCell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridCell other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
    }

    public readonly struct GridRect
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public GridRect(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0 || (long)width * height > 1000000
                || (long)x + width > int.MaxValue || (long)y + height > int.MaxValue)
                throw new ArgumentException("Invalid placement rectangle.");
            X = x; Y = y; Width = width; Height = height;
        }
    }
}
