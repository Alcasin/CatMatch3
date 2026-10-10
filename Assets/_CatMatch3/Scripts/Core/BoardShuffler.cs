using System;

namespace CatMatch3.Core
{
    public static class BoardShuffler
    {
        public const int DefaultMaxAttempts = 256;
        public static ReshuffleResult TryReshuffle(Board board, IRandomSource random,
            int maxAttempts = DefaultMaxAttempts)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (!board.IsFull) throw new InvalidOperationException("Reshuffling requires a full board.");
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            var before = board.Capture();
            var original = new CatType[Board.Width * Board.Height];
            var counts = new int[5];
            for (int x = 0; x < Board.Width; x++)
                for (int y = 0; y < Board.Height; y++)
                {
                    var type = board.GetCell(new BoardPosition(x, y));
                    original[x * Board.Height + y] = type;
                    counts[(int)type]++;
                }
            // Each seven-cell row can contain at most five of one type without a triple.
            foreach (int count in counts)
                if (count > 5 * Board.Height)
                    return new ReshuffleResult(false, "Tile counts cannot form a match-free board.", 0, before, before);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var shuffled = (CatType[])original.Clone();
                for (int i = shuffled.Length - 1; i > 0; i--)
                {
                    int j = RandomSource.Draw(random, i + 1);
                    var type = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = type;
                }
                var layout = new CatType[Board.Width, Board.Height];
                for (int x = 0; x < Board.Width; x++)
                    for (int y = 0; y < Board.Height; y++) layout[x, y] = shuffled[x * Board.Height + y];
                var candidate = new Board(layout);
                if (MatchFinder.Find(candidate).HasMatches || !candidate.HasLegalMove()) continue;
                // Only commit a verified candidate; exhausted attempts leave the original untouched.
                board.ReplaceWith(candidate);
                return new ReshuffleResult(true, null, attempt, before, board.Capture());
            }
            return new ReshuffleResult(false, $"No stable playable reshuffle found in {maxAttempts} attempts.",
                maxAttempts, before, before);
        }
    }
}
