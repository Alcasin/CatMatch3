using System;
using System.Collections.Generic;
using CatMatch3.Core;
using UnityEngine;

namespace CatMatch3.Runtime
{
    [CreateAssetMenu(menuName = "CatMatch3/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Serializable] public struct Cell { public CatType cat; public StripeOrientation stripe; }
        [Serializable] public struct Collection { public CatType cat; public int count; }
        [SerializeField] private string identifier;
        [SerializeField] private string displayName;
        [SerializeField] private int width = 7, height = 7;
        [Tooltip("49 cells: index = y * 7 + x. Bottom row first, left to right.")]
        [SerializeField] private Cell[] cells = new Cell[49];
        [SerializeField] private int moveLimit;
        [SerializeField] private Collection[] collections = Array.Empty<Collection>();
        [SerializeField] private Vector2Int[] pawCells = Array.Empty<Vector2Int>();
        [SerializeField] private bool useDevelopmentSeed = true;
        [SerializeField] private int developmentSeed = 1337;

        public LevelConfiguration ToConfiguration()
        {
            try
            {
                if (cells == null || collections == null || pawCells == null)
                    throw new ArgumentException("Layout and objective arrays must be assigned.");
                var tiles = new Tile[cells.Length];
                for (int i = 0; i < cells.Length; i++)
                {
                    if (!Enum.IsDefined(typeof(CatType), cells[i].cat) || !Enum.IsDefined(typeof(StripeOrientation), cells[i].stripe))
                        throw new ArgumentException($"Unknown cat or stripe at cell index {i}.");
                    tiles[i] = new Tile(cells[i].cat, cells[i].stripe);
                }
                var targets = new Dictionary<CatType, int>();
                foreach (var target in collections)
                {
                    if (targets.ContainsKey(target.cat)) throw new ArgumentException("Duplicate collection target: " + target.cat);
                    targets.Add(target.cat, target.count);
                }
                var paws = new BoardPosition[pawCells.Length];
                for (int i = 0; i < paws.Length; i++) paws[i] = new BoardPosition(pawCells[i].x, pawCells[i].y);
                return new LevelConfiguration(identifier, displayName, width, height, tiles, moveLimit, targets, paws,
                    useDevelopmentSeed ? unchecked((uint)developmentSeed) : (uint?)null);
            }
            catch (Exception exception) { throw new ArgumentException($"Level asset '{name}': {exception.Message}", exception); }
        }

#if UNITY_EDITOR
        public bool MatchesConfiguration(LevelConfiguration expected)
        {
            var actual = ToConfiguration();
            if (actual.Identifier != expected.Identifier || actual.DisplayName != expected.DisplayName ||
                actual.MoveLimit != expected.MoveLimit || actual.DevelopmentSeed != expected.DevelopmentSeed ||
                actual.CollectionTargets.Count != expected.CollectionTargets.Count || actual.PawCells.Count != expected.PawCells.Count)
                return false;
            foreach (var target in expected.CollectionTargets)
                if (!actual.CollectionTargets.TryGetValue(target.Key, out var count) || count != target.Value) return false;
            for (int i = 0; i < actual.PawCells.Count; i++) if (actual.PawCells[i] != expected.PawCells[i]) return false;
            var a = actual.CopyCells(); var b = expected.CopyCells();
            for (int i = 0; i < a.Length; i++) if (!a[i].Equals(b[i])) return false;
            return true;
        }

        // Used for new assets and revision upgrades after an exact generated-baseline match.
        public void InitializeDefaults(int number)
        {
            var source = AuthoredLevels.Create(number);
            identifier = source.Identifier; displayName = source.DisplayName; width = height = 7;
            moveLimit = source.MoveLimit; useDevelopmentSeed = true;
            developmentSeed = unchecked((int)source.DevelopmentSeed.Value);
            var tiles = source.CopyCells(); cells = new Cell[tiles.Length];
            for (int i = 0; i < tiles.Length; i++) cells[i] = new Cell { cat = tiles[i].Type, stripe = tiles[i].Stripe };
            collections = new Collection[source.CollectionTargets.Count]; int index = 0;
            foreach (var target in source.CollectionTargets) collections[index++] = new Collection { cat = target.Key, count = target.Value };
            pawCells = new Vector2Int[source.PawCells.Count];
            for (int i = 0; i < pawCells.Length; i++) pawCells[i] = new Vector2Int(source.PawCells[i].X, source.PawCells[i].Y);
        }
#endif
    }
}
