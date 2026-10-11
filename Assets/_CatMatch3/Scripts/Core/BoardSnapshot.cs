using System;

namespace CatMatch3.Core
{
    /// <summary>Immutable presentation snapshot; null means an empty cell.</summary>
    public sealed class BoardSnapshot
    {
        private readonly Tile?[,] cells;
        internal BoardSnapshot(Tile?[,] cells) { this.cells = (Tile?[,])cells.Clone(); }
        public CatType? GetCell(BoardPosition position)
        {
            return GetTile(position)?.Type;
        }
        public Tile? GetTile(BoardPosition position)
        {
            if (!Board.IsValidPosition(position)) throw new ArgumentOutOfRangeException(nameof(position));
            return cells[position.X, position.Y];
        }
        public Tile?[,] CopyTiles() { return (Tile?[,])cells.Clone(); }
        public CatType?[,] CopyCells()
        {
            var copy = new CatType?[Board.Width, Board.Height];
            for (int x = 0; x < Board.Width; x++)
                for (int y = 0; y < Board.Height; y++) copy[x, y] = cells[x, y]?.Type;
            return copy;
        }
    }

    public readonly struct TileMovement
    {
        public Tile Tile { get; }
        public CatType Type => Tile.Type;
        public BoardPosition From { get; }
        public BoardPosition To { get; }
        internal TileMovement(Tile tile, BoardPosition from, BoardPosition to)
        { Tile = tile; From = from; To = to; }
    }

    public readonly struct TileSpawn
    {
        public Tile Tile { get; }
        public CatType Type => Tile.Type;
        public BoardPosition Position { get; }
        public BoardPosition EntryPosition { get; }
        internal TileSpawn(CatType type, BoardPosition position, BoardPosition entryPosition)
        { Tile = new Tile(type); Position = position; EntryPosition = entryPosition; }
    }
}
