using System;
using System.Collections.Generic;
using System.Linq;
using CatMatch3.Core;
using NUnit.Framework;

namespace CatMatch3.Tests.EditMode
{
    public sealed class SpecialObjectiveTests
    {
        private static BoardPosition P(int x, int y) { return new BoardPosition(x, y); }
        private static void AssertPlayable(Board board)
        {
            Assert.That(board.IsFull, Is.True);
            Assert.That(MatchFinder.Find(board).HasMatches, Is.False);
            Assert.That(board.HasLegalMove(), Is.True);
        }

        private sealed class ScriptedRandom : IRandomSource
        {
            private readonly Queue<int> values;
            public ScriptedRandom(params int[] values) { this.values = new Queue<int>(values); }
            public int NextInt(int bound)
            {
                Assert.That(values.Count, Is.GreaterThan(0), "Unexpected extra refill or shuffle.");
                int value = values.Dequeue();
                Assert.That(value, Is.InRange(0, bound - 1));
                return value;
            }
            public void AssertConsumed() { Assert.That(values, Is.Empty); }
        }

        private static Board TopRun(int length, bool vertical = false)
        {
            var layout = PrototypeLayout.CreateBoard().CopyTiles();
            for (int x = 0; x < Board.Width; x++)
                layout[x, 6] = new Tile(x < length ? CatType.Orange : (CatType)(x - length + 1));
            if (!vertical) return new Board(layout);
            var transpose = new Tile[Board.Width, Board.Height];
            for (int x = 0; x < Board.Width; x++)
                for (int y = 0; y < Board.Height; y++) transpose[y, x] = layout[x, y];
            return new Board(transpose);
        }

        [TestCase(M3QaScenario.HorizontalCreation, StripeOrientation.Horizontal, 1, 0)]
        [TestCase(M3QaScenario.VerticalCreation, StripeOrientation.Vertical, 0, 1)]
        public void GuaranteedFourSwapCreatesOneSurvivingStripeWithoutSelfActivation(
            M3QaScenario scenario, StripeOrientation stripe, int anchorX, int anchorY)
        {
            var board = M3PrototypeLayout.CreateBoard(scenario);
            AssertPlayable(board);
            var destination = P(anchorX, anchorY);
            Assert.That(board.TrySwap(P(1, 1), destination).IsValid, Is.True);
            var result = BoardResolver.Resolve(board, new SeededRandom(1337), swapDestination: destination);
            Assert.That(result.Success, Is.True, result.Error);
            var wave = result.Waves.Single();
            Assert.That(wave.Matches.Groups.Single().Length, Is.EqualTo(4));
            Assert.That(wave.Creations.Count, Is.EqualTo(1));
            Assert.That(wave.Creations[0].Position, Is.EqualTo(destination));
            Assert.That(wave.Creations[0].Tile, Is.EqualTo(new Tile(CatType.Orange, stripe)));
            Assert.That(wave.Activations, Is.Empty);
            Assert.That(wave.Removals.Count, Is.EqualTo(3));
            Assert.That(wave.Removals.Any(r => r.Position == destination), Is.False);
            Assert.That(wave.AfterClear.GetTile(destination), Is.EqualTo(wave.Creations[0].Tile));
            Assert.That(board.CopyTiles().Cast<Tile>().Count(t => t.IsSpecial), Is.EqualTo(1));
            var fallenAnchor = stripe == StripeOrientation.Vertical ? P(0, 0) : destination;
            Assert.That(board.GetTile(fallenAnchor), Is.EqualTo(new Tile(CatType.Orange, stripe)));
            AssertPlayable(board);
        }

        [TestCase(5, false)]
        [TestCase(7, true)]
        public void LongRunCreatesOnlyOneStripeAtDeterministicFallback(int length, bool vertical)
        {
            var board = TopRun(length, vertical);
            var result = BoardResolver.Resolve(board, new SeededRandom(17));
            Assert.That(result.Success, Is.True, result.Error);
            var wave = result.Waves[0];
            Assert.That(wave.Matches.Groups.Single().Length, Is.EqualTo(length));
            var creation = wave.Creations.Single();
            Assert.That(creation.Position, Is.EqualTo(vertical ? P(6, 0) : P(0, 6)));
            Assert.That(creation.Tile.Type, Is.EqualTo(CatType.Orange));
            Assert.That(creation.Tile.Stripe, Is.EqualTo(vertical ? StripeOrientation.Vertical : StripeOrientation.Horizontal));
            Assert.That(wave.Removals.Count, Is.EqualTo(length - 1));
            Assert.That(wave.Activations, Is.Empty);
            Assert.That(wave.AfterClear.GetTile(creation.Position), Is.EqualTo(creation.Tile));
            AssertPlayable(board);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OverlappingQualifyingRunsHaveDeterministicAnchorPriority(bool useSharedDestination)
        {
            var layout = TopRun(4).CopyTiles();
            for (int y = 3; y < 7; y++) layout[0, y] = new Tile(CatType.Orange);
            var board = new Board(layout);
            Assert.That(MatchFinder.Find(board).Groups.Count, Is.EqualTo(2));
            var result = BoardResolver.Resolve(board, new SeededRandom(17), maxWaves: 1,
                swapDestination: useSharedDestination ? P(0, 6) : (BoardPosition?)null);
            var wave = result.Waves.Single();
            Assert.That(wave.Creations.Count, Is.EqualTo(useSharedDestination ? 1 : 2));
            Assert.That(wave.Creations[0].Position, Is.EqualTo(P(0, 6)));
            Assert.That(wave.Creations[0].Tile.Stripe, Is.EqualTo(StripeOrientation.Horizontal));
            if (!useSharedDestination)
            {
                Assert.That(wave.Creations[1].Position, Is.EqualTo(P(0, 3)));
                Assert.That(wave.Creations[1].Tile.Stripe, Is.EqualTo(StripeOrientation.Vertical));
            }
            Assert.That(wave.Removals.Count, Is.EqualTo(7 - wave.Creations.Count));
            Assert.That(wave.Removals.Select(r => r.Position).Distinct().Count(), Is.EqualTo(wave.Removals.Count));
        }

        [Test]
        public void IndependentFourRunsCreateTheirOwnSpecialsInOneWave()
        {
            var layout = TopRun(4).CopyTiles();
            for (int y = 0; y < 4; y++) layout[6, y] = new Tile(CatType.Orange);
            var board = new Board(layout);
            var result = BoardResolver.Resolve(board, new SeededRandom(17), maxWaves: 1, swapDestination: P(2, 6));
            var wave = result.Waves.Single();
            Assert.That(wave.Matches.Groups.Count, Is.EqualTo(2));
            Assert.That(wave.Creations.Select(c => c.Position), Is.EqualTo(new[] { P(2, 6), P(6, 0) }));
            Assert.That(wave.Removals.Count, Is.EqualTo(6));
            Assert.That(wave.Activations, Is.Empty);
        }

        [TestCase(M3QaScenario.HorizontalActivation, StripeOrientation.Horizontal)]
        [TestCase(M3QaScenario.VerticalActivation, StripeOrientation.Vertical)]
        public void ExistingStripeRequiresNormalMatchAndClearsItsCompleteLine(
            M3QaScenario scenario, StripeOrientation stripe)
        {
            var board = M3PrototypeLayout.CreateBoard(scenario);
            var destination = M3PrototypeLayout.SwapDestination(scenario);
            Assert.That(board.TrySwap(P(1, 1), destination).IsValid, Is.True);
            var result = BoardResolver.Resolve(board, new SeededRandom(1337), swapDestination: destination);
            Assert.That(result.Success, Is.True, result.Error);
            var wave = result.Waves.Single();
            var activation = wave.Activations.Single();
            Assert.That(activation.Tile.Stripe, Is.EqualTo(stripe));
            Assert.That(activation.Position, Is.EqualTo(P(0, 0)));
            Assert.That(activation.IsChained, Is.False);
            var expected = Enumerable.Range(0, 7).Select(i => stripe == StripeOrientation.Horizontal ? P(i, 0) : P(0, i));
            Assert.That(activation.Cells, Is.EqualTo(expected));
            Assert.That(wave.Removals.Select(r => r.Position), Is.EquivalentTo(expected));
            Assert.That(wave.Removals.All(r => r.HitBySpecial), Is.True);
            Assert.That(wave.Creations, Is.Empty);
            Assert.That(board.CopyTiles().Cast<Tile>().Any(t => t.IsSpecial), Is.False);
            AssertPlayable(board);
        }

        [Test]
        public void ExistingStripeInFourRunActivatesInsteadOfCreatingReplacement()
        {
            var layout = TopRun(4).CopyTiles();
            layout[3, 6] = new Tile(CatType.Orange, StripeOrientation.Horizontal);
            var result = BoardResolver.Resolve(new Board(layout), new SeededRandom(17), maxWaves: 1);
            var wave = result.Waves.Single();
            Assert.That(wave.Matches.Groups.Single().Length, Is.EqualTo(4));
            Assert.That(wave.Creations, Is.Empty);
            Assert.That(wave.Activations.Count, Is.EqualTo(1));
            Assert.That(wave.Removals.Count, Is.EqualTo(7));
        }

        [Test]
        public void CyclicCrossingChainsActivateEachExistingSpecialOnceAndCountUniqueNormals()
        {
            var layout = PrototypeLayout.CreateBoard().CopyTiles();
            layout[0, 6] = new Tile(CatType.Black, StripeOrientation.Horizontal);
            layout[1, 6] = layout[2, 6] = new Tile(CatType.Black);
            layout[3, 6] = new Tile(layout[3, 6].Type, StripeOrientation.Vertical);
            layout[3, 3] = new Tile(layout[3, 3].Type, StripeOrientation.Horizontal);
            layout[0, 3] = new Tile(layout[0, 3].Type, StripeOrientation.Vertical);
            var board = new Board(layout);
            var objectives = new ObjectiveTracker(Enum.GetValues(typeof(CatType)).Cast<CatType>().ToDictionary(t => t, t => 99),
                new[] { P(0, 6), P(3, 3), P(6, 6), P(6, 0) });
            var result = BoardResolver.Resolve(board, new SeededRandom(17), maxWaves: 1, objectives: objectives);
            var wave = result.Waves.Single();
            Assert.That(wave.Activations.Select(a => a.Position), Is.EqualTo(new[] { P(0, 6), P(3, 6), P(3, 3), P(0, 3) }));
            Assert.That(wave.Activations.Select(a => a.IsChained), Is.EqualTo(new[] { false, true, true, true }));
            Assert.That(wave.Removals.Count, Is.EqualTo(24));
            Assert.That(wave.Removals.Select(r => r.Position).Distinct().Count(), Is.EqualTo(24));
            Assert.That(wave.Removals.Count(r => r.Tile.IsSpecial), Is.EqualTo(4));
            Assert.That(wave.ObjectivesAfter.Collections.Sum(c => c.Collected), Is.EqualTo(20));
            Assert.That(wave.ObjectivesAfter.ClearedPaws, Is.EqualTo(3));
            Assert.That(wave.ObjectivesAfter.RemainingMarks, Is.EqualTo(new[] { P(6, 0) }));
        }

        [Test]
        public void ExplosionDefeatsConflictingCreationInAnotherMatchingRun()
        {
            var layout = PrototypeLayout.CreateBoard().CopyTiles();
            for (int x = 0; x < 7; x++) layout[x, 6] = new Tile(x < 3 ? CatType.Black : CatType.Orange);
            layout[0, 6] = new Tile(CatType.Black, StripeOrientation.Horizontal);
            var result = BoardResolver.Resolve(new Board(layout), new SeededRandom(17), maxWaves: 1, swapDestination: P(3, 6));
            var wave = result.Waves.Single();
            Assert.That(wave.Matches.Groups.Select(g => g.Length), Is.EqualTo(new[] { 3, 4 }));
            Assert.That(wave.Activations.Count, Is.EqualTo(1));
            Assert.That(wave.Creations, Is.Empty);
            Assert.That(wave.Removals.Count, Is.EqualTo(7));
            Assert.That(wave.AfterClear.GetTile(P(3, 6)), Is.Null);
        }

        [Test]
        public void CreationSurvivorGivesNoCollectionOrPawCredit()
        {
            var board = M3PrototypeLayout.CreateBoard(M3QaScenario.HorizontalCreation);
            var objectives = M3PrototypeLayout.CreateObjectives();
            board.TrySwap(P(1, 1), P(1, 0));
            var result = BoardResolver.Resolve(board, new SeededRandom(1337), objectives: objectives, swapDestination: P(1, 0));
            var progress = result.FinalObjectives;
            Assert.That(progress.Collections.Single(c => c.Type == CatType.Orange).Collected, Is.EqualTo(3));
            Assert.That(progress.Collections.Single(c => c.Type == CatType.Gray).Collected, Is.Zero);
            Assert.That(progress.ClearedPaws, Is.EqualTo(3));
            Assert.That(progress.RemainingMarks, Does.Contain(P(1, 0)));
            Assert.That(progress.IsComplete, Is.False);
        }

        [Test]
        public void CascadeAndSubsequentSwapAccumulateTargetsWithoutRecountingPaws()
        {
            var layout = PrototypeLayout.CreateBoard().CopyTiles();
            for (int x = 0; x < 3; x++) layout[x, 6] = new Tile(CatType.Black);
            var board = new Board(layout);
            var objectives = new ObjectiveTracker(new Dictionary<CatType, int>
                { { CatType.Black, 2 }, { CatType.Gray, 3 }, { CatType.Orange, 2 } },
                new[] { P(0, 6), P(1, 6), P(2, 6) });
            var random = new ScriptedRandom(3, 3, 3, 2, 3, 4); // BBB -> GGG -> original WGS.
            var first = BoardResolver.Resolve(board, random, objectives: objectives);
            random.AssertConsumed();
            Assert.That(first.Success, Is.True, first.Error);
            Assert.That(first.WaveCount, Is.EqualTo(2));
            Assert.That(first.FinalObjectives.Collections.Single(c => c.Type == CatType.Black).Collected, Is.EqualTo(3));
            Assert.That(first.FinalObjectives.Collections.Single(c => c.Type == CatType.Gray).Collected, Is.EqualTo(3));
            Assert.That(first.FinalObjectives.ClearedPaws, Is.EqualTo(3));
            Assert.That(first.FinalObjectives.IsComplete, Is.False);
            Assert.That(board.TrySwap(P(1, 1), P(1, 0)).IsValid, Is.True);
            var second = BoardResolver.Resolve(board, new SeededRandom(1337), objectives: objectives, swapDestination: P(1, 0));
            Assert.That(second.Success, Is.True, second.Error);
            Assert.That(second.FinalObjectives.Collections.Single(c => c.Type == CatType.Black).Collected, Is.EqualTo(3));
            Assert.That(second.FinalObjectives.Collections.Single(c => c.Type == CatType.Gray).Collected, Is.EqualTo(3));
            Assert.That(second.FinalObjectives.Collections.Single(c => c.Type == CatType.Orange).Collected, Is.EqualTo(3));
            Assert.That(second.FinalObjectives.ClearedPaws, Is.EqualTo(3));
            Assert.That(second.FinalObjectives.IsComplete, Is.True);
            Assert.That(first.FinalObjectives.IsComplete, Is.False, "Earlier progress snapshots cannot change.");
            AssertPlayable(board);
        }

        [Test]
        public void MarksStayAtCellsDuringSwapGravityAndRefillAndOnlyRemovedPositionsCount()
        {
            var board = M3PrototypeLayout.CreateBoard(M3QaScenario.HorizontalActivation);
            var objectives = new ObjectiveTracker(new Dictionary<CatType, int> { { CatType.Orange, 3 }, { CatType.Gray, 1 } },
                new[] { P(0, 3), P(1, 0) });
            Assert.That(board.TrySwap(P(1, 1), P(1, 0)).IsValid, Is.True);
            Assert.That(objectives.Capture().RemainingMarks, Is.EqualTo(new[] { P(0, 3), P(1, 0) }));
            var result = BoardResolver.Resolve(board, new SeededRandom(1337), objectives: objectives, swapDestination: P(1, 0));
            var wave = result.Waves.Single();
            Assert.That(wave.Movements.Any(m => m.To == P(0, 3)), Is.True);
            Assert.That(wave.Removals.Any(r => r.Position == P(0, 3)), Is.False);
            Assert.That(result.FinalObjectives.RemainingMarks, Is.EqualTo(new[] { P(0, 3) }));
            Assert.That(result.FinalObjectives.ClearedPaws, Is.EqualTo(1));
            Assert.That(result.FinalObjectives.Collections.Select(c => c.Collected), Is.EqualTo(new[] { 3, 1 }));
        }

        [Test]
        public void CascadeCreatesAtFallbackAndALaterPlayerMatchActivatesThatSameStripe()
        {
            var layout = PrototypeLayout.CreateBoard().CopyTiles();
            for (int x = 0; x < 3; x++) layout[x, 6] = new Tile(CatType.Black);
            var board = new Board(layout);
            // BBB -> OOOO -> O O B O B G G. A later B/O swap matches the created stripe.
            var random = new ScriptedRandom(0, 0, 0, 0, 1, 0, 2, 3, 4, 0, 1, 3, 3);
            var first = BoardResolver.Resolve(board, random, swapDestination: P(2, 6));
            Assert.That(first.Success, Is.True, first.Error);
            Assert.That(first.WaveCount, Is.EqualTo(2));
            Assert.That(first.Waves[0].Creations, Is.Empty);
            Assert.That(first.Waves[1].Creations.Single().Position, Is.EqualTo(P(0, 6)),
                "A cascade must ignore the earlier swap destination and use its fixed fallback.");
            Assert.That(first.Waves[1].Activations, Is.Empty);
            Assert.That(board.GetTile(P(0, 6)), Is.EqualTo(new Tile(CatType.Orange, StripeOrientation.Horizontal)));
            AssertPlayable(board);
            Assert.That(board.TrySwap(P(3, 6), P(2, 6)).IsValid, Is.True);
            var second = BoardResolver.Resolve(board, random, swapDestination: P(2, 6));
            Assert.That(second.Success, Is.True, second.Error);
            Assert.That(second.Waves.Single().Activations.Single().Position, Is.EqualTo(P(0, 6)));
            Assert.That(second.Waves[0].Removals.Count, Is.EqualTo(7));
            Assert.That(board.CopyTiles().Cast<Tile>().Any(t => t.IsSpecial), Is.False);
            Assert.That(board.CopyCells(), Is.EqualTo(PrototypeLayout.CreateBoard().CopyCells()));
            random.AssertConsumed();
            AssertPlayable(board);
        }

        [Test]
        public void ResetRestoresCopiedObjectiveDefinitionAndOldSnapshotsRemainImmutable()
        {
            var targets = new Dictionary<CatType, int> { { CatType.Black, 3 } };
            var marks = new List<BoardPosition> { P(0, 6), P(1, 6), P(2, 6) };
            var objectives = new ObjectiveTracker(targets, marks);
            targets[CatType.Black] = 99; marks.Clear();
            var layout = PrototypeLayout.CreateBoard().CopyTiles();
            for (int x = 0; x < 3; x++) layout[x, 6] = new Tile(CatType.Black);
            BoardResolver.Resolve(new Board(layout), new ScriptedRandom(2, 3, 4), objectives: objectives);
            var completed = objectives.Capture();
            Assert.That(completed.IsComplete, Is.True);
            objectives.Reset();
            var reset = objectives.Capture();
            Assert.That(reset.Collections.Single().Target, Is.EqualTo(3));
            Assert.That(reset.Collections.Single().Collected, Is.Zero);
            Assert.That(reset.RemainingMarks, Is.EqualTo(new[] { P(0, 6), P(1, 6), P(2, 6) }));
            Assert.That(reset.IsComplete, Is.False);
            Assert.That(completed.Collections.Single().Collected, Is.EqualTo(3));
            Assert.That(completed.RemainingMarks, Is.Empty);
            Assert.Throws<NotSupportedException>(() => ((IList<BoardPosition>)reset.RemainingMarks).Clear());
        }

        [Test]
        public void IdentitySnapshotsMovementAndSwapValidationPreserveStripeMetadata()
        {
            var layout = PrototypeLayout.CreateBoard().CopyTiles();
            layout[0, 5] = new Tile(layout[0, 5].Type, StripeOrientation.Vertical);
            var board = new Board(layout);
            var before = board.Capture();
            layout[0, 5] = new Tile(CatType.Black);
            board.ClearCells(new[] { P(0, 1), P(0, 4) });
            var moved = board.ApplyGravity().Single(m => m.From == P(0, 5));
            Assert.That(moved.To, Is.EqualTo(P(0, 3)));
            Assert.That(moved.Tile.Stripe, Is.EqualTo(StripeOrientation.Vertical));
            Assert.That(board.GetTile(moved.To), Is.EqualTo(moved.Tile));
            var snapshotCopy = before.CopyTiles(); snapshotCopy[0, 5] = null;
            Assert.That(before.GetTile(P(0, 5)).Value.Stripe, Is.EqualTo(StripeOrientation.Vertical));
            Assert.That(before.GetCell(P(0, 5)), Is.EqualTo(moved.Type));
            var spawns = board.Refill(new SeededRandom(9));
            Assert.That(spawns.All(s => !s.Tile.IsSpecial), Is.True);
            var copied = board.CopyTiles(); copied[moved.To.X, moved.To.Y] = default;
            Assert.That(board.GetTile(moved.To).IsSpecial, Is.True);

            var invalid = M3PrototypeLayout.CreateBoard(M3QaScenario.HorizontalActivation);
            var original = invalid.CopyTiles();
            Assert.That(invalid.TrySwap(P(0, 0), P(0, 1)).IsValid, Is.False);
            Assert.That(invalid.CopyTiles(), Is.EqualTo(original));
            original[1, 0] = new Tile(CatType.Orange, StripeOrientation.Vertical);
            var equalColors = new Board(original);
            Assert.That(equalColors.TrySwap(P(0, 0), P(1, 0)).IsValid, Is.False, "Special pairs confer no free activation.");
            Assert.That(equalColors.CopyTiles(), Is.EqualTo(original));
        }

        [Test]
        public void ReshufflePreservesAllTileIdentitiesAndExistingObjectiveProgress()
        {
            var board = M3PrototypeLayout.CreateBoard(M3QaScenario.HorizontalCreation);
            var objectives = M3PrototypeLayout.CreateObjectives();
            board.TrySwap(P(1, 1), P(1, 0));
            BoardResolver.Resolve(board, new SeededRandom(1337), objectives: objectives, swapDestination: P(1, 0));
            var tiles = board.CopyTiles();
            tiles[6, 6] = new Tile(tiles[6, 6].Type, StripeOrientation.Vertical);
            board = new Board(tiles);
            var clone = new Board(tiles);
            var progress = objectives.Capture();
            Assert.That(board.HasLegalMove(), Is.True);
            Assert.That(board.CopyTiles(), Is.EqualTo(tiles), "Move detection must restore complete identities.");
            var shuffled = BoardShuffler.TryReshuffle(board, new SeededRandom(91));
            var repeated = BoardShuffler.TryReshuffle(clone, new SeededRandom(91));
            Assert.That(shuffled.Success && repeated.Success, Is.True, shuffled.Error);
            Assert.That(board.CopyTiles().Cast<Tile>(), Is.EquivalentTo(tiles.Cast<Tile>()));
            Assert.That(board.CopyTiles(), Is.EqualTo(clone.CopyTiles()));
            Assert.That(objectives.Capture().RemainingMarks, Is.EqualTo(progress.RemainingMarks));
            Assert.That(objectives.Capture().Collections.Select(c => c.Collected), Is.EqualTo(progress.Collections.Select(c => c.Collected)));
            Assert.That(progress.ClearedPaws, Is.EqualTo(3));
            AssertPlayable(board);

            var deadTiles = PrototypeLayout.CreateDeadlockedBoard().CopyTiles();
            deadTiles[0, 0] = new Tile(deadTiles[0, 0].Type, StripeOrientation.Horizontal);
            deadTiles[6, 6] = new Tile(deadTiles[6, 6].Type, StripeOrientation.Vertical);
            var dead = new Board(deadTiles);
            Assert.That(dead.HasLegalMove(), Is.False);
            Assert.That(dead.CopyTiles(), Is.EqualTo(deadTiles));
            var automatic = BoardResolver.Resolve(dead, new SeededRandom(1337), objectives: objectives,
                reshufflePolicy: ReshufflePolicy.Automatic);
            Assert.That(automatic.Success, Is.True, automatic.Error);
            Assert.That(automatic.Reshuffle, Is.Not.Null);
            Assert.That(dead.CopyTiles().Cast<Tile>(), Is.EquivalentTo(deadTiles.Cast<Tile>()));
            Assert.That(automatic.FinalObjectives.RemainingMarks, Is.EqualTo(progress.RemainingMarks));
            Assert.That(automatic.FinalObjectives.Collections.Select(c => c.Collected), Is.EqualTo(progress.Collections.Select(c => c.Collected)));
            AssertPlayable(dead);
        }

        [Test]
        public void ChainQaFixtureContinuesThroughCascadesIntoAPlayableBoard()
        {
            var board = M3PrototypeLayout.CreateBoard(M3QaScenario.Chain);
            var objectives = M3PrototypeLayout.CreateObjectives();
            AssertPlayable(board);
            Assert.That(board.TrySwap(P(1, 1), P(1, 0)).IsValid, Is.True);
            var result = BoardResolver.Resolve(board, new SeededRandom(1337), objectives: objectives, swapDestination: P(1, 0));
            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.WaveCount, Is.EqualTo(4));
            Assert.That(result.Waves[0].Activations.Select(a => a.Position), Is.EqualTo(new[] { P(0, 0), P(4, 0), P(4, 4) }));
            Assert.That(result.Waves[0].Removals.Count, Is.EqualTo(19));
            foreach (var collection in result.FinalObjectives.Collections)
                Assert.That(collection.Collected, Is.EqualTo(result.Waves.Sum(w =>
                    w.Removals.Count(r => !r.Tile.IsSpecial && r.Tile.Type == collection.Type))));
            AssertPlayable(board);
        }
    }
}
