using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CatMatch3.Core
{
    /// <summary>Validated, copied level data. Flat cells use index y * 7 + x, bottom row first.</summary>
    public sealed class LevelConfiguration
    {
        private readonly Tile[,] layout;
        public string Identifier { get; }
        public string DisplayName { get; }
        public int MoveLimit { get; }
        public uint? DevelopmentSeed { get; }
        public IReadOnlyDictionary<CatType, int> CollectionTargets { get; }
        public IReadOnlyList<BoardPosition> PawCells { get; }

        public LevelConfiguration(string identifier, string displayName, int width, int height,
            IReadOnlyList<Tile> cells, int moveLimit, IReadOnlyDictionary<CatType, int> targets,
            IReadOnlyList<BoardPosition> paws, uint? developmentSeed = null)
        {
            if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Level identifier and display name are required.");
            if (width != Board.Width || height != Board.Height || cells == null || cells.Count != 49)
                throw new ArgumentException("Level layout must be 7 by 7 with exactly 49 cells.");
            if (Enum.GetValues(typeof(CatType)).Length != 5)
                throw new InvalidOperationException("CatMatch3 requires exactly five supported cat types.");
            if (moveLimit <= 0) throw new ArgumentException("Level move limit must be positive.");
            if (targets == null || paws == null) throw new ArgumentException("Level objective arrays are required.");
            if (targets.Count == 0 && paws.Count == 0) throw new ArgumentException("Level needs at least one objective.");
            // Reuse the authoritative objective validation, including duplicate/out-of-range paws.
            new ObjectiveTracker(targets, paws);
            layout = new Tile[Board.Width, Board.Height];
            for (int i = 0; i < cells.Count; i++)
            {
                var tile = cells[i];
                if (!Enum.IsDefined(typeof(CatType), tile.Type) || !Enum.IsDefined(typeof(StripeOrientation), tile.Stripe))
                    throw new ArgumentException($"Unsupported tile at cell index {i}.");
                layout[i % Board.Width, i / Board.Width] = tile;
            }
            var opening = new Board(layout);
            if (MatchFinder.Find(opening).HasMatches) throw new ArgumentException("Level opening contains an initial match.");
            if (!opening.HasLegalMove()) throw new ArgumentException("Level opening has no legal swap.");
            Identifier = identifier; DisplayName = displayName; MoveLimit = moveLimit;
            DevelopmentSeed = developmentSeed;
            var copiedTargets = new Dictionary<CatType, int>();
            foreach (var target in targets) copiedTargets.Add(target.Key, target.Value);
            CollectionTargets = new ReadOnlyDictionary<CatType, int>(copiedTargets);
            PawCells = Array.AsReadOnly(new List<BoardPosition>(paws).ToArray());
        }

        public Board CreateBoard() { return new Board(layout); }
        public ObjectiveTracker CreateObjectives() { return new ObjectiveTracker(CollectionTargets, PawCells); }
        public Tile[] CopyCells()
        {
            var result = new Tile[49];
            for (int i = 0; i < result.Length; i++) result[i] = layout[i % 7, i / 7];
            return result;
        }
    }
}
