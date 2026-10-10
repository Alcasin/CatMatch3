using System;
using System.Collections.Generic;

namespace CatMatch3.Core
{
    public enum MatchOrientation { Horizontal, Vertical }

    /// <summary>A maximal straight run. Intersections remain separate groups.</summary>
    public sealed class MatchGroup
    {
        public CatType CatType { get; }
        public MatchOrientation Orientation { get; }
        public IReadOnlyList<BoardPosition> Cells { get; }
        public int Length => Cells.Count;

        internal MatchGroup(CatType type, MatchOrientation orientation, List<BoardPosition> cells)
        {
            CatType = type;
            Orientation = orientation;
            Cells = Array.AsReadOnly(cells.ToArray());
        }
    }

    public sealed class MatchResult
    {
        public static MatchResult Empty { get; } = new MatchResult(new List<MatchGroup>());
        public IReadOnlyList<MatchGroup> Groups { get; }
        /// <summary>Unique coordinates, in deterministic first-detected order.</summary>
        public IReadOnlyList<BoardPosition> Cells { get; }
        public bool HasMatches => Cells.Count > 0;

        internal MatchResult(List<MatchGroup> groups)
        {
            Groups = Array.AsReadOnly(groups.ToArray());
            var unique = new HashSet<BoardPosition>();
            var cells = new List<BoardPosition>();
            foreach (var group in groups)
                foreach (var cell in group.Cells)
                    if (unique.Add(cell)) cells.Add(cell);
            Cells = Array.AsReadOnly(cells.ToArray());
        }

        public bool Contains(BoardPosition position)
        {
            foreach (var cell in Cells)
                if (cell == position) return true;
            return false;
        }
    }
}
