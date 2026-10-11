using System;
using System.Collections.Generic;
using System.Linq;
using CatMatch3.Core;
using NUnit.Framework;

namespace CatMatch3.Tests.EditMode
{
    public sealed class GameplaySessionTests
    {
        private static BoardPosition P(int x, int y) { return new BoardPosition(x, y); }
        private static Tile[] Flatten(Board board)
        {
            var cells = new Tile[49];
            for (int i = 0; i < cells.Length; i++) cells[i] = board.GetTile(P(i % 7, i / 7));
            return cells;
        }
        private static LevelConfiguration Fixture(int moves = 4, int orange = 99, uint? seed = 1337,
            M3QaScenario scenario = M3QaScenario.Original, BoardPosition[] paws = null)
        {
            return new LevelConfiguration("test", "Session fixture", 7, 7, Flatten(M3PrototypeLayout.CreateBoard(scenario)),
                moves, orange > 0 ? new Dictionary<CatType, int> { { CatType.Orange, orange } } : new Dictionary<CatType, int>(),
                paws ?? Array.Empty<BoardPosition>(), seed);
        }
        private static GameplayTurn Opening(GameplaySession session) { return session.TrySwap(P(1, 0), P(1, 1)); }

        [TestCase(1, 3, "OBOGSOB", "WGSOBGG")]
        [TestCase(2, 4, "OBOGSOB", "BWGSOBW")]
        public void AuthoredLevelsInitializeExactRowsObjectivesAndPlayableBoards(int number, int moves, string bottom, string top)
        {
            var level = AuthoredLevels.Create(number);
            var session = new GameplaySession(level, 0);
            const string symbols = "OBWGS";
            for (int x = 0; x < 7; x++)
            {
                Assert.That(session.Board.GetCell(P(x, 0)), Is.EqualTo((CatType)symbols.IndexOf(bottom[x])));
                Assert.That(session.Board.GetCell(P(x, 6)), Is.EqualTo((CatType)symbols.IndexOf(top[x])));
            }
            Assert.That(MatchFinder.Find(session.Board).HasMatches, Is.False);
            Assert.That(session.Board.HasLegalMove(), Is.True);
            Assert.That(session.Board.CopyTiles().Cast<Tile>().All(t => !t.IsSpecial), Is.True);
            Assert.That(session.RemainingMoves, Is.EqualTo(moves));
            Assert.That(session.State, Is.EqualTo(GameplayState.Active));
            Assert.That(session.Progress.IsComplete, Is.False);
            if (number == 1)
            {
                Assert.That(session.Progress.Collections.Select(c => c.Target), Is.EqualTo(new[] { 6, 9 }));
                Assert.That(session.Progress.Collections.All(c => c.Collected == 0), Is.True);
                Assert.That(session.Progress.TotalPaws, Is.Zero);
            }
            else
            {
                Assert.That(session.Progress.Collections, Is.Empty);
                Assert.That(session.Progress.RemainingMarks, Is.EqualTo(new[] { P(0,0), P(1,0), P(0,1), P(1,1), P(0,2), P(1,2), P(3,3), P(3,4) }));
                Assert.That(session.Progress.ClearedPaws, Is.Zero);
            }
        }

        [Test]
        public void InvalidSwapCostsNothingAndDoesNotAdvanceRandomnessOrProgress()
        {
            var level = Fixture();
            var a = new GameplaySession(level, 0);
            var b = new GameplaySession(level, 0);
            var before = a.Board.CopyTiles();
            var rejected = a.TrySwap(P(3, 0), P(4, 0));
            Assert.That(rejected.Accepted, Is.False);
            Assert.That(rejected.Resolution, Is.Null);
            Assert.That(a.Board.CopyTiles(), Is.EqualTo(before));
            Assert.That(a.RemainingMoves, Is.EqualTo(4));
            Assert.That(a.Progress.Collections.Single().Collected, Is.Zero);
            Assert.That(a.State, Is.EqualTo(GameplayState.Active));
            Opening(a); Opening(b);
            Assert.That(a.Board.CopyTiles(), Is.EqualTo(b.Board.CopyTiles()));
        }

        [TestCase(1337u, 1)]
        [TestCase(141u, 3)]
        public void AcceptedSwapSpendsExactlyOneMoveRegardlessOfCascadeCount(uint seed, int waves)
        {
            var session = new GameplaySession(Fixture(seed: seed), 0);
            var turn = Opening(session);
            Assert.That(turn.Accepted, Is.True);
            Assert.That(turn.Resolution.Success, Is.True, turn.Resolution.Error);
            Assert.That(turn.Resolution.WaveCount, Is.EqualTo(waves));
            Assert.That(session.RemainingMoves, Is.EqualTo(3));
            Assert.That(session.State, Is.EqualTo(GameplayState.Active));
        }

        [TestCase(M3QaScenario.HorizontalCreation, 0, 1)]
        [TestCase(M3QaScenario.Chain, 3, 0)]
        public void SpecialCreationAndChainedActivationCostNoExtraMoves(M3QaScenario scenario, int activations, int creations)
        {
            var session = new GameplaySession(Fixture(scenario: scenario), 0);
            var turn = session.TrySwap(P(1, 1), P(1, 0));
            Assert.That(turn.Resolution.Success, Is.True, turn.Resolution.Error);
            Assert.That(turn.Resolution.Waves[0].Activations.Count, Is.EqualTo(activations));
            Assert.That(turn.Resolution.Waves[0].Creations.Count, Is.EqualTo(creations));
            Assert.That(session.RemainingMoves, Is.EqualTo(3));
        }

        [Test]
        public void EveryCollectionTargetIsRequiredAndProgressComesOnlyFromResolver()
        {
            var source = Fixture();
            var level = new LevelConfiguration("both", "Both collections", 7, 7, source.CopyCells(), 4,
                new Dictionary<CatType, int> { { CatType.Orange, 3 }, { CatType.Gray, 1 } }, Array.Empty<BoardPosition>(), 1337);
            var session = new GameplaySession(level, 0);
            var turn = Opening(session);
            Assert.That(turn.Resolution.FinalObjectives.Collections.Select(c => c.Collected), Is.EqualTo(new[] { 3, 0 }));
            Assert.That(session.Progress.IsComplete, Is.False);
            Assert.That(session.State, Is.EqualTo(GameplayState.Active));
            var stripe = new GameplaySession(new LevelConfiguration("both-stripe", "Both through stripe", 7, 7,
                Flatten(M3PrototypeLayout.CreateBoard(M3QaScenario.HorizontalActivation)), 4,
                level.CollectionTargets, Array.Empty<BoardPosition>(), 1337), 0);
            stripe.TrySwap(P(1, 1), P(1, 0));
            Assert.That(stripe.Progress.Collections.Select(c => c.Collected), Is.EqualTo(new[] { 3, 1 }));
            Assert.That(stripe.State, Is.EqualTo(GameplayState.Win));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AllPawCellsAreRequiredAndRemainFixedUntilRemoved(bool includeUncleared)
        {
            var paws = new List<BoardPosition> { P(0, 0), P(1, 0), P(2, 0) };
            if (includeUncleared) paws.Add(P(6, 6));
            var session = new GameplaySession(Fixture(orange: 0, paws: paws.ToArray()), 0);
            Opening(session);
            Assert.That(session.Progress.ClearedPaws, Is.EqualTo(3));
            Assert.That(session.Progress.RemainingMarks, Is.EqualTo(includeUncleared ? new[] { P(6, 6) } : Array.Empty<BoardPosition>()));
            Assert.That(session.State, Is.EqualTo(includeUncleared ? GameplayState.Active : GameplayState.Win));
        }

        [Test]
        public void SurvivingCreationAnchorGivesNeitherCollectionNorPawCredit()
        {
            var session = new GameplaySession(Fixture(orange: 4, scenario: M3QaScenario.HorizontalCreation, paws: new[] { P(1, 0) }), 0);
            session.TrySwap(P(1, 1), P(1, 0));
            Assert.That(session.Progress.Collections.Single().Collected, Is.EqualTo(3));
            Assert.That(session.Progress.ClearedPaws, Is.Zero);
            Assert.That(session.State, Is.EqualTo(GameplayState.Active));
        }

        [TestCase(3, GameplayState.Win)]
        [TestCase(99, GameplayState.Fail)]
        public void TerminalStatesRejectRepeatedActionsWithoutNegativeMoves(int target, GameplayState expected)
        {
            var session = new GameplaySession(Fixture(moves: 1, orange: target), 0);
            Opening(session);
            Assert.That(session.State, Is.EqualTo(expected));
            var before = session.Board.CopyTiles();
            var progress = session.Progress.Collections.Single().Collected;
            for (int i = 0; i < 10; i++) Assert.That(Opening(session).Accepted, Is.False);
            Assert.That(session.RemainingMoves, Is.Zero);
            Assert.That(session.Board.CopyTiles(), Is.EqualTo(before));
            Assert.That(session.Progress.Collections.Single().Collected, Is.EqualTo(progress));
        }

        [Test]
        public void FinalMoveWinIsEvaluatedAfterTheLastCascadeBeforeFail()
        {
            var session = new GameplaySession(Fixture(moves: 1, orange: 6, seed: 141), 0);
            var turn = Opening(session);
            Assert.That(turn.Resolution.WaveCount, Is.EqualTo(3));
            Assert.That(turn.Resolution.Waves.Take(2).All(w => !w.ObjectivesAfter.IsComplete), Is.True);
            Assert.That(turn.Resolution.Waves[2].ObjectivesAfter.IsComplete, Is.True);
            Assert.That(session.RemainingMoves, Is.Zero);
            Assert.That(session.State, Is.EqualTo(GameplayState.Win));
            Assert.That(MatchFinder.Find(session.Board).HasMatches, Is.False);
        }

        [TestCase(1337u)]
        [TestCase(null)]
        public void RestartRestoresAuthoredTilesMovesPawsProgressAndTheChosenRandomStream(uint? developmentSeed)
        {
            var config = Fixture(seed: developmentSeed, paws: new[] { P(1, 0), P(6, 6) });
            var session = new GameplaySession(config, 141);
            var openingBoard = session.Board.CopyTiles();
            var first = Opening(session);
            var final = session.Board.CopyTiles();
            var oldProgress = session.Progress;
            session.Restart();
            Assert.That(session.Board.CopyTiles(), Is.EqualTo(openingBoard));
            Assert.That(session.RemainingMoves, Is.EqualTo(4));
            Assert.That(session.Progress.Collections.Single().Collected, Is.Zero);
            Assert.That(session.Progress.RemainingMarks, Is.EqualTo(config.PawCells));
            Assert.That(session.State, Is.EqualTo(GameplayState.Active));
            Assert.That(Opening(session).Resolution.WaveCount, Is.EqualTo(first.Resolution.WaveCount));
            Assert.That(session.Board.CopyTiles(), Is.EqualTo(final));
            Assert.That(oldProgress.ClearedPaws, Is.EqualTo(1));
            Assert.That(oldProgress.Collections.Single().Collected, Is.GreaterThan(0));
        }

        [Test]
        public void EnteringAnotherLevelDoesNotRetainProgressOrRandomState()
        {
            var first = new GameplaySession(AuthoredLevels.Create(1), 0);
            Opening(first);
            var second = new GameplaySession(AuthoredLevels.Create(2), 0);
            Assert.That(second.Progress.ClearedPaws, Is.Zero);
            Assert.That(second.RemainingMoves, Is.EqualTo(4));
            Assert.That(second.Progress.Collections, Is.Empty);
            var replay = new GameplaySession(AuthoredLevels.Create(2), 999);
            Opening(second); Opening(replay);
            Assert.That(second.Board.CopyTiles(), Is.EqualTo(replay.Board.CopyTiles()));
            Assert.That(first.Progress.Collections.First().Collected, Is.EqualTo(3));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void AuthoredLevelsHaveReproducibleOrdinaryWinningRoutes(int number)
        {
            // Exhaustively verified puzzle routes, without diagnostic shortcuts.
            int[,] route = number == 1 ? new[,] {
                {2,0,2,1}, {4,5,4,6}, {4,5,4,6}
            } : new[,] {
                {0,1,1,1}, {1,3,1,4}, {2,3,2,4}, {0,3,1,3}
            };
            var session = new GameplaySession(AuthoredLevels.Create(number), 0);
            for (int i = 0; i < route.GetLength(0); i++)
            {
                Assert.That(session.State, Is.EqualTo(GameplayState.Active));
                var turn = session.TrySwap(P(route[i,0], route[i,1]), P(route[i,2], route[i,3]));
                Assert.That(turn.Accepted, Is.True, "Route move " + (i + 1));
                Assert.That(turn.Resolution.Success, Is.True, turn.Resolution.Error);
                Assert.That(session.Board.IsFull && session.Board.HasLegalMove(), Is.True);
                Assert.That(MatchFinder.Find(session.Board).HasMatches, Is.False);
            }
            Assert.That(session.State, Is.EqualTo(GameplayState.Win));
            Assert.That(session.Progress.IsComplete, Is.True);
            Assert.That(session.RemainingMoves, Is.Zero);
        }

        [TestCase(3)]
        [TestCase(99)]
        public void RestartLeavesEitherTerminalStateAndReplaysTheFirstTurn(int orange)
        {
            var session = new GameplaySession(Fixture(moves: 1, orange: orange), 0);
            Opening(session);
            var terminal = session.State;
            Assert.That(terminal, Is.Not.EqualTo(GameplayState.Active));
            session.Restart();
            Assert.That(session.RemainingMoves, Is.EqualTo(1));
            Assert.That(session.State, Is.EqualTo(GameplayState.Active));
            Assert.That(session.Progress.Collections.Single().Collected, Is.Zero);
            Assert.That(Opening(session).Accepted, Is.True);
            Assert.That(session.State, Is.EqualTo(terminal));
        }

        [Test]
        public void ConfigurationOwnsItsDataAndEveryBoardInstanceIsIndependent()
        {
            var cells = AuthoredLevels.Create(1).CopyCells();
            var targets = new Dictionary<CatType, int> { { CatType.Orange, 3 } };
            var paws = new List<BoardPosition> { P(0, 0) };
            var config = new LevelConfiguration("copy", "Copies", 7, 7, cells, 4, targets, paws, 1337);
            cells[0] = new Tile(CatType.Black); targets.Clear(); paws.Clear();
            var first = config.CreateBoard(); first.TrySwap(P(1, 0), P(1, 1));
            Assert.That(config.CreateBoard().GetCell(P(0, 0)), Is.EqualTo(CatType.Orange));
            Assert.That(config.CreateObjectives().Capture().Collections.Single().Target, Is.EqualTo(3));
            Assert.That(config.PawCells, Is.EqualTo(new[] { P(0, 0) }));
            Assert.Throws<NotSupportedException>(() => ((IList<BoardPosition>)config.PawCells).Clear());
        }

        [TestCase("dimensions")]
        [TestCase("cells")]
        [TestCase("moves")]
        [TestCase("identity")]
        [TestCase("objectives")]
        [TestCase("target-type")]
        [TestCase("target-count")]
        [TestCase("duplicate-paw")]
        [TestCase("paw-bounds")]
        [TestCase("initial-match")]
        [TestCase("deadlock")]
        public void InvalidAuthoredDataIsRejectedExplicitly(string fault)
        {
            var cells = AuthoredLevels.Create(1).CopyCells();
            var targets = new Dictionary<CatType, int> { { CatType.Orange, 3 } };
            var paws = new[] { P(0, 0) };
            int width = 7, moves = 4; string id = "test";
            switch (fault)
            {
                case "dimensions": width = 6; break;
                case "cells": cells = new Tile[48]; break;
                case "moves": moves = 0; break;
                case "identity": id = " "; break;
                case "objectives": targets.Clear(); paws = Array.Empty<BoardPosition>(); break;
                case "target-type": targets.Clear(); targets.Add((CatType)9, 3); break;
                case "target-count": targets[CatType.Orange] = 0; break;
                case "duplicate-paw": paws = new[] { P(0, 0), P(0, 0) }; break;
                case "paw-bounds": paws = new[] { P(7, 0) }; break;
                case "initial-match": cells[0] = cells[1] = cells[2] = new Tile(CatType.Orange); break;
                case "deadlock": cells = Flatten(PrototypeLayout.CreateDeadlockedBoard()); break;
            }
            var error = Assert.Throws<ArgumentException>(() => new LevelConfiguration(id, "Invalid", width, 7, cells, moves, targets, paws));
            Assert.That(error.Message, Is.Not.Empty);
        }
    }
}
