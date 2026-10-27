using System;

namespace Matikka.Core.Randomness
{
    /// <summary>
    /// Deterministinen satunnaislukugeneraattori (SplitMix64).
    /// Sama siemen tuottaa saman tehtävän kaikilla alustoilla (Android, iOS, palvelin, Unity),
    /// joten pelitapahtumaan riittää tallentaa taso + siemen ja tehtävä voidaan toistaa myöhemmin.
    /// System.Random-luokan algoritmi ei ole taattu samaksi eri ajoympäristöissä.
    /// </summary>
    public sealed class SeededRandom
    {
        private ulong _state;

        public SeededRandom(long seed)
        {
            _state = unchecked((ulong)seed);
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Kokonaisluku väliltä [minInclusive, maxInclusive].</summary>
        public int Next(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxInclusive));

            ulong range = (ulong)((long)maxInclusive - minInclusive + 1);
            return (int)((long)minInclusive + (long)(NextUInt64() % range));
        }

        /// <summary>Liukuluku väliltä [0, 1).</summary>
        public double NextDouble()
        {
            return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
        }

        public bool NextBool()
        {
            return (NextUInt64() & 1UL) == 1UL;
        }

        /// <summary>Nollasta poikkeava luku väliltä [-maxAbs, -minAbs] ∪ [minAbs, maxAbs].</summary>
        public int NextNonZero(int minAbs, int maxAbs)
        {
            int value = Next(minAbs, maxAbs);
            return NextBool() ? value : -value;
        }

        /// <summary>Uusi siemen seuraavaa tehtävää varten.</summary>
        public long NextSeed()
        {
            return unchecked((long)NextUInt64());
        }
    }
}
