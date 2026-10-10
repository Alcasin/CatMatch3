using System;
using System.Collections.Generic;

namespace CatMatch3.Core
{
    public sealed class ResolutionWave
    {
        public MatchResult Matches { get; }
        public BoardSnapshot Before { get; }
        public BoardSnapshot AfterClear { get; }
        public BoardSnapshot AfterGravity { get; }
        public BoardSnapshot AfterRefill { get; }
        public IReadOnlyList<TileMovement> Movements { get; }
        public IReadOnlyList<TileSpawn> Spawns { get; }

        internal ResolutionWave(MatchResult matches, BoardSnapshot before, BoardSnapshot clear,
            BoardSnapshot gravity, BoardSnapshot refill, IReadOnlyList<TileMovement> movements,
            IReadOnlyList<TileSpawn> spawns)
        {
            Matches = matches; Before = before; AfterClear = clear;
            AfterGravity = gravity; AfterRefill = refill; Movements = movements; Spawns = spawns;
        }
    }

    public sealed class ReshuffleResult
    {
        public bool Success { get; }
        public string Error { get; }
        public int Attempts { get; }
        public BoardSnapshot Before { get; }
        public BoardSnapshot After { get; }
        internal ReshuffleResult(bool success, string error, int attempts, BoardSnapshot before, BoardSnapshot after)
        { Success = success; Error = error; Attempts = attempts; Before = before; After = after; }
    }

    public sealed class ResolutionResult
    {
        public bool Success { get; }
        public string Error { get; }
        public IReadOnlyList<ResolutionWave> Waves { get; }
        public int WaveCount => Waves.Count;
        public int CascadeCount => Math.Max(0, WaveCount - 1);
        public BoardSnapshot Initial { get; }
        public BoardSnapshot Final { get; }
        public ReshuffleResult Reshuffle { get; }

        internal ResolutionResult(bool success, string error, List<ResolutionWave> waves,
            BoardSnapshot initial, BoardSnapshot final, ReshuffleResult reshuffle)
        {
            Success = success; Error = error; Waves = Array.AsReadOnly(waves.ToArray());
            Initial = initial; Final = final; Reshuffle = reshuffle;
        }
    }
}
