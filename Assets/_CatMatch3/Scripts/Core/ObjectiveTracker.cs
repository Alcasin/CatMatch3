using System;
using System.Collections.Generic;

namespace CatMatch3.Core
{
    public readonly struct CatCollectionProgress
    {
        public CatType Type { get; }
        public int Target { get; }
        public int Collected { get; }
        public bool IsComplete => Collected >= Target;
        internal CatCollectionProgress(CatType type, int target, int collected)
        { Type = type; Target = target; Collected = collected; }
    }

    public sealed class ObjectiveSnapshot
    {
        public IReadOnlyList<CatCollectionProgress> Collections { get; }
        public IReadOnlyList<BoardPosition> RemainingMarks { get; }
        public int TotalPaws { get; }
        public int ClearedPaws => TotalPaws - RemainingMarks.Count;
        public bool IsComplete { get; }
        internal ObjectiveSnapshot(List<CatCollectionProgress> collections, List<BoardPosition> remaining, int total)
        {
            Collections = Array.AsReadOnly(collections.ToArray());
            RemainingMarks = Array.AsReadOnly(remaining.ToArray());
            TotalPaws = total;
            bool complete = remaining.Count == 0;
            foreach (var collection in collections) complete &= collection.IsComplete;
            IsComplete = complete;
        }
    }

    /// <summary>Persistent progress and fixed cell marks; tile movement and shuffle do not touch it.</summary>
    public sealed class ObjectiveTracker
    {
        private readonly int[] targets = new int[5];
        private readonly int[] collected = new int[5];
        private readonly List<BoardPosition> initialMarks = new List<BoardPosition>();
        private readonly HashSet<BoardPosition> remaining = new HashSet<BoardPosition>();

        public ObjectiveTracker(IReadOnlyDictionary<CatType, int> collectionTargets,
            IReadOnlyList<BoardPosition> markedCells)
        {
            if (collectionTargets == null) throw new ArgumentNullException(nameof(collectionTargets));
            if (markedCells == null) throw new ArgumentNullException(nameof(markedCells));
            foreach (var target in collectionTargets)
            {
                if (!Enum.IsDefined(typeof(CatType), target.Key) || target.Value <= 0)
                    throw new ArgumentException("Collection targets need a known cat type and positive count.", nameof(collectionTargets));
                targets[(int)target.Key] = target.Value;
            }
            foreach (var cell in markedCells)
            {
                if (!Board.IsValidPosition(cell) || !remaining.Add(cell))
                    throw new ArgumentException("Marks must be unique valid board cells.", nameof(markedCells));
                initialMarks.Add(cell);
            }
        }

        public ObjectiveSnapshot Capture()
        {
            var progress = new List<CatCollectionProgress>();
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] > 0) progress.Add(new CatCollectionProgress((CatType)i, targets[i], collected[i]));
            var marks = new List<BoardPosition>();
            foreach (var cell in initialMarks) if (remaining.Contains(cell)) marks.Add(cell);
            return new ObjectiveSnapshot(progress, marks, initialMarks.Count);
        }

        public void Reset()
        {
            Array.Clear(collected, 0, collected.Length);
            remaining.Clear();
            foreach (var cell in initialMarks) remaining.Add(cell);
        }

        internal void RecordRemovals(IReadOnlyList<TileRemoval> removals)
        {
            // The wave already owns a unique union. Defensively ignore duplicate positions as well.
            var seen = new HashSet<BoardPosition>();
            foreach (var removal in removals)
            {
                if (!seen.Add(removal.Position)) continue;
                if (!removal.Tile.IsSpecial && targets[(int)removal.Tile.Type] > 0)
                    collected[(int)removal.Tile.Type]++;
                remaining.Remove(removal.Position);
            }
        }
    }
}
