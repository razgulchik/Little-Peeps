using System;
using System.Collections.Generic;

namespace LittlePeeps
{
    // The island generator's own random source: PCG32 (O'Neill), 64-bit state, 32-bit output.
    //
    // Not System.Random on purpose. A run's island must come back identical from its seed — that is
    // how a bug report ("seed 4127, age 3") gets reproduced and, later, how a save can store the island
    // as a seed. System.Random's seeded sequence is an implementation detail that has already changed
    // between .NET runtimes, and Unity ships two (Mono / IL2CPP); twelve lines of PCG are the same
    // everywhere. The whole state is two ulongs, so a snapshot for a rollback is a plain copy.
    //
    // The sampling helpers mirror what the prototype used from Python's `random` (uniform, randint,
    // choice, shuffle), so the ported algorithm reads like the original.
    public sealed class IslandRng
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong DefaultStream = 0x853c49e6748fea9bUL;

        private ulong state;
        private readonly ulong inc;

        public IslandRng(int seed) : this(unchecked((ulong)seed), DefaultStream) { }

        public IslandRng(ulong seed, ulong stream)
        {
            inc = (stream << 1) | 1;   // must be odd
            state = 0;
            NextUInt();
            state += seed;
            NextUInt();
        }

        // An independent generator keyed by (seed, parts...): the prototype's
        // `Random(f'{seed}:resources:{index}:{biome}:{attempt}')`. Content generation draws from streams
        // made this way so it never touches the shape stream — retrying a section's content, or
        // populating three candidates, cannot change which shapes the island grows next.
        public static IslandRng Derive(int seed, params ulong[] parts)
        {
            ulong h = unchecked((ulong)seed);
            foreach (var part in parts) h = Mix(h ^ part);
            return new IslandRng(Mix(h), Mix(h ^ 0x9E3779B97F4A7C15UL));
        }

        // FNV-1a of a string, for naming a stream after a biome id.
        public static ulong Hash(string text)
        {
            ulong h = 14695981039346656037UL;
            if (text != null)
                foreach (char c in text) h = unchecked((h ^ c) * 1099511628211UL);
            return h;
        }

        // SplitMix64 finaliser: spreads a small integer over all 64 bits.
        private static ulong Mix(ulong z)
        {
            z = unchecked(z + 0x9E3779B97F4A7C15UL);
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            return z ^ (z >> 31);
        }

        public uint NextUInt()
        {
            ulong old = state;
            state = unchecked(old * Multiplier + inc);
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        // [0, 1)
        public double NextDouble() => NextUInt() * (1.0 / 4294967296.0);

        // [a, b)
        public double Uniform(double a, double b) => a + (b - a) * NextDouble();

        // Both bounds INCLUSIVE, like Python's randint. Multiply-shift rather than modulo: no division,
        // and for the tiny spans used here the bias is far below anything the generator could notice.
        public int Range(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive)
                throw new ArgumentException($"Empty range [{minInclusive}, {maxInclusive}].");
            ulong span = (ulong)(maxInclusive - minInclusive) + 1;
            return minInclusive + (int)(((ulong)NextUInt() * span) >> 32);
        }

        public T Choice<T>(IReadOnlyList<T> options)
        {
            if (options.Count == 0) throw new ArgumentException("Cannot choose from an empty list.");
            return options[Range(0, options.Count - 1)];
        }

        // One option, with probability proportional to its weight (Python's random.choices, k=1).
        // Weights must be non-negative and not all zero.
        public T WeightedChoice<T>(IReadOnlyList<T> options, IReadOnlyList<double> weights)
        {
            if (options.Count == 0 || options.Count != weights.Count)
                throw new ArgumentException("WeightedChoice needs one weight per option.");
            double total = 0;
            for (int i = 0; i < weights.Count; i++) total += weights[i];
            if (total <= 0) throw new ArgumentException("WeightedChoice needs a positive total weight.");

            double roll = NextDouble() * total;
            for (int i = 0; i < options.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0) return options[i];
            }
            return options[options.Count - 1];   // rounding at the very top of the range
        }

        // Fisher–Yates, in place.
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
