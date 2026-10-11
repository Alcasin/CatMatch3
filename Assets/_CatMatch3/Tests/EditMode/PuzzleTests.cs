using System;
using System.Collections.Generic;
using System.Linq;
using CatMatch3.Core;
using NUnit.Framework;

namespace CatMatch3.Tests.EditMode
{
    public sealed class PuzzleTests
    {
        private static BoardPosition P(int x, int y) { return new BoardPosition(x, y); }
        private static LevelConfiguration Goal(int moves, int orange)
        {
            return new LevelConfiguration("synthetic", "Verification fixture", 7, 7,
                AuthoredLevels.CreatePreviousM4(1).CopyCells(), moves,
                new Dictionary<CatType, int> { { CatType.Orange, orange } }, Array.Empty<BoardPosition>(), 1337);
        }

        private sealed class ForbiddenRandom : IRandomSource
        {
            public int NextInt(int bound) { Assert.Fail("A stable no-reshuffle board must draw no randomness."); return 0; }
        }

        [Test]
        public void DisabledReshuffleLeavesStableBoardProgressAndRandomnessUntouched()
        {
            var board = PrototypeLayout.CreateDeadlockedBoard();
            var before = board.CopyTiles();
            var tracker = new ObjectiveTracker(new Dictionary<CatType, int> { { CatType.Orange, 3 } }, new[] { P(1, 0) });
            var result = BoardResolver.Resolve(board, new ForbiddenRandom(), objectives: tracker, reshufflePolicy: ReshufflePolicy.Disabled);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Waves, Is.Empty);
            Assert.That(result.Reshuffle, Is.Null);
            Assert.That(board.CopyTiles(), Is.EqualTo(before));
            Assert.That(result.FinalObjectives.Collections.Single().Collected, Is.Zero);
            Assert.That(result.FinalObjectives.RemainingMarks, Is.EqualTo(new[] { P(1, 0) }));
        }

        [Test]
        public void GenuineSwapDeadlockFailsWithMovesRemainingAndNoExtraMoveOrShuffle()
        {
            var session = new GameplaySession(PuzzleDiagnostics.NoLegalMoves(), 0);
            var turn = session.TrySwap(P(2, 5), P(2, 6));
            Assert.That(turn.Accepted, Is.True);
            Assert.That(turn.Resolution.Success, Is.True);
            Assert.That(turn.Resolution.WaveCount, Is.EqualTo(1));
            Assert.That(turn.Resolution.Reshuffle, Is.Null);
            var expected = PrototypeLayout.CreateDeadlockedBoard().CopyTiles();
            expected[0, 6] = new Tile(CatType.Orange, StripeOrientation.Horizontal);
            expected[1, 6] = new Tile(CatType.White); expected[2, 6] = new Tile(CatType.Gray); expected[3, 6] = new Tile(CatType.Siamese);
            Assert.That(session.Board.CopyTiles(), Is.EqualTo(expected));
            Assert.That(session.Board.HasLegalMove(), Is.False);
            Assert.That(session.State, Is.EqualTo(GameplayState.Fail));
            Assert.That(session.Failure, Is.EqualTo(GameplayFailure.NoLegalMoves));
            Assert.That(session.RemainingMoves, Is.EqualTo(2));
            Assert.That(session.Progress.Collections.Single().Collected, Is.EqualTo(3));
            Assert.That(session.TrySwap(P(2, 5), P(2, 6)).Accepted, Is.False);
            Assert.That(session.RemainingMoves, Is.EqualTo(2));
            session.Restart();
            Assert.That(session.Failure, Is.EqualTo(GameplayFailure.None));
            var replay = session.TrySwap(P(2, 6), P(2, 5));
            Assert.That(replay.Resolution.Final.CopyTiles(), Is.EqualTo(turn.Resolution.Final.CopyTiles()));
            Assert.That(session.Failure, Is.EqualTo(GameplayFailure.NoLegalMoves));
        }

        [TestCase(1)]
        [TestCase(3)]
        public void ObjectiveCompletionWinsOnADeadlockEvenWhenMovesAreAlsoZero(int moves)
        {
            var session = new GameplaySession(PuzzleDiagnostics.NoLegalMoves(true, moves), 0);
            var turn = session.TrySwap(P(2, 5), P(2, 6));
            Assert.That(turn.Resolution.Reshuffle, Is.Null);
            Assert.That(session.Board.HasLegalMove(), Is.False);
            Assert.That(session.Progress.IsComplete, Is.True);
            Assert.That(session.State, Is.EqualTo(GameplayState.Win));
            Assert.That(session.Failure, Is.EqualTo(GameplayFailure.None));
            Assert.That(session.RemainingMoves, Is.EqualTo(moves - 1));
        }

        [Test]
        public void MoveExhaustionPrecedesDeadlockAsTheFailureExplanation()
        {
            var session = new GameplaySession(PuzzleDiagnostics.NoLegalMoves(false, 1), 0);
            session.TrySwap(P(2, 5), P(2, 6));
            Assert.That(session.State, Is.EqualTo(GameplayState.Fail));
            Assert.That(session.Failure, Is.EqualTo(GameplayFailure.OutOfMoves));
        }

        [Test]
        public void ReversingAnUndirectedCreationSwapPreservesSpecialsProgressAndFutureRefills()
        {
            var source = M3PrototypeLayout.CreateBoard(M3QaScenario.HorizontalCreation);
            var cells = new Tile[49];
            for (int i = 0; i < 49; i++) cells[i] = source.GetTile(P(i % 7, i / 7));
            var config = new LevelConfiguration("direction", "Direction", 7, 7, cells, 4,
                new Dictionary<CatType, int> { { CatType.Orange, 99 } }, new[] { P(1, 0) }, 1337);
            var a = new GameplaySession(config, 0); var b = new GameplaySession(config, 0);
            a.TrySwap(P(1, 0), P(1, 1)); b.TrySwap(P(1, 1), P(1, 0));
            Assert.That(a.Board.CopyTiles(), Is.EqualTo(b.Board.CopyTiles()));
            Assert.That(a.Board.GetTile(P(1, 0)).Stripe, Is.EqualTo(StripeOrientation.Horizontal));
            Assert.That(a.Progress.RemainingMarks, Is.EqualTo(new[] { P(1, 0) }));
            var before = b.Board.CopyTiles();
            var next = PuzzleVerifier.LegalSwaps(a.Board)[0];
            a.TrySwap(next.First, next.Second);
            Assert.That(b.Board.CopyTiles(), Is.EqualTo(before), "Independent branches must not share board state.");
            b.TrySwap(next.Second, next.First);
            Assert.That(a.Board.CopyTiles(), Is.EqualTo(b.Board.CopyTiles()), "Neither branch may consume the other's RNG state.");
            Assert.That(a.Progress.Collections.Single().Collected, Is.EqualTo(b.Progress.Collections.Single().Collected));
            Assert.That(a.Progress.RemainingMarks, Is.EqualTo(b.Progress.RemainingMarks));
        }

        [Test]
        public void ExhaustiveZeroAndOneRouteFixturesAreClassifiedCorrectly()
        {
            var zero = PuzzleVerifier.Verify(PuzzleDiagnostics.NoLegalMoves(false, 1));
            Assert.That(zero.Status, Is.EqualTo(PuzzleVerificationStatus.Unsolvable));
            Assert.That(zero.Exhaustive, Is.True);
            Assert.That(zero.WinningRoutes, Is.Zero);
            var source = PuzzleDiagnostics.NoLegalMoves();
            var unique = new LevelConfiguration("one", "One route", 7, 7, source.CopyCells(), 1,
                new Dictionary<CatType, int> { { CatType.Orange, 5 } }, Array.Empty<BoardPosition>(), 300);
            var proof = PuzzleVerifier.Verify(unique);
            Assert.That(proof.Status, Is.EqualTo(PuzzleVerificationStatus.Unique));
            Assert.That(proof.Exhaustive, Is.True);
            Assert.That(proof.WinningRoutes, Is.EqualTo(1));
            Assert.That(proof.Transitions, Is.EqualTo(4), "All legal unordered pairs must be explored, not eight directions.");
            Assert.That(proof.Routes[0].Single().ToString(), Is.EqualTo("(2,6) <-> (3,6)"));
        }

        [Test]
        public void DistinctWinningSequencesAreCountedAndMultipleStopsWithAnExplicitLowerBound()
        {
            var config = new LevelConfiguration("multi", "Two-step multiple routes", 7, 7,
                AuthoredLevels.CreatePreviousM4(1).CopyCells(), 2,
                new Dictionary<CatType, int> { { CatType.Orange, 3 }, { CatType.Gray, 3 } }, Array.Empty<BoardPosition>(), 1337);
            var proof = PuzzleVerifier.Verify(config);
            Assert.That(proof.Status, Is.EqualTo(PuzzleVerificationStatus.Multiple));
            Assert.That(proof.WinningRoutes, Is.EqualTo(2));
            Assert.That(proof.Exhaustive, Is.False, "Early rejection at two routes is not exhaustive counting.");
            Assert.That(string.Join(";", proof.Routes[0]), Is.Not.EqualTo(string.Join(";", proof.Routes[1])));
            foreach (var route in proof.Routes)
            {
                Assert.That(route.Count, Is.EqualTo(2));
                var session = new GameplaySession(config, 0);
                foreach (var m in route) session.TrySwap(m.First, m.Second);
                Assert.That(session.State, Is.EqualTo(GameplayState.Win));
            }
        }

        [Test]
        public void OneFoundRouteBeforeBudgetExhaustionIsInconclusiveNeverUnique()
        {
            var proof = PuzzleVerifier.Verify(Goal(1, 3), maxTransitions: 1);
            Assert.That(proof.WinningRoutes, Is.EqualTo(1));
            Assert.That(proof.Status, Is.EqualTo(PuzzleVerificationStatus.Inconclusive));
            Assert.That(proof.Exhaustive, Is.False);
            Assert.That(proof.Limitation, Is.Not.Empty);
        }

        [Test]
        public void ReplayWorkAndDepthBudgetsCannotProduceFalseUniqueness()
        {
            Assert.That(PuzzleVerifier.Verify(AuthoredLevels.Create(1), maxReplayedMoves: 1).Status,
                Is.EqualTo(PuzzleVerificationStatus.Inconclusive));
            Assert.That(PuzzleVerifier.Verify(AuthoredLevels.Create(1), maxDepth: 1).Status,
                Is.EqualTo(PuzzleVerificationStatus.Inconclusive));
            var opening = AuthoredLevels.Create(1).CreateBoard(); var before = opening.CopyTiles();
            var moves = PuzzleVerifier.LegalSwaps(opening);
            Assert.That(opening.CopyTiles(), Is.EqualTo(before));
            Assert.That(moves.Select(m => m.ToString()).Distinct().Count(), Is.EqualTo(moves.Count));
        }

        [TestCase(1, 333, 3)]
        [TestCase(2, 2420, 4)]
        public void ProductionPuzzlesHaveExactlyOneExhaustivelyVerifiedWinningSequence(int number, int states, int length)
        {
            var proof = PuzzleVerifier.Verify(AuthoredLevels.Create(number));
            Assert.That(proof.Status, Is.EqualTo(PuzzleVerificationStatus.Unique), proof.Limitation);
            Assert.That(proof.Exhaustive, Is.True);
            Assert.That(proof.ResolutionErrors, Is.Zero);
            Assert.That(proof.WinningRoutes, Is.EqualTo(1));
            Assert.That(proof.States, Is.EqualTo(states));
            Assert.That(proof.Transitions, Is.EqualTo(states - 1));
            Assert.That(proof.Routes.Single().Count, Is.EqualTo(length));
        }

        [Test]
        public void OffRouteLegalSwapResolvesNormallyAndItsRecordedContinuationLoses()
        {
            var session = new GameplaySession(AuthoredLevels.Create(1), 0);
            var first = session.TrySwap(P(1, 0), P(1, 1));
            Assert.That(first.Accepted, Is.True);
            Assert.That(first.Resolution.WaveCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(session.RemainingMoves, Is.EqualTo(2));
            Assert.That(session.State, Is.EqualTo(GameplayState.Active));
            Assert.That(session.TrySwap(P(5, 4), P(5, 5)).Accepted, Is.True);
            Assert.That(session.TrySwap(P(1, 5), P(1, 6)).Accepted, Is.True);
            Assert.That(session.State, Is.EqualTo(GameplayState.Fail));
            Assert.That(session.Failure, Is.EqualTo(GameplayFailure.OutOfMoves));
            Assert.That(session.RemainingMoves, Is.Zero);
            Assert.That(session.Progress.Collections.Select(c => c.Collected), Is.EqualTo(new[] { 3, 6 }));
        }
    }
}
