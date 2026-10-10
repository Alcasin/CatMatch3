using System;
using System.Collections.Generic;

namespace CatMatch3.Core
{
    public static class MatchFinder
    {
        public static MatchResult Find(Board board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            var groups = new List<MatchGroup>();
            Scan(board, MatchOrientation.Horizontal, groups);
            Scan(board, MatchOrientation.Vertical, groups);
            return groups.Count == 0 ? MatchResult.Empty : new MatchResult(groups);
        }

        private static void Scan(Board board, MatchOrientation orientation, List<MatchGroup> groups)
        {
            bool horizontal = orientation == MatchOrientation.Horizontal;
            int lines = horizontal ? Board.Height : Board.Width;
            int length = horizontal ? Board.Width : Board.Height;
            for (int line = 0; line < lines; line++)
            {
                int start = 0;
                while (start < length)
                {
                    var first = Position(horizontal, line, start);
                    if (!board.TryGetCell(first, out var type)) { start++; continue; }
                    int end = start + 1;
                    while (end < length && board.TryGetCell(Position(horizontal, line, end), out var next) && next == type)
                        end++;

                    if (end - start >= 3)
                    {
                        var cells = new List<BoardPosition>();
                        for (int index = start; index < end; index++)
                            cells.Add(Position(horizontal, line, index));
                        groups.Add(new MatchGroup(type, orientation, cells));
                    }
                    start = end;
                }
            }
        }

        private static BoardPosition Position(bool horizontal, int line, int index)
        {
            return horizontal ? new BoardPosition(index, line) : new BoardPosition(line, index);
        }
    }
}
