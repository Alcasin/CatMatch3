using System;

namespace CatMatch3.Core
{
    /// <summary>Immutable presentation snapshot; null means an empty cell.</summary>
    public sealed class BoardSnapshot
    {
        private readonly CatType?[,] cells;
        internal BoardSnapshot(CatType?[,] cells) { this.cells = (CatType?[,])cells.Clone(); }
        public CatType? GetCell(BoardPosition position)
        {
            if (!Board.IsValidPosition(position)) throw new ArgumentOutOfRangeException(nameof(position));
            return cells[position.X, position.Y];
        }
        public CatType?[,] CopyCells() { return (CatType?[,])cells.Clone(); }
    }

    public readonly struct TileMovement
    {
        public CatType Type { get; }
        public BoardPosition From { get; }
        public BoardPosition To { get; }
        internal TileMovement(CatType type, BoardPosition from, BoardPosition to)
        { Type = type; From = from; To = to; }
    }

    public readonly struct TileSpawn
    {
        public CatType Type { get; }
        public BoardPosition Position { get; }
        public BoardPosition EntryPosition { get; }
        internal TileSpawn(CatType type, BoardPosition position, BoardPosition entryPosition)
        { Type = type; Position = position; EntryPosition = entryPosition; }
    }
}
