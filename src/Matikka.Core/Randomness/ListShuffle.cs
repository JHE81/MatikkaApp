using System.Collections.Generic;

namespace Matikka.Core.Randomness
{
    internal static class ListShuffle
    {
        /// <summary>Fisher–Yates-sekoitus deterministisellä generaattorilla.</summary>
        public static void Shuffle<T>(IList<T> list, SeededRandom random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(0, i);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
