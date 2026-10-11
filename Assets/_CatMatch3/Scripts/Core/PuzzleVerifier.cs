#if UNITY_EDITOR || CATMATCH3_OFFLINE
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CatMatch3.Core
{
    public readonly struct PuzzleSwap
    {
        public BoardPosition First { get; }
        public BoardPosition Second { get; }
        public PuzzleSwap(BoardPosition a, BoardPosition b)
        {
            if (!Board.IsValidPosition(a) || !Board.IsValidPosition(b) || !a.IsAdjacentTo(b))
                throw new ArgumentException("Puzzle swaps need two valid adjacent cells.");
            bool ordered = a.Y * Board.Width + a.X < b.Y * Board.Width + b.X;
            First = ordered ? a : b; Second = ordered ? b : a;
        }
        public override string ToString() { return First + " <-> " + Second; }
    }

    public enum PuzzleVerificationStatus { Unsolvable, Unique, Multiple, Inconclusive }

    public sealed class PuzzleVerificationResult
    {
        public PuzzleVerificationStatus Status { get; internal set; }
        public bool Exhaustive { get; internal set; }
        public int WinningRoutes => Routes.Count;
        public long States { get; internal set; }
        public long Transitions { get; internal set; }
        public long ReplayedMoves { get; internal set; }
        public int ResolutionErrors { get; internal set; }
        public long ElapsedMilliseconds { get; internal set; }
        public string Limitation { get; internal set; }
        public IReadOnlyList<IReadOnlyList<PuzzleSwap>> Routes => routes.AsReadOnly();
        internal readonly List<IReadOnlyList<PuzzleSwap>> routes = new List<IReadOnlyList<PuzzleSwap>>();
    }

    /// <summary>Development-only bounded route counting. No memoization or merging of different paths.</summary>
    public static class PuzzleVerifier
    {
        public static IReadOnlyList<PuzzleSwap> LegalSwaps(Board board)
        {
            var moves = new List<PuzzleSwap>();
            var original = board.CopyTiles();
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++)
                {
                    var first = new BoardPosition(x, y);
                    if (x + 1 < Board.Width) AddIfLegal(first, new BoardPosition(x + 1, y));
                    if (y + 1 < Board.Height) AddIfLegal(first, new BoardPosition(x, y + 1));
                }
            return moves.AsReadOnly();

            void AddIfLegal(BoardPosition a, BoardPosition b)
            {
                if (new Board(original).TrySwap(a, b).IsValid) moves.Add(new PuzzleSwap(a, b));
            }
        }

        public static PuzzleVerificationResult Verify(LevelConfiguration level, long maxTransitions = 50000,
            long maxReplayedMoves = 500000, int maxMilliseconds = 10000, int maxDepth = 16)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (!level.DevelopmentSeed.HasValue) throw new ArgumentException("Verification requires a fixed level seed.");
            if (maxTransitions < 1 || maxReplayedMoves < 1 || maxMilliseconds < 1 || maxDepth < 1)
                throw new ArgumentOutOfRangeException("Verification budgets must be positive.");
            var result = new PuzzleVerificationResult();
            var clock = Stopwatch.StartNew();
            var path = new List<PuzzleSwap>();
            bool stopped = false;
            bool incomplete = false;
            Visit(new GameplaySession(level, 0));
            result.ElapsedMilliseconds = clock.ElapsedMilliseconds;
            result.Exhaustive = !stopped && !incomplete;
            result.Status = result.WinningRoutes >= 2 ? PuzzleVerificationStatus.Multiple :
                !result.Exhaustive ? PuzzleVerificationStatus.Inconclusive :
                result.WinningRoutes == 1 ? PuzzleVerificationStatus.Unique : PuzzleVerificationStatus.Unsolvable;
            return result;

            void Visit(GameplaySession state)
            {
                result.States++;
                if (state.State == GameplayState.Win)
                {
                    result.routes.Add(Array.AsReadOnly(path.ToArray()));
                    if (result.WinningRoutes == 2) { stopped = true; result.Limitation = "Stopped after two distinct winning routes."; }
                    return;
                }
                if (state.State == GameplayState.Fail) return;
                if (state.State == GameplayState.Error)
                { incomplete = true; result.ResolutionErrors++; result.Limitation = state.Error; return; }
                if (path.Count == maxDepth)
                { incomplete = true; result.Limitation = "Depth budget reached before the level move limit."; return; }
                foreach (var move in LegalSwaps(state.Board))
                {
                    if (stopped) return;
                    if (result.Transitions >= maxTransitions || result.ReplayedMoves + path.Count + 1 > maxReplayedMoves ||
                        clock.ElapsedMilliseconds >= maxMilliseconds)
                    { stopped = true; result.Limitation = "Transition, replay-work or elapsed-time budget exhausted."; return; }
                    // Replay is the complete branch state: Board identities, tracker, moves, terminal
                    // status and the exact xorshift state are reconstructed by the actual session.
                    // No branch shares a mutable board/tracker/RNG, and no approximate engine is used.
                    var branch = new GameplaySession(level, 0);
                    result.Transitions++;
                    try
                    {
                        foreach (var earlier in path)
                        {
                            result.ReplayedMoves++;
                            if (!branch.TrySwap(earlier.First, earlier.Second).Accepted || branch.State == GameplayState.Error)
                                throw new InvalidOperationException("Deterministic prefix replay failed.");
                        }
                        result.ReplayedMoves++;
                        if (!branch.TrySwap(move.First, move.Second).Accepted)
                            throw new InvalidOperationException("Enumerated legal swap was rejected.");
                    }
                    catch (Exception exception)
                    { incomplete = true; result.ResolutionErrors++; result.Limitation = exception.Message; continue; }
                    path.Add(move);
                    Visit(branch);
                    path.RemoveAt(path.Count - 1);
                }
            }
        }
    }
}
#endif
