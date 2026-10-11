#if UNITY_EDITOR || CATMATCH3_OFFLINE
using System;
using System.Collections.Generic;

namespace CatMatch3.Core
{
    /// <summary>A genuine top-row four-run leaves a stable striped deadlock, with no replacement/shuffle.</summary>
    public static class PuzzleDiagnostics
    {
        public static LevelConfiguration NoLegalMoves(bool completeObjective = false, int moves = 3)
        {
            var board = PrototypeLayout.CreateDeadlockedBoard();
            var cells = new Tile[49];
            for (int i = 0; i < cells.Length; i++) cells[i] = board.GetTile(new BoardPosition(i % 7, i / 7));
            cells[6 * 7] = cells[6 * 7 + 1] = new Tile(CatType.Orange);
            cells[6 * 7 + 2] = new Tile(CatType.White);
            cells[5 * 7 + 2] = new Tile(CatType.Orange);
            // The top four Orange cats preserve a stripe at (0,6). Seed 300 then fills columns 1..3
            // with White, Gray, Siamese, leaving a stable board with no legal swaps.
            return new LevelConfiguration("qa-no-moves", "No legal moves fixture", 7, 7, cells, moves,
                new Dictionary<CatType, int> { { CatType.Orange, completeObjective ? 3 : 99 } },
                Array.Empty<BoardPosition>(), 300);
        }
    }
}
#endif
