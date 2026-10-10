using System;
using System.Collections.Generic;

namespace CatMatch3.Core
{
    public static class BoardResolver
    {
        public const int DefaultMaxWaves = 100;
        public static ResolutionResult Resolve(Board board, IRandomSource random,
            int maxWaves = DefaultMaxWaves, int maxShuffleAttempts = BoardShuffler.DefaultMaxAttempts)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (!board.IsFull) throw new InvalidOperationException("Resolution must start with a full board.");
            if (maxWaves < 1) throw new ArgumentOutOfRangeException(nameof(maxWaves));
            if (maxShuffleAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxShuffleAttempts));
            var initial = board.Capture();
            var waves = new List<ResolutionWave>();
            var matches = MatchFinder.Find(board);
            while (matches.HasMatches)
            {
                if (waves.Count == maxWaves)
                    return new ResolutionResult(false, $"Resolution exceeded {maxWaves} waves; matches remain.",
                        waves, initial, board.Capture(), null);
                var before = board.Capture();
                board.ClearCells(matches.Cells);
                var clear = board.Capture();
                var movements = board.ApplyGravity();
                var gravity = board.Capture();
                var spawns = board.Refill(random);
                waves.Add(new ResolutionWave(matches, before, clear, gravity, board.Capture(), movements, spawns));
                matches = MatchFinder.Find(board);
            }

            ReshuffleResult reshuffle = null;
            if (!board.HasLegalMove()) reshuffle = BoardShuffler.TryReshuffle(board, random, maxShuffleAttempts);
            bool success = reshuffle == null || reshuffle.Success;
            return new ResolutionResult(success, success ? null : reshuffle.Error,
                waves, initial, board.Capture(), reshuffle);
        }
    }
}
