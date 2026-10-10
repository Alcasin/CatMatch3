using System;

namespace CatMatch3.Core
{
    /// <summary>X increases right, Y increases up; (0, 0) is the bottom-left cell.</summary>
    public readonly struct BoardPosition : IEquatable<BoardPosition>
    {
        public int X { get; }
        public int Y { get; }

        public BoardPosition(int x, int y) { X = x; Y = y; }

        public bool IsAdjacentTo(BoardPosition other)
        {
            return Math.Abs((long)X - other.X) + Math.Abs((long)Y - other.Y) == 1;
        }

        public bool Equals(BoardPosition other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return obj is BoardPosition other && Equals(other); }
        public override int GetHashCode() { return unchecked((X * 397) ^ Y); }
        public override string ToString() { return $"({X},{Y})"; }
        public static bool operator ==(BoardPosition a, BoardPosition b) { return a.Equals(b); }
        public static bool operator !=(BoardPosition a, BoardPosition b) { return !a.Equals(b); }
    }
}
