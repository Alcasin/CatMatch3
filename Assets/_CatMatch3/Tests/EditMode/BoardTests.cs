using System;
using System.Linq;
using CatMatch3.Core;
using NUnit.Framework;

namespace CatMatch3.Tests.EditMode
{
    public sealed class BoardTests
    {
        private static CatType[,] CleanCells()
        {
            var cells = new CatType[Board.Width, Board.Height];
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++)
                    cells[x, y] = (CatType)((x + 2 * y) % 5);
            return cells;
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(7)]
        public void HorizontalRunsAreMaximalAndIncludeBoardEdges(int length)
        {
            var cells = CleanCells();
            for (int x = 0; x < Board.Width; x++)
                cells[x, 2] = x < length ? CatType.Orange : CatType.Black;
            // Avoid introducing another run in the remainder of this controlled row.
            for (int x = length; x < Board.Width; x++)
                cells[x, 2] = x % 2 == 0 ? CatType.Black : CatType.White;
            var matches = MatchFinder.Find(new Board(cells));
            var group = matches.Groups.Single(g => g.Orientation == MatchOrientation.Horizontal);
            Assert.That(group.Length, Is.EqualTo(length));
            Assert.That(group.CatType, Is.EqualTo(CatType.Orange));
            Assert.That(group.Cells, Is.EqualTo(Enumerable.Range(0, length).Select(x => new BoardPosition(x, 2))));
        }

        [Test]
        public void VerticalRunIsReportedFromBottomToTop()
        {
            var cells = CleanCells();
            for (int y = 0; y < Board.Height; y++)
                cells[1, y] = y < 5 ? CatType.Orange : CatType.Black;
            var matches = MatchFinder.Find(new Board(cells));
            var group = matches.Groups.Single();
            Assert.That(group.Orientation, Is.EqualTo(MatchOrientation.Vertical));
            Assert.That(group.Length, Is.EqualTo(5));
            Assert.That(group.Cells, Is.EqualTo(Enumerable.Range(0, 5).Select(y => new BoardPosition(1, y))));
        }

        [Test]
        public void DiagonalsAndPairsDoNotMatch()
        {
            var cells = CleanCells();
            cells[0, 0] = cells[1, 1] = cells[2, 2] = CatType.Orange;
            Assert.That(MatchFinder.Find(new Board(cells)).HasMatches, Is.False);
        }

        [Test]
        public void IntersectingAndSimultaneousRunsRetainGroupsButDeduplicateCells()
        {
            var cells = CleanCells();
            for (int i = 0; i < 7; i++)
            {
                cells[i, 3] = CatType.Black;
                cells[3, i] = CatType.Black;
            }
            for (int i = 1; i <= 5; i++)
            {
                cells[i, 3] = CatType.Orange;
                cells[3, i] = CatType.Orange;
            }
            cells[0, 6] = cells[1, 6] = cells[2, 6] = CatType.Siamese;
            var matches = MatchFinder.Find(new Board(cells));
            Assert.That(matches.Groups.Select(g => g.Length), Is.EquivalentTo(new[] { 5, 5, 3 }));
            Assert.That(matches.Groups.Count(g => g.Orientation == MatchOrientation.Vertical), Is.EqualTo(1));
            Assert.That(matches.Cells.Count, Is.EqualTo(12));
            Assert.That(matches.Cells.Distinct().Count(), Is.EqualTo(12));
            Assert.That(matches.Cells.Count(p => p == new BoardPosition(3, 3)), Is.EqualTo(1));
        }

        [Test]
        public void ValidSwapMutatesExactlyTheTwoRequestedCellsAndReturnsMatches()
        {
            var board = PrototypeLayout.CreateBoard();
            var before = board.CopyCells();
            var a = new BoardPosition(1, 0);
            var b = new BoardPosition(1, 1);
            var result = board.TrySwap(a, b);
            Assert.That(result.Status, Is.EqualTo(SwapStatus.Accepted));
            Assert.That(result.Matches.Contains(a), Is.True);
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++)
                {
                    var p = new BoardPosition(x, y);
                    var expected = p == a ? before[b.X, b.Y] : p == b ? before[a.X, a.Y] : before[x, y];
                    Assert.That(board.GetCell(p), Is.EqualTo(expected), p.ToString());
                }
        }

        [Test]
        public void AdjacentSwapWithoutMatchRollsBackTheEntireBoard()
        {
            var board = new Board(CleanCells());
            var before = board.CopyCells();
            var result = board.TrySwap(new BoardPosition(0, 0), new BoardPosition(1, 0));
            Assert.That(result.Status, Is.EqualTo(SwapStatus.NoMatch));
            Assert.That(result.Matches.HasMatches, Is.False);
            Assert.That(board.CopyCells(), Is.EqualTo(before));
        }

        [TestCase(0, 0, 1, 1, SwapStatus.NotAdjacent)]
        [TestCase(0, 0, 0, 0, SwapStatus.NotAdjacent)]
        [TestCase(0, 0, 2, 0, SwapStatus.NotAdjacent)]
        [TestCase(-1, 0, 0, 0, SwapStatus.OutOfBounds)]
        [TestCase(6, 6, 7, 6, SwapStatus.OutOfBounds)]
        [TestCase(0, 0, 0, 7, SwapStatus.OutOfBounds)]
        public void InvalidCoordinatesOrNonNeighborsAreRejectedWithoutMutation(
            int ax, int ay, int bx, int by, SwapStatus expected)
        {
            var board = PrototypeLayout.CreateBoard();
            var before = board.CopyCells();
            Assert.That(board.TrySwap(new BoardPosition(ax, ay), new BoardPosition(bx, by)).Status,
                Is.EqualTo(expected));
            Assert.That(board.CopyCells(), Is.EqualTo(before));
        }

        [Test]
        public void UnrelatedExistingMatchDoesNotLegalizeASwap()
        {
            var cells = CleanCells();
            cells[0, 6] = cells[1, 6] = cells[2, 6] = CatType.Gray;
            var board = new Board(cells);
            var before = board.CopyCells();
            Assert.That(MatchFinder.Find(board).HasMatches, Is.True);
            var result = board.TrySwap(new BoardPosition(0, 0), new BoardPosition(1, 0));
            Assert.That(result.IsValid, Is.False);
            Assert.That(board.CopyCells(), Is.EqualTo(before));
            // Swapping identical members of that existing run creates nothing either.
            Assert.That(board.TrySwap(new BoardPosition(0, 6), new BoardPosition(1, 6)).IsValid, Is.False);
            Assert.That(board.CopyCells(), Is.EqualTo(before));
        }

        [Test]
        public void RepeatedDetectionIsDeterministicAndDoesNotMutateBoardOrOldResults()
        {
            var board = PrototypeLayout.CreateBoard();
            var original = board.TrySwap(new BoardPosition(1, 0), new BoardPosition(1, 1)).Matches;
            var before = board.CopyCells();
            var first = MatchFinder.Find(board);
            var second = MatchFinder.Find(board);
            Assert.That(second.Cells, Is.EqualTo(first.Cells));
            Assert.That(second.Groups.Select(g => g.Orientation), Is.EqualTo(first.Groups.Select(g => g.Orientation)));
            Assert.That(board.CopyCells(), Is.EqualTo(before));
            Assert.That(original.Cells, Is.EqualTo(first.Cells));
            var originalCells = original.Cells.ToArray();
            Assert.That(board.TrySwap(new BoardPosition(4, 5), new BoardPosition(5, 5)).IsValid, Is.True);
            Assert.That(original.Cells, Is.EqualTo(originalCells), "Old match results must remain snapshots after another move.");
        }

        [Test]
        public void LayoutOwnershipAndSafeAccessProtectBoardState()
        {
            var cells = CleanCells();
            var board = new Board(cells);
            cells[0, 0] = CatType.Siamese;
            var copy = board.CopyCells();
            copy[0, 0] = CatType.Black;
            Assert.That(board.GetCell(new BoardPosition(0, 0)), Is.EqualTo(CatType.Orange));
            Assert.That(board.TryGetCell(new BoardPosition(6, 6), out var corner), Is.True);
            Assert.That(corner, Is.EqualTo(board.GetCell(new BoardPosition(6, 6))));
            Assert.That(board.TryGetCell(new BoardPosition(int.MinValue, 0), out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => board.GetCell(new BoardPosition(0, -1)));
            Assert.Throws<ArgumentException>(() => new Board(new CatType[6, 7]));
            cells[0, 0] = (CatType)99;
            Assert.Throws<ArgumentException>(() => new Board(cells));
        }

        [Test]
        public void PrototypeStartsCleanContainsAllCatsAndHasMultipleValidMoves()
        {
            var board = PrototypeLayout.CreateBoard();
            Assert.That(board.CopyCells(), Is.EqualTo(PrototypeLayout.CreateBoard().CopyCells()));
            Assert.That(board.CopyCells().Cast<CatType>().Distinct(), Is.EquivalentTo(Enum.GetValues(typeof(CatType))));
            Assert.That(MatchFinder.Find(board).HasMatches, Is.False);
            int validMoves = 0;
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++)
                {
                    var p = new BoardPosition(x, y);
                    if (x + 1 < Board.Width && PrototypeLayout.CreateBoard().TrySwap(p, new BoardPosition(x + 1, y)).IsValid)
                        validMoves++;
                    if (y + 1 < Board.Height && PrototypeLayout.CreateBoard().TrySwap(p, new BoardPosition(x, y + 1)).IsValid)
                        validMoves++;
                }
            Assert.That(validMoves, Is.GreaterThanOrEqualTo(2));
            Assert.That(PrototypeLayout.CreateBoard().TrySwap(new BoardPosition(4, 5), new BoardPosition(5, 5)).IsValid, Is.True);
            Assert.That(PrototypeLayout.CreateBoard().TrySwap(new BoardPosition(3, 0), new BoardPosition(4, 0)).IsValid, Is.False);
        }
    }
}
