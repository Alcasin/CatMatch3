using System;
using System.Collections.Generic;

namespace CatMatch3.Core
{
    /// <summary>Two fixed authoring defaults, shared by Editor asset creation and Core tests.</summary>
    public static class AuthoredLevels
    {
        public static LevelConfiguration Create(int number)
        {
            var previous = CreatePreviousM4(number);
            if (number == 1)
                return new LevelConfiguration(previous.Identifier, previous.DisplayName, 7, 7, previous.CopyCells(), 3,
                    new Dictionary<CatType, int> { { CatType.Orange, 6 }, { CatType.Gray, 9 } }, Array.Empty<BoardPosition>(), 1339);
            return new LevelConfiguration(previous.Identifier, previous.DisplayName, 7, 7, previous.CopyCells(), 4,
                new Dictionary<CatType, int>(), new[] { new BoardPosition(0, 0), new BoardPosition(1, 0),
                    new BoardPosition(0, 1), new BoardPosition(1, 1), new BoardPosition(0, 2),
                    new BoardPosition(1, 2), new BoardPosition(3, 3), new BoardPosition(3, 4) }, 141);
        }

        // Immutable migration baseline: existing generated M4 assets may be upgraded, custom data may not.
        public static LevelConfiguration CreatePreviousM4(int number)
        {
            if (number == 1)
                return new LevelConfiguration("level-1", "Collect the Cats", 7, 7,
                    ParseRows("WGSOBGG", "OBWGGOB", "GSOBWGS", "BWGSOBW", "SOBWGSO", "WOSOBWG", "OBOGSOB"),
                    18, new Dictionary<CatType, int> { { CatType.Orange, 12 }, { CatType.Gray, 10 } },
                    Array.Empty<BoardPosition>(), 1337);
            if (number == 2)
                return new LevelConfiguration("level-2", "Clear the Paw Prints", 7, 7,
                    ParseRows("BWGSOBW", "SOBWGSO", "WGSOBGG", "GSOBWGS", "OBWGGOB", "WOSOBWG", "OBOGSOB"),
                    22, new Dictionary<CatType, int>(), new[] { new BoardPosition(1, 0), new BoardPosition(5, 0),
                        new BoardPosition(0, 2), new BoardPosition(3, 2), new BoardPosition(6, 3),
                        new BoardPosition(2, 4), new BoardPosition(4, 5), new BoardPosition(1, 6) }, 141);
            throw new ArgumentOutOfRangeException(nameof(number), "Only Level 1 and Level 2 exist.");
        }

        // Human-readable rows are top to bottom; assets store bottom to top, left to right.
        private static Tile[] ParseRows(params string[] topToBottom)
        {
            const string symbols = "OBWGS";
            var cells = new Tile[49];
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 7; x++) cells[y * 7 + x] = new Tile((CatType)symbols.IndexOf(topToBottom[6 - y][x]));
            return cells;
        }
    }
}
