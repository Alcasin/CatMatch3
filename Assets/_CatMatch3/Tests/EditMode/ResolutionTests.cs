using System;
using System.Collections.Generic;
using System.Linq;
using CatMatch3.Core;
using NUnit.Framework;

namespace CatMatch3.Tests.EditMode
{
    public sealed class ResolutionTests
    {
        private static BoardPosition P(int x, int y) { return new BoardPosition(x, y); }

        private sealed class ScriptedRandom : IRandomSource
        {
            private readonly Queue<int> values;
            public ScriptedRandom(params int[] values) { this.values = new Queue<int>(values); }
            public int NextInt(int bound)
            {
                Assert.That(values.Count, Is.GreaterThan(0), "Unexpected extra random draw / resolution wave.");
                int value = values.Dequeue();
                Assert.That(value, Is.InRange(0, bound - 1));
                return value;
            }
            public void AssertConsumed() { Assert.That(values, Is.Empty); }
        }

        private sealed class ConstantRandom : IRandomSource
        {
            private readonly int value;
            public ConstantRandom(int value) { this.value = value; }
            public int NextInt(int bound) { return value; }
        }

        private sealed class IdentityShuffleRandom : IRandomSource
        {
            public int NextInt(int bound) { return bound - 1; }
        }

        private static Board TopTriple()
        {
            var layout = PrototypeLayout.CreateBoard().CopyCells();
            layout[0, 6] = layout[1, 6] = layout[2, 6] = CatType.Black;
            return new Board(layout);
        }

        private static void AssertPlayable(Board board)
        {
            Assert.That(board.IsFull, Is.True);
            Assert.That(MatchFinder.Find(board).HasMatches, Is.False);
            Assert.That(board.HasLegalMove(), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClearRemovesAnEntireHorizontalOrVerticalRun(bool vertical)
        {
            var layout = PrototypeLayout.CreateDeadlockedBoard().CopyCells();
            for (int i = 0; i < 3; i++) layout[vertical ? 6 : i, vertical ? i : 6] = CatType.Gray;
            var board = new Board(layout);
            var before = board.Capture();
            var matches = MatchFinder.Find(board);
            Assert.That(matches.Groups.Count, Is.EqualTo(1));
            var cleared = board.ClearCells(matches.Cells);
            Assert.That(cleared.Count, Is.EqualTo(3));
            foreach (var p in cleared)
            {
                Assert.That(board.TryGetCell(p, out _), Is.False);
                Assert.That(board.Capture().GetCell(p), Is.Null);
                Assert.That(before.GetCell(p), Is.EqualTo(CatType.Gray));
            }
            Assert.That(MatchFinder.Find(board).HasMatches, Is.False, "Empty cells must break runs.");
            Assert.That(board.ClearCells(matches.Cells), Is.Empty, "Already empty cells are not cleared twice.");
        }

        [Test]
        public void ClearDeduplicatesCrossAndClearsIndependentGroupInTheSameWave()
        {
            var layout = PrototypeLayout.CreateDeadlockedBoard().CopyCells();
            for (int i = 0; i < 7; i++) layout[i, 3] = layout[3, i] = CatType.Black;
            for (int i = 1; i <= 5; i++) layout[i, 3] = layout[3, i] = CatType.Orange;
            layout[0, 6] = layout[1, 6] = layout[2, 6] = CatType.Siamese;
            var board = new Board(layout);
            var matches = MatchFinder.Find(board);
            var original = board.Capture();
            var cleared = board.ClearCells(matches.Cells.Concat(matches.Cells).ToArray());
            Assert.That(matches.Groups.Count, Is.EqualTo(3));
            Assert.That(cleared.Count, Is.EqualTo(12));
            Assert.That(cleared.Count(p => p == P(3, 3)), Is.EqualTo(1));
            for (int x = 0; x < Board.Width; x++)
                for (int y = 0; y < Board.Height; y++)
                    Assert.That(board.Capture().GetCell(P(x, y)),
                        Is.EqualTo(matches.Contains(P(x, y)) ? null : original.GetCell(P(x, y))));
            Assert.That(matches.Cells.Count, Is.EqualTo(12), "Clearing cannot mutate the detected wave.");
        }

        [Test]
        public void GravityCompactsMultipleGapsAndPreservesSurvivorOrderPerColumn()
        {
            var board = PrototypeLayout.CreateBoard();
            var original = board.CopyCells();
            var gaps = new[] { P(2, 0), P(2, 2), P(2, 5), P(4, 1), P(4, 6) };
            board.ClearCells(gaps);
            var movements = board.ApplyGravity();
            for (int x = 0; x < Board.Width; x++)
            {
                var survivors = Enumerable.Range(0, Board.Height).Where(y => !gaps.Contains(P(x, y))).ToArray();
                for (int y = 0; y < Board.Height; y++)
                    if (y < survivors.Length) Assert.That(board.GetCell(P(x, y)), Is.EqualTo(original[x, survivors[y]]));
                    else Assert.That(board.TryGetCell(P(x, y), out _), Is.False);
            }
            Assert.That(movements.All(m => m.From.X == m.To.X && m.To.Y < m.From.Y), Is.True);
            foreach (var move in movements) Assert.That(move.Type, Is.EqualTo(original[move.From.X, move.From.Y]));
            Assert.That(board.ApplyGravity(), Is.Empty, "Compaction is idempotent.");
        }

        [Test]
        public void RefillIsFullValidSeededAndEntersAtColumnTop()
        {
            var a = PrototypeLayout.CreateBoard();
            var b = PrototypeLayout.CreateBoard();
            var gaps = new[] { P(0, 0), P(0, 2), P(3, 4), P(6, 6) };
            a.ClearCells(gaps); b.ClearCells(gaps);
            a.ApplyGravity(); b.ApplyGravity();
            var before = a.Capture();
            var spawns = a.Refill(new SeededRandom(91));
            b.Refill(new SeededRandom(91));
            Assert.That(a.CopyCells(), Is.EqualTo(b.CopyCells()));
            Assert.That(a.IsFull, Is.True);
            Assert.That(a.CopyCells().Cast<CatType>().All(c => Enum.IsDefined(typeof(CatType), c)), Is.True);
            Assert.That(spawns.Count, Is.EqualTo(gaps.Length));
            Assert.That(spawns.Select(s => s.Position), Is.EqualTo(new[] { P(0, 5), P(0, 6), P(3, 6), P(6, 6) }));
            Assert.That(spawns.Select(s => s.EntryPosition), Is.EqualTo(new[] { P(0, 7), P(0, 8), P(3, 7), P(6, 7) }));
            foreach (var spawn in spawns)
            {
                Assert.That(before.GetCell(spawn.Position), Is.Null);
                Assert.That(a.GetCell(spawn.Position), Is.EqualTo(spawn.Type));
            }
            var copy = before.CopyCells(); copy[1, 0] = null;
            Assert.That(before.GetCell(P(1, 0)), Is.Not.Null, "Snapshots must own their arrays.");
        }

        [Test]
        public void RefillRejectsGapsOrFaultyRandomWithoutPartiallyWritingCells()
        {
            var board = PrototypeLayout.CreateBoard();
            board.ClearCells(new[] { P(0, 1), P(1, 2) });
            var before = board.Capture().CopyCells();
            Assert.Throws<InvalidOperationException>(() => board.Refill(new SeededRandom(1)));
            Assert.That(board.Capture().CopyCells(), Is.EqualTo(before));
            board.ApplyGravity();
            before = board.Capture().CopyCells();
            Assert.Throws<InvalidOperationException>(() => board.Refill(new ConstantRandom(5)));
            Assert.That(board.Capture().CopyCells(), Is.EqualTo(before));
            Assert.That(board.TrySwap(P(0, 0), P(1, 0)).IsValid, Is.False, "Partial boards cannot accept moves.");
        }

        [Test]
        public void OneWaveResolvesWithoutCascadeAndRetainsIntermediateSnapshots()
        {
            var board = TopTriple();
            var random = new ScriptedRandom(2, 3, 4); // Restore W/G/S at the original top-left.
            var result = BoardResolver.Resolve(board, random);
            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.WaveCount, Is.EqualTo(1));
            Assert.That(result.CascadeCount, Is.Zero);
            var wave = result.Waves.Single();
            Assert.That(wave.Matches.Cells.Count, Is.EqualTo(3));
            Assert.That(wave.Movements, Is.Empty, "Only the top row was removed.");
            Assert.That(wave.Before.GetCell(P(0, 6)), Is.EqualTo(CatType.Black));
            Assert.That(wave.AfterClear.GetCell(P(0, 6)), Is.Null);
            Assert.That(wave.AfterGravity.GetCell(P(0, 6)), Is.Null);
            Assert.That(wave.AfterRefill.GetCell(P(0, 6)), Is.EqualTo(CatType.White));
            Assert.That(result.Final.CopyCells(), Is.EqualTo(board.Capture().CopyCells()));
            Assert.That(board.CopyCells(), Is.EqualTo(PrototypeLayout.CreateBoard().CopyCells()));
            random.AssertConsumed();
            AssertPlayable(board);
        }

        [Test]
        public void ScriptedRefillConstructsExactlyTwoWavesThenStops()
        {
            var board = TopTriple();
            var random = new ScriptedRandom(3, 3, 3, 2, 3, 4); // BBB -> GGG -> original WGS.
            var result = BoardResolver.Resolve(board, random);
            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.WaveCount, Is.EqualTo(2));
            Assert.That(result.CascadeCount, Is.EqualTo(1));
            Assert.That(result.Waves[0].Matches.Groups.Single().CatType, Is.EqualTo(CatType.Black));
            Assert.That(result.Waves[1].Matches.Groups.Single().CatType, Is.EqualTo(CatType.Gray));
            Assert.That(result.Waves[1].Before.CopyCells(), Is.EqualTo(result.Waves[0].AfterRefill.CopyCells()));
            Assert.That(result.Waves.All(w => w.Spawns.Count == 3), Is.True);
            Assert.That(result.Reshuffle, Is.Null);
            random.AssertConsumed();
            AssertPlayable(board);
        }

        [Test]
        public void IndependentMatchesResolveTogetherBeforeAnyRefill()
        {
            var layout = TopTriple().CopyCells();
            layout[4, 6] = layout[5, 6] = layout[6, 6] = CatType.White;
            var board = new Board(layout);
            var random = new ScriptedRandom(2, 3, 4, 1, 3, 3);
            var result = BoardResolver.Resolve(board, random);
            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.WaveCount, Is.EqualTo(1));
            Assert.That(result.Waves[0].Matches.Groups.Count, Is.EqualTo(2));
            Assert.That(result.Waves[0].Matches.Cells.Count, Is.EqualTo(6));
            Assert.That(result.Waves[0].AfterClear.CopyCells().Cast<CatType?>().Count(c => !c.HasValue), Is.EqualTo(6));
            random.AssertConsumed();
            AssertPlayable(board);
        }

        [Test]
        public void DiagnosticCascadeSwapProducesThreeWavesWithDeterministicFallAndRefill()
        {
            var a = PrototypeLayout.CreateBoard();
            var b = PrototypeLayout.CreateBoard();
            var swapA = a.TrySwap(P(1, 0), P(1, 1));
            Assert.That(swapA.IsValid, Is.True);
            Assert.That(b.TrySwap(P(1, 0), P(1, 1)).IsValid, Is.True);
            var first = BoardResolver.Resolve(a, new SeededRandom(141));
            var second = BoardResolver.Resolve(b, new SeededRandom(141));
            Assert.That(first.Success, Is.True, first.Error);
            Assert.That(first.WaveCount, Is.EqualTo(3));
            Assert.That(first.Waves[0].Matches.Cells, Is.EqualTo(swapA.Matches.Cells));
            Assert.That(first.Waves[0].Movements.Count, Is.GreaterThan(0));
            Assert.That(a.CopyCells(), Is.EqualTo(b.CopyCells()));
            for (int i = 0; i < first.WaveCount; i++)
            {
                Assert.That(first.Waves[i].AfterClear.CopyCells(), Is.EqualTo(second.Waves[i].AfterClear.CopyCells()));
                Assert.That(first.Waves[i].AfterGravity.CopyCells(), Is.EqualTo(second.Waves[i].AfterGravity.CopyCells()));
                Assert.That(first.Waves[i].AfterRefill.CopyCells(), Is.EqualTo(second.Waves[i].AfterRefill.CopyCells()));
            }
            AssertPlayable(a);
        }

        [Test]
        public void ConsecutiveValidSwapsResolveAndCanBeReplayedFromResetSeed()
        {
            var a = PrototypeLayout.CreateBoard();
            var b = PrototypeLayout.CreateBoard();
            var randomA = new SeededRandom(1337);
            var randomB = new SeededRandom(1337);
            for (int turn = 0; turn < 8; turn++)
            {
                AssertPlayable(a);
                BoardPosition first = default, second = default;
                bool found = false;
                for (int x = 0; x < Board.Width && !found; x++)
                    for (int y = 0; y < Board.Height && !found; y++)
                        foreach (var neighbor in new[] { P(x + 1, y), P(x, y + 1) })
                            if (Board.IsValidPosition(neighbor) && new Board(a.CopyCells()).TrySwap(P(x, y), neighbor).IsValid)
                            { first = P(x, y); second = neighbor; found = true; break; }
                Assert.That(found, Is.True);
                Assert.That(a.TrySwap(first, second).IsValid, Is.True);
                Assert.That(b.TrySwap(first, second).IsValid, Is.True);
                var resultA = BoardResolver.Resolve(a, randomA);
                var resultB = BoardResolver.Resolve(b, randomB);
                Assert.That(resultA.Success && resultB.Success, Is.True, resultA.Error);
                Assert.That(resultA.WaveCount, Is.EqualTo(resultB.WaveCount));
                Assert.That(a.CopyCells(), Is.EqualTo(b.CopyCells()));
                AssertPlayable(a);
            }
        }

        [Test]
        public void InvalidSwapStillLeavesBoardUnchangedBeforeAValidResolution()
        {
            var board = PrototypeLayout.CreateBoard();
            var before = board.CopyCells();
            Assert.That(board.TrySwap(P(3, 0), P(4, 0)).IsValid, Is.False);
            Assert.That(board.CopyCells(), Is.EqualTo(before));
            Assert.That(board.TrySwap(P(1, 0), P(1, 1)).IsValid, Is.True);
            Assert.That(BoardResolver.Resolve(board, new SeededRandom(1337)).Success, Is.True);
            AssertPlayable(board);
        }

        [Test]
        public void WaveGuardExplicitlyFailsWithUnresolvedSnapshot()
        {
            var board = new Board(new CatType[Board.Width, Board.Height]);
            var result = BoardResolver.Resolve(board, new ConstantRandom(0), maxWaves: 3);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("3 waves"));
            Assert.That(result.WaveCount, Is.EqualTo(3));
            Assert.That(board.IsFull, Is.True);
            Assert.That(MatchFinder.Find(board).HasMatches, Is.True);
            Assert.That(result.Final.CopyCells(), Is.EqualTo(board.Capture().CopyCells()));
            Assert.That(result.Reshuffle, Is.Null, "Never check or shuffle an unresolved board.");
        }

        [Test]
        public void LegalMoveScanIsNonmutatingForPlayableAndDeadlockedBoards()
        {
            var playable = PrototypeLayout.CreateBoard();
            var deadlocked = PrototypeLayout.CreateDeadlockedBoard();
            var playableBefore = playable.CopyCells();
            var deadBefore = deadlocked.CopyCells();
            Assert.That(playable.HasLegalMove(), Is.True);
            Assert.That(playable.CopyCells(), Is.EqualTo(playableBefore));
            Assert.That(MatchFinder.Find(deadlocked).HasMatches, Is.False);
            Assert.That(deadlocked.HasLegalMove(), Is.False);
            Assert.That(deadlocked.CopyCells(), Is.EqualTo(deadBefore));
        }

        [Test]
        public void StableDeadlockAutomaticallyShufflesIntoReproduciblePlayablePermutation()
        {
            var a = PrototypeLayout.CreateDeadlockedBoard();
            var b = PrototypeLayout.CreateDeadlockedBoard();
            var before = a.CopyCells();
            var first = BoardResolver.Resolve(a, new SeededRandom(1337), reshufflePolicy: ReshufflePolicy.Automatic);
            var second = BoardResolver.Resolve(b, new SeededRandom(1337), reshufflePolicy: ReshufflePolicy.Automatic);
            Assert.That(first.Success, Is.True, first.Error);
            Assert.That(second.Success, Is.True, second.Error);
            Assert.That(first.Waves, Is.Empty);
            Assert.That(first.Reshuffle, Is.Not.Null);
            Assert.That(first.Reshuffle.Success, Is.True);
            Assert.That(first.Reshuffle.Attempts, Is.InRange(1, BoardShuffler.DefaultMaxAttempts));
            Assert.That(first.Reshuffle.Attempts, Is.EqualTo(second.Reshuffle.Attempts));
            Assert.That(a.CopyCells(), Is.EqualTo(b.CopyCells()));
            Assert.That(a.CopyCells().Cast<CatType>(), Is.EquivalentTo(before.Cast<CatType>()));
            Assert.That(first.Reshuffle.Before.CopyCells(), Is.EqualTo(first.Initial.CopyCells()));
            Assert.That(first.Reshuffle.After.CopyCells(), Is.EqualTo(first.Final.CopyCells()));
            AssertPlayable(a);
        }

        [Test]
        public void ImpossibleCountsAndExhaustedShuffleFailWithoutChangingTheBoard()
        {
            var impossible = new Board(new CatType[Board.Width, Board.Height]);
            var before = impossible.CopyCells();
            var result = BoardShuffler.TryReshuffle(impossible, new SeededRandom(1));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Attempts, Is.Zero);
            Assert.That(result.Error, Does.Contain("Tile counts"));
            Assert.That(impossible.CopyCells(), Is.EqualTo(before));

            var dead = PrototypeLayout.CreateDeadlockedBoard();
            before = dead.CopyCells();
            var failed = BoardResolver.Resolve(dead, new IdentityShuffleRandom(), maxShuffleAttempts: 4,
                reshufflePolicy: ReshufflePolicy.Automatic);
            Assert.That(failed.Success, Is.False);
            Assert.That(failed.Reshuffle.Attempts, Is.EqualTo(4));
            Assert.That(failed.Error, Does.Contain("4 attempts"));
            Assert.That(dead.CopyCells(), Is.EqualTo(before));
            Assert.That(dead.HasLegalMove(), Is.False);
            Assert.Throws<InvalidOperationException>(() => BoardShuffler.TryReshuffle(dead, new ConstantRandom(-1)));
            Assert.That(dead.CopyCells(), Is.EqualTo(before));
        }
    }
}
