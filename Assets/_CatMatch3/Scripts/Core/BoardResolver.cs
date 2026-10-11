using System;
using System.Collections.Generic;

namespace CatMatch3.Core
{
    public static class BoardResolver
    {
        public const int DefaultMaxWaves = 100;
        public static ResolutionResult Resolve(Board board, IRandomSource random,
            int maxWaves = DefaultMaxWaves, int maxShuffleAttempts = BoardShuffler.DefaultMaxAttempts,
            ObjectiveTracker objectives = null, BoardPosition? swapDestination = null)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (!board.IsFull) throw new InvalidOperationException("Resolution must start with a full board.");
            if (maxWaves < 1) throw new ArgumentOutOfRangeException(nameof(maxWaves));
            if (maxShuffleAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxShuffleAttempts));
            if (swapDestination.HasValue && !Board.IsValidPosition(swapDestination.Value))
                throw new ArgumentOutOfRangeException(nameof(swapDestination));
            var initial = board.Capture();
            var initialObjectives = objectives?.Capture();
            var waves = new List<ResolutionWave>();
            var matches = MatchFinder.Find(board);
            while (matches.HasMatches)
            {
                if (waves.Count == maxWaves)
                    return new ResolutionResult(false, $"Resolution exceeded {maxWaves} waves; matches remain.",
                        waves, initial, board.Capture(), null, initialObjectives, objectives?.Capture());
                var before = board.Capture();
                var removals = ResolveMatches(board, matches, waves.Count == 0 ? swapDestination : null,
                    out var creations, out var activations);
                objectives?.RecordRemovals(removals);
                var clear = board.Capture();
                var movements = board.ApplyGravity();
                var gravity = board.Capture();
                var spawns = board.Refill(random);
                waves.Add(new ResolutionWave(matches, before, clear, gravity, board.Capture(), movements, spawns,
                    removals, creations, activations, objectives?.Capture()));
                matches = MatchFinder.Find(board);
            }

            ReshuffleResult reshuffle = null;
            if (!board.HasLegalMove()) reshuffle = BoardShuffler.TryReshuffle(board, random, maxShuffleAttempts);
            bool success = reshuffle == null || reshuffle.Success;
            return new ResolutionResult(success, success ? null : reshuffle.Error,
                waves, initial, board.Capture(), reshuffle, initialObjectives, objectives?.Capture());
        }

        private static List<TileRemoval> ResolveMatches(Board board, MatchResult matches,
            BoardPosition? destination, out List<SpecialCreation> creations, out List<SpecialActivation> activations)
        {
            var affected = new List<BoardPosition>(matches.Cells);
            var unique = new HashSet<BoardPosition>(matches.Cells);
            var effectCells = new HashSet<BoardPosition>();
            var pending = new Queue<BoardPosition>();
            var queued = new HashSet<BoardPosition>();
            foreach (var cell in matches.Cells)
                if (board.GetTile(cell).IsSpecial && queued.Add(cell)) pending.Enqueue(cell);

            activations = new List<SpecialActivation>();
            // Breadth-first, matching seeds first, then hits in left-to-right / bottom-to-top order.
            // Queued positions can never be queued again, so even intersecting chains terminate.
            while (pending.Count > 0)
            {
                var origin = pending.Dequeue();
                var tile = board.GetTile(origin);
                var line = new List<BoardPosition>();
                for (int i = 0; i < Board.Width; i++)
                {
                    var cell = tile.Stripe == StripeOrientation.Horizontal
                        ? new BoardPosition(i, origin.Y) : new BoardPosition(origin.X, i);
                    line.Add(cell);
                    effectCells.Add(cell);
                    if (unique.Add(cell)) affected.Add(cell);
                    if (board.GetTile(cell).IsSpecial && queued.Add(cell)) pending.Enqueue(cell);
                }
                activations.Add(new SpecialActivation(origin, tile, !matches.Contains(origin), line));
            }

            creations = new List<SpecialCreation>();
            var anchors = new HashSet<BoardPosition>();
            // MatchFinder returns horizontal runs before vertical runs. A shared anchor goes to
            // the first run; different anchors can both survive. Effects always defeat creation.
            foreach (var group in matches.Groups)
            {
                if (group.Length < 4) continue;
                bool hasExistingSpecial = false;
                var anchor = group.Cells[0]; // Leftmost horizontal / bottommost vertical.
                foreach (var cell in group.Cells)
                {
                    hasExistingSpecial |= board.GetTile(cell).IsSpecial;
                    if (destination.HasValue && cell == destination.Value) anchor = cell;
                }
                if (hasExistingSpecial || effectCells.Contains(anchor) || !anchors.Add(anchor)) continue;
                var orientation = group.Orientation == MatchOrientation.Horizontal
                    ? StripeOrientation.Horizontal : StripeOrientation.Vertical;
                creations.Add(new SpecialCreation(anchor, new Tile(group.CatType, orientation)));
            }

            var removals = new List<TileRemoval>();
            var clearedCells = new List<BoardPosition>();
            foreach (var cell in affected)
            {
                if (anchors.Contains(cell)) continue;
                removals.Add(new TileRemoval(cell, board.GetTile(cell), effectCells.Contains(cell)));
                clearedCells.Add(cell);
            }
            board.ClearCells(clearedCells);
            foreach (var creation in creations) board.SetTile(creation.Position, creation.Tile);
            return removals;
        }
    }
}
