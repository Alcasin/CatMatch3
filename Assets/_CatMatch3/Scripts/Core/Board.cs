using System;
using System.Collections.Generic;

namespace CatMatch3.Core
{
    /// <summary>A fixed 7x7 board. Layout arrays are indexed [x, y], bottom-left first.</summary>
    public sealed class Board
    {
        public const int Width = 7;
        public const int Height = 7;
        private readonly Tile[,] cells;
        private readonly bool[,] occupied = new bool[Width, Height];

        public Board(CatType[,] layout) : this(ToTiles(layout)) { }

        public Board(Tile[,] layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (layout.GetLength(0) != Width || layout.GetLength(1) != Height)
                throw new ArgumentException("A board layout must be exactly 7 columns by 7 rows.", nameof(layout));
            cells = (Tile[,])layout.Clone();
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++) occupied[x, y] = true;
        }

        public static bool IsValidPosition(BoardPosition position)
        {
            return position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;
        }

        public CatType GetCell(BoardPosition position)
        {
            return GetTile(position).Type;
        }

        public Tile GetTile(BoardPosition position)
        {
            if (!IsValidPosition(position)) throw new ArgumentOutOfRangeException(nameof(position));
            if (!occupied[position.X, position.Y]) throw new InvalidOperationException("The cell is empty.");
            return cells[position.X, position.Y];
        }

        public bool TryGetCell(BoardPosition position, out CatType type)
        {
            type = default;
            if (!IsValidPosition(position) || !occupied[position.X, position.Y]) return false;
            type = cells[position.X, position.Y].Type;
            return true;
        }

        public bool TryGetTile(BoardPosition position, out Tile tile)
        {
            tile = default;
            if (!IsValidPosition(position) || !occupied[position.X, position.Y]) return false;
            tile = cells[position.X, position.Y];
            return true;
        }

        public bool IsFull
        {
            get { foreach (bool value in occupied) if (!value) return false; return true; }
        }

        public CatType[,] CopyCells()
        {
            if (!IsFull) throw new InvalidOperationException("Use Capture for a board with empty cells.");
            var copy = new CatType[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++) copy[x, y] = cells[x, y].Type;
            return copy;
        }

        /// <summary>Use this full-identity copy when constructing another board containing specials.</summary>
        public Tile[,] CopyTiles()
        {
            if (!IsFull) throw new InvalidOperationException("Use Capture for a board with empty cells.");
            return (Tile[,])cells.Clone();
        }

        public BoardSnapshot Capture()
        {
            var snapshot = new Tile?[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (occupied[x, y]) snapshot[x, y] = cells[x, y];
            return new BoardSnapshot(snapshot);
        }

        public IReadOnlyList<BoardPosition> ClearCells(IReadOnlyList<BoardPosition> positions)
        {
            if (positions == null) throw new ArgumentNullException(nameof(positions));
            foreach (var position in positions)
                if (!IsValidPosition(position)) throw new ArgumentOutOfRangeException(nameof(positions));
            var cleared = new List<BoardPosition>();
            foreach (var position in positions)
            {
                if (!occupied[position.X, position.Y]) continue;
                occupied[position.X, position.Y] = false;
                cleared.Add(position);
            }
            return cleared.AsReadOnly();
        }

        public IReadOnlyList<TileMovement> ApplyGravity()
        {
            var movements = new List<TileMovement>();
            for (int x = 0; x < Width; x++)
            {
                int destination = 0;
                for (int y = 0; y < Height; y++)
                {
                    if (!occupied[x, y]) continue;
                    if (y != destination)
                    {
                        cells[x, destination] = cells[x, y];
                        occupied[x, destination] = true;
                        occupied[x, y] = false;
                        movements.Add(new TileMovement(cells[x, y],
                            new BoardPosition(x, y), new BoardPosition(x, destination)));
                    }
                    destination++;
                }
            }
            return movements.AsReadOnly();
        }

        public IReadOnlyList<TileSpawn> Refill(IRandomSource random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            // Validate compaction before drawing; invalid randomness cannot leave a half-refilled board.
            for (int x = 0; x < Width; x++)
            {
                bool gap = false;
                for (int y = 0; y < Height; y++)
                    if (!occupied[x, y]) gap = true;
                    else if (gap) throw new InvalidOperationException("Apply gravity before refill.");
            }
            var spawns = new List<TileSpawn>();
            for (int x = 0; x < Width; x++)
            {
                int entry = Height;
                for (int y = 0; y < Height; y++)
                    if (!occupied[x, y])
                        spawns.Add(new TileSpawn((CatType)RandomSource.Draw(random, 5),
                            new BoardPosition(x, y), new BoardPosition(x, entry++)));
            }
            foreach (var spawn in spawns)
            {
                cells[spawn.Position.X, spawn.Position.Y] = spawn.Tile;
                occupied[spawn.Position.X, spawn.Position.Y] = true;
            }
            return spawns.AsReadOnly();
        }

        public bool HasLegalMove()
        {
            if (!IsFull) throw new InvalidOperationException("Legal moves require a full board.");
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    var first = new BoardPosition(x, y);
                    if (x + 1 < Width && IsLegalExchange(first, new BoardPosition(x + 1, y))) return true;
                    if (y + 1 < Height && IsLegalExchange(first, new BoardPosition(x, y + 1))) return true;
                }
            return false;
        }

        private bool IsLegalExchange(BoardPosition first, BoardPosition second)
        {
            if (GetCell(first) == GetCell(second)) return false;
            Exchange(first, second);
            try
            {
                var matches = MatchFinder.Find(this);
                return matches.Contains(first) || matches.Contains(second);
            }
            finally { Exchange(first, second); }
        }

        internal void ReplaceWith(Board replacement)
        {
            Array.Copy(replacement.cells, cells, cells.Length);
            Array.Copy(replacement.occupied, occupied, occupied.Length);
        }

        internal void SetTile(BoardPosition position, Tile tile)
        {
            if (!IsValidPosition(position)) throw new ArgumentOutOfRangeException(nameof(position));
            if (!occupied[position.X, position.Y]) throw new InvalidOperationException("Cannot replace an empty cell.");
            cells[position.X, position.Y] = tile;
        }

        private static Tile[,] ToTiles(CatType[,] layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (layout.GetLength(0) != Width || layout.GetLength(1) != Height)
                throw new ArgumentException("A board layout must be exactly 7 columns by 7 rows.", nameof(layout));
            var tiles = new Tile[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    if (!Enum.IsDefined(typeof(CatType), layout[x, y]))
                        throw new ArgumentException("Layout contains an unknown cat type.", nameof(layout));
                    tiles[x, y] = new Tile(layout[x, y]);
                }
            return tiles;
        }

        public SwapResult TrySwap(BoardPosition first, BoardPosition second)
        {
            if (!IsValidPosition(first) || !IsValidPosition(second))
                return Rejected(SwapStatus.OutOfBounds);
            if (!first.IsAdjacentTo(second)) return Rejected(SwapStatus.NotAdjacent);
            if (!IsFull) return Rejected(SwapStatus.NoMatch);
            // Equal tiles cannot create a new match, even on a board with existing matches.
            if (GetCell(first) == GetCell(second)) return Rejected(SwapStatus.NoMatch);

            Exchange(first, second);
            var matches = MatchFinder.Find(this);
            if (matches.Contains(first) || matches.Contains(second))
                return new SwapResult(SwapStatus.Accepted, matches);

            Exchange(first, second);
            return Rejected(SwapStatus.NoMatch);
        }

        private static SwapResult Rejected(SwapStatus status)
        {
            return new SwapResult(status, MatchResult.Empty);
        }

        private void Exchange(BoardPosition first, BoardPosition second)
        {
            var original = cells[first.X, first.Y];
            cells[first.X, first.Y] = cells[second.X, second.Y];
            cells[second.X, second.Y] = original;
        }
    }
}
