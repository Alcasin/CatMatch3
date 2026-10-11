using System.Collections.Generic;

namespace CatMatch3.Core
{
    public enum M3QaScenario { Original, HorizontalCreation, VerticalCreation, HorizontalActivation, VerticalActivation, Chain, Objectives }

    /// <summary>Explicit development fixtures, not level definitions. Runtime QA menus are Editor-only.</summary>
    public static class M3PrototypeLayout
    {
        public static Board CreateBoard(M3QaScenario scenario)
        {
            var layout = PrototypeLayout.CreateBoard().CopyTiles();
            if (scenario == M3QaScenario.HorizontalCreation || scenario == M3QaScenario.VerticalCreation)
                layout[3, 0] = new Tile(CatType.Orange);
            if (scenario == M3QaScenario.HorizontalActivation || scenario == M3QaScenario.VerticalActivation ||
                scenario == M3QaScenario.Chain || scenario == M3QaScenario.Objectives)
                layout[0, 0] = new Tile(CatType.Orange, StripeOrientation.Horizontal);
            if (scenario == M3QaScenario.Chain)
            {
                layout[4, 0] = new Tile(layout[4, 0].Type, StripeOrientation.Vertical);
                layout[4, 4] = new Tile(layout[4, 4].Type, StripeOrientation.Horizontal);
            }
            if (scenario == M3QaScenario.VerticalCreation || scenario == M3QaScenario.VerticalActivation)
            {
                var transposed = new Tile[Board.Width, Board.Height];
                for (int x = 0; x < Board.Width; x++)
                    for (int y = 0; y < Board.Height; y++)
                    {
                        var tile = layout[x, y];
                        var stripe = tile.Stripe == StripeOrientation.Horizontal ? StripeOrientation.Vertical : tile.Stripe;
                        transposed[y, x] = new Tile(tile.Type, stripe);
                    }
                layout = transposed;
            }
            return new Board(layout);
        }

        public static ObjectiveTracker CreateObjectives()
        {
            var marks = new List<BoardPosition>();
            for (int x = 0; x < Board.Width; x++) marks.Add(new BoardPosition(x, 0));
            marks.Add(new BoardPosition(1, 1));
            return new ObjectiveTracker(new Dictionary<CatType, int>
                { { CatType.Orange, 12 }, { CatType.Gray, 10 } }, marks);
        }

        public static BoardPosition SwapDestination(M3QaScenario scenario)
        {
            return scenario == M3QaScenario.VerticalCreation || scenario == M3QaScenario.VerticalActivation
                ? new BoardPosition(0, 1) : new BoardPosition(1, 0);
        }
    }
}
