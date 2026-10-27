using System;
using System.Collections.Generic;

namespace Matikka.Core.Games.NumberHop
{
    /// <summary>Leveyshaku tiloihin (luku, käytetyt kortit): vähiten kortteja tavoitteeseen.</summary>
    public static class NumberHopSolver
    {
        /// <returns>Korttien tunnisteet järjestyksessä, tai null jos tavoitetta ei saavuteta.</returns>
        public static IReadOnlyList<int>? Solve(int start, int target, IReadOnlyList<NumberCard> hand, int usedMask = 0)
        {
            if (hand.Count > 16)
                throw new ArgumentException("Liikaa kortteja.", nameof(hand));

            if (start == target)
                return Array.Empty<int>();

            var parents = new Dictionary<(int Value, int Mask), ((int, int) Parent, int CardIndex)>();
            var seen = new HashSet<(int, int)> { (start, usedMask) };
            var queue = new Queue<(int Value, int Mask)>();
            queue.Enqueue((start, usedMask));

            while (queue.Count > 0)
            {
                var (value, mask) = queue.Dequeue();
                for (int i = 0; i < hand.Count; i++)
                {
                    if ((mask & (1 << i)) != 0)
                        continue;
                    if (hand[i].TryApply(value, out int next) != CardError.None)
                        continue;

                    var state = (next, mask | (1 << i));
                    if (!seen.Add(state))
                        continue;

                    parents[state] = ((value, mask), i);
                    if (next == target)
                        return Path(parents, (start, usedMask), state, hand);

                    queue.Enqueue(state);
                }
            }

            return null;
        }

        private static IReadOnlyList<int> Path(
            Dictionary<(int, int), ((int, int) Parent, int CardIndex)> parents, (int, int) start, (int, int) end, IReadOnlyList<NumberCard> hand)
        {
            var path = new List<int>();
            var state = end;
            while (!state.Equals(start))
            {
                var (parent, index) = parents[state];
                path.Add(hand[index].Id);
                state = parent;
            }

            path.Reverse();
            return path;
        }
    }
}
