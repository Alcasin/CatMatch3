using System;

namespace CatMatch3.Core
{
    public interface IRandomSource
    {
        /// <summary>Return an integer in [0, maxExclusive).</summary>
        int NextInt(int maxExclusive);
    }

    /// <summary>Fixed xorshift32 algorithm, independent of framework Random implementations.</summary>
    public sealed class SeededRandom : IRandomSource
    {
        private uint state;
        public SeededRandom(uint seed) { state = seed == 0 ? 0x6d2b79f5u : seed; }
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            // Xorshift visits nonzero uints. Reject the tail to avoid modulo bias.
            uint bound = (uint)maxExclusive;
            uint limit = uint.MaxValue - uint.MaxValue % bound;
            uint sample;
            do
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                sample = state - 1;
            } while (sample >= limit);
            return (int)(sample % bound);
        }
    }

    internal static class RandomSource
    {
        internal static int Draw(IRandomSource random, int bound)
        {
            int value = random.NextInt(bound);
            if (value < 0 || value >= bound)
                throw new InvalidOperationException("Random source returned an out-of-range value.");
            return value;
        }
    }
}
