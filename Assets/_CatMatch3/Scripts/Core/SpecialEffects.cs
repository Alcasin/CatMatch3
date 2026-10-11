using System;
using System.Collections.Generic;

namespace CatMatch3.Core
{
    public readonly struct TileRemoval
    {
        public BoardPosition Position { get; }
        public Tile Tile { get; }
        public bool HitBySpecial { get; }
        internal TileRemoval(BoardPosition position, Tile tile, bool hitBySpecial)
        { Position = position; Tile = tile; HitBySpecial = hitBySpecial; }
    }

    public readonly struct SpecialCreation
    {
        public BoardPosition Position { get; }
        public Tile Tile { get; }
        internal SpecialCreation(BoardPosition position, Tile tile) { Position = position; Tile = tile; }
    }

    public sealed class SpecialActivation
    {
        public BoardPosition Position { get; }
        public Tile Tile { get; }
        public bool IsChained { get; }
        public IReadOnlyList<BoardPosition> Cells { get; }
        internal SpecialActivation(BoardPosition position, Tile tile, bool chained, List<BoardPosition> cells)
        { Position = position; Tile = tile; IsChained = chained; Cells = Array.AsReadOnly(cells.ToArray()); }
    }
}
