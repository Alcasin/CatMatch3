namespace CatMatch3.Core
{
    /// <summary>Temporary M1 fixture, not a production level definition or board generator.</summary>
    public static class PrototypeLayout
    {
        public static Board CreateBoard()
        {
            // Display order: top row first. O/B/W/G/S map to the five CatType values.
            string[] rows =
            {
                "WGSOBGG",
                "OBWGGOB",
                "GSOBWGS",
                "BWGSOBW",
                "SOBWGSO",
                "WOSOBWG",
                "OBOGSOB"
            };
            var layout = new CatType[Board.Width, Board.Height];
            const string symbols = "OBWGS";
            for (int row = 0; row < Board.Height; row++)
                for (int x = 0; x < Board.Width; x++)
                    layout[x, Board.Height - 1 - row] = (CatType)symbols.IndexOf(rows[row][x]);
            return new Board(layout);
        }

        /// <summary>Stable but without legal moves; a deterministic development/test fixture.</summary>
        public static Board CreateDeadlockedBoard()
        {
            var layout = new CatType[Board.Width, Board.Height];
            for (int x = 0; x < Board.Width; x++)
                for (int y = 0; y < Board.Height; y++) layout[x, y] = (CatType)((x + 2 * y) % 5);
            return new Board(layout);
        }
    }
}
