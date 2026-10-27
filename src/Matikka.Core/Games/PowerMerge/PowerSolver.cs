using System;
using System.Collections.Generic;

namespace Matikka.Core.Games.PowerMerge
{
    public enum Reachability
    {
        Reachable,

        /// <summary>Tavoitetta ei saa enää koottua mistään siirtosarjasta.</summary>
        Unreachable,

        /// <summary>Haku katkesi rajaan ennen vastausta. Käsitellään kuin tavoite olisi mahdollinen.</summary>
        Unknown,
    }

    public sealed class SolveResult
    {
        public SolveResult(Reachability status, IReadOnlyList<Direction> path)
        {
            Status = status;
            Path = path;
        }

        public Reachability Status { get; }

        /// <summary>Lyhin siirtosarja tavoitteeseen (tyhjä, jos ei löytynyt tai tavoite on jo laudalla).</summary>
        public IReadOnlyList<Direction> Path { get; }
    }

    /// <summary>Leveyshaku: lyhin siirtosarja, jolla kaikki tavoitelaatat ovat laudalla yhtä aikaa.</summary>
    public static class PowerSolver
    {
        public static readonly IReadOnlyList<Direction> Directions =
            new[] { Direction.Left, Direction.Right, Direction.Up, Direction.Down };

        public static SolveResult Solve(PowerBoard start, IReadOnlyList<PowerGoal> goals, int maxDepth = 16, int maxStates = 60000)
        {
            if (start.ContainsAll(goals))
                return new SolveResult(Reachability.Reachable, Array.Empty<Direction>());

            var parents = new Dictionary<string, (string Parent, Direction Move)>();
            var visited = new HashSet<string> { start.Key };
            var queue = new Queue<(PowerBoard Board, int Depth)>();
            queue.Enqueue((start, 0));
            bool truncated = false;

            while (queue.Count > 0)
            {
                var (board, depth) = queue.Dequeue();
                if (depth >= maxDepth)
                {
                    truncated = true;
                    continue;
                }

                foreach (Direction direction in Directions)
                {
                    SlideResult slide = board.Slide(direction);
                    if (!slide.Changed)
                        continue;

                    string key = slide.Board.Key;
                    if (!visited.Add(key))
                        continue;

                    parents[key] = (board.Key, direction);
                    if (slide.Board.ContainsAll(goals))
                        return new SolveResult(Reachability.Reachable, BuildPath(parents, start.Key, key));

                    if (visited.Count >= maxStates)
                        return new SolveResult(Reachability.Unknown, Array.Empty<Direction>());

                    queue.Enqueue((slide.Board, depth + 1));
                }
            }

            return new SolveResult(truncated ? Reachability.Unknown : Reachability.Unreachable, Array.Empty<Direction>());
        }

        private static IReadOnlyList<Direction> BuildPath(Dictionary<string, (string Parent, Direction Move)> parents, string startKey, string endKey)
        {
            var path = new List<Direction>();
            string key = endKey;
            while (key != startKey)
            {
                var (parent, move) = parents[key];
                path.Add(move);
                key = parent;
            }

            path.Reverse();
            return path;
        }
    }
}
