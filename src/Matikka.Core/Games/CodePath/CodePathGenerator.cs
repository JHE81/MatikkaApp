using System;
using System.Collections.Generic;
using System.Linq;
using Matikka.Core.Randomness;

namespace Matikka.Core.Games.CodePath
{
    /// <param name="Level">Taso.</param>
    /// <param name="Seed">Siemen: taso + siemen toistaa saman kentän.</param>
    /// <param name="Map">Kenttä.</param>
    /// <param name="Par">Malliratkaisun lohkomäärä (kolmen tähden raja).</param>
    /// <param name="BlockLimit">Suurin sallittu lohkomäärä, tai null jos rajaa ei ole.</param>
    /// <param name="Palette">Tällä tasolla käytettävissä olevat lohkot.</param>
    /// <param name="ModelMain">Malliratkaisun pääohjelma.</param>
    /// <param name="ModelProcedure">Malliratkaisun aliohjelma.</param>
    /// <param name="HintRoute">Malliratkaisun reitti soluina (vihjettä varten).</param>
    public sealed record CodePathPuzzle(
        int Level,
        long Seed,
        RouteMap Map,
        int Par,
        int? BlockLimit,
        IReadOnlyList<CommandKind> Palette,
        IReadOnlyList<Block> ModelMain,
        IReadOnlyList<Block> ModelProcedure,
        IReadOnlyList<Cell> HintRoute);

    /// <summary>
    /// Luo kentän takaperin: ensin malliohjelma, sitten sen kulkema reitti, ja lopuksi puut reitin
    /// ympärille. Peräkkäisyystasoilla par lasketaan leveyshaulla tarkasti. Toistotasoilla par on
    /// malliohjelman koko, ja lohkoraja on niin tiukka, ettei kenttää pysty ratkaisemaan ilman
    /// toistoa tai aliohjelmaa.
    /// </summary>
    public static class CodePathGenerator
    {
        private const int MaxAttempts = 2000;

        private static readonly CommandKind[] Basic = { CommandKind.Forward, CommandKind.TurnLeft, CommandKind.TurnRight };
        private static readonly CommandKind[] WithRepeat = { CommandKind.Forward, CommandKind.TurnLeft, CommandKind.TurnRight, CommandKind.Repeat };
        private static readonly CommandKind[] All = { CommandKind.Forward, CommandKind.TurnLeft, CommandKind.TurnRight, CommandKind.Repeat, CommandKind.Call };

        public static int MaxLevel => CodePathGame.Info.Levels.Count;

        public static int GridSize(int level) => level <= 2 ? 6 : 7;

        public static CodePathPuzzle Generate(int level, long seed)
        {
            if (level < 1 || level > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level, "Tuntematon taso.");

            var random = new SeededRandom(seed);
            int size = GridSize(level);

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var ids = new RobotProgram();
                var (main, procedure) = BuildModel(level, random, ids);
                List<CommandKind> flat = Flatten(main, procedure);

                var heading = (Heading)random.Next(0, 3);
                List<Cell>? route = Walk(flat, heading);
                if (route == null)
                    continue; // reitti risteää itsensä

                int minX = route.Min(c => c.X), maxX = route.Max(c => c.X);
                int minY = route.Min(c => c.Y), maxY = route.Max(c => c.Y);
                if (maxX - minX >= size || maxY - minY >= size)
                    continue;

                int dx = random.Next(0, size - 1 - (maxX - minX)) - minX;
                int dy = random.Next(0, size - 1 - (maxY - minY)) - minY;
                route = route.Select(c => new Cell(c.X + dx, c.Y + dy)).ToList();

                Cell start = route[0];
                Cell goal = route[route.Count - 1];
                var onRoute = new HashSet<Cell>(route);

                var berries = new List<Cell>();
                if (level == 2)
                {
                    var middle = route.Skip(1).Take(route.Count - 2).Distinct().ToList();
                    if (middle.Count < 3)
                        continue;
                    while (berries.Count < 2)
                    {
                        Cell c = middle[random.Next(0, middle.Count - 1)];
                        if (!berries.Contains(c))
                            berries.Add(c);
                    }
                }

                double density = level <= 2 ? 0.4 : 0.8;
                var trees = new List<Cell>();
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        var c = new Cell(x, y);
                        if (!onRoute.Contains(c) && random.NextDouble() < density)
                            trees.Add(c);
                    }
                }

                var map = new RouteMap(size, size, trees, start, heading, goal, berries);

                // Pelkillä peruslohkoilla tarvittava vähimmäismäärä (tarkka leveyshaku).
                List<CommandKind>? shortest = ShortestSequence(map);
                if (shortest == null)
                    continue;

                if (level <= 2)
                {
                    // Sopivan mittainen reitti: puut voivat pakottaa kiertoteille.
                    if (shortest.Count < (level == 1 ? 4 : 6) || shortest.Count > (level == 1 ? 8 : 12))
                        continue;

                    var model = shortest.Select(k => ids.NewBlock(k)).ToList();
                    List<Cell> hint = Walk(shortest, heading, start) ?? route;
                    return new CodePathPuzzle(level, seed, map, model.Count, null, Basic, model, Array.Empty<Block>(), hint);
                }

                int par = RobotProgram.Count(main) + RobotProgram.Count(procedure);
                int limit = par + 1;

                // Lohkoraja ei saa riittää pelkkiin peruslohkoihin: toisto (tai aliohjelma) on pakko keksiä.
                if (shortest.Count <= limit + 1)
                    continue;

                // Varmistus: malliratkaisu toimii valmiilla kentällä.
                RunResult check = Interpreter.Run(map, main, procedure);
                if (check.Outcome != RunOutcome.Success)
                    continue;

                return new CodePathPuzzle(level, seed, map, par, limit, level <= 4 ? WithRepeat : All, main, procedure, route);
            }

            throw new InvalidOperationException($"Tason {level} kenttää ei saatu luotua siemenellä {seed}.");
        }

        private static (List<Block> Main, List<Block> Procedure) BuildModel(int level, SeededRandom r, RobotProgram ids)
        {
            var main = new List<Block>();
            var procedure = new List<Block>();

            Block F() => ids.NewBlock(CommandKind.Forward);
            Block Turn() => ids.NewBlock(r.NextBool() ? CommandKind.TurnLeft : CommandKind.TurnRight);
            Block Repeat(int n, params Block[] body)
            {
                Block b = ids.NewBlock(CommandKind.Repeat, n);
                b.Body.AddRange(body);
                return b;
            }

            switch (level)
            {
                case 1:
                case 2:
                {
                    int segments = level == 1 ? r.Next(2, 3) : r.Next(3, 4);
                    for (int s = 0; s < segments; s++)
                    {
                        if (s > 0)
                            main.Add(Turn());
                        int length = r.Next(1, 3);
                        for (int i = 0; i < length; i++)
                            main.Add(F());
                    }

                    break;
                }

                case 3:
                {
                    int segments = r.Next(2, 3);
                    for (int s = 0; s < segments; s++)
                    {
                        if (s > 0)
                            main.Add(Turn());
                        main.Add(Repeat(r.Next(3, 6), F()));
                    }

                    break;
                }

                case 4:
                {
                    // Portaat: toista [eteen a, käänny, eteen b, käänny takaisin].
                    if (r.NextBool())
                        main.Add(F());

                    bool leftFirst = r.NextBool();
                    var pattern = new List<Block>();
                    for (int i = r.Next(1, 2); i > 0; i--)
                        pattern.Add(F());
                    pattern.Add(ids.NewBlock(leftFirst ? CommandKind.TurnLeft : CommandKind.TurnRight));
                    for (int i = r.Next(1, 2); i > 0; i--)
                        pattern.Add(F());
                    pattern.Add(ids.NewBlock(leftFirst ? CommandKind.TurnRight : CommandKind.TurnLeft));
                    main.Add(Repeat(r.Next(3, 4), pattern.ToArray()));
                    break;
                }

                case 5:
                {
                    // Sama pätkä kolmessa kohdassa, välissä erilaiset liitokset.
                    procedure.AddRange(Pattern(r, ids, r.Next(4, 5)));
                    main.Add(ids.NewBlock(CommandKind.Call));
                    main.AddRange(Glue(r, ids));
                    main.Add(ids.NewBlock(CommandKind.Call));
                    main.AddRange(Glue(r, ids));
                    main.Add(ids.NewBlock(CommandKind.Call));
                    break;
                }

                default:
                {
                    // Pätkä toistuu kahdessa jaksossa: toisto + aliohjelma yhdessä.
                    procedure.AddRange(Pattern(r, ids, r.Next(4, 5)));
                    main.Add(Repeat(r.Next(2, 3), ids.NewBlock(CommandKind.Call)));
                    main.AddRange(Glue(r, ids));
                    main.Add(Repeat(2, ids.NewBlock(CommandKind.Call)));
                    break;
                }
            }

            return (main, procedure);
        }

        /// <summary>Lyhyt kuvio, jossa on vähintään kaksi askelta ja yksi käännös.</summary>
        private static List<Block> Pattern(SeededRandom r, RobotProgram ids, int length)
        {
            var kinds = new List<CommandKind> { CommandKind.Forward };
            while (kinds.Count < length)
            {
                // Ei kahta käännöstä peräkkäin: se olisi turhaa tai käännös taaksepäin.
                bool lastWasTurn = kinds[kinds.Count - 1] != CommandKind.Forward;
                if (!lastWasTurn && r.Next(0, 2) == 0)
                    kinds.Add(r.NextBool() ? CommandKind.TurnLeft : CommandKind.TurnRight);
                else
                    kinds.Add(CommandKind.Forward);
            }

            if (kinds.Count(k => k == CommandKind.Forward) < 2 || kinds.All(k => k == CommandKind.Forward))
                kinds[kinds.Count - 2] = CommandKind.TurnLeft;

            return kinds.Select(k => ids.NewBlock(k)).ToList();
        }

        private static List<Block> Glue(SeededRandom r, RobotProgram ids)
        {
            CommandKind[][] options =
            {
                new[] { CommandKind.TurnLeft },
                new[] { CommandKind.TurnRight },
                new[] { CommandKind.Forward, CommandKind.TurnLeft },
                new[] { CommandKind.Forward, CommandKind.TurnRight },
                new[] { CommandKind.TurnLeft, CommandKind.Forward },
                new[] { CommandKind.TurnRight, CommandKind.Forward },
            };

            return options[r.Next(0, options.Length - 1)].Select(k => ids.NewBlock(k)).ToList();
        }

        /// <summary>Ohjelma yksinkertaisiksi komennoiksi (toistot ja kutsut auki).</summary>
        public static List<CommandKind> Flatten(IReadOnlyList<Block> main, IReadOnlyList<Block> procedure)
        {
            var result = new List<CommandKind>();
            Expand(main, procedure, result, 0);
            return result;
        }

        private static void Expand(IReadOnlyList<Block> blocks, IReadOnlyList<Block> procedure, List<CommandKind> result, int depth)
        {
            if (depth > 10)
                return;

            foreach (Block b in blocks)
            {
                switch (b.Kind)
                {
                    case CommandKind.Repeat:
                        for (int i = 0; i < b.Count; i++)
                            Expand(b.Body, procedure, result, depth + 1);
                        break;
                    case CommandKind.Call:
                        Expand(procedure, procedure, result, depth + 1);
                        break;
                    default:
                        result.Add(b.Kind);
                        break;
                }
            }
        }

        /// <summary>Komentojen kulkema reitti. Null, jos reitti kulkee saman solun kautta kahdesti.</summary>
        private static List<Cell>? Walk(IReadOnlyList<CommandKind> commands, Heading heading, Cell start = default)
        {
            var route = new List<Cell> { start };
            var seen = new HashSet<Cell> { start };
            Cell position = start;

            foreach (CommandKind command in commands)
            {
                if (command == CommandKind.TurnLeft)
                    heading = heading.Left();
                else if (command == CommandKind.TurnRight)
                    heading = heading.Right();
                else
                {
                    position = position.Step(heading);
                    if (!seen.Add(position))
                        return null;
                    route.Add(position);
                }
            }

            return route;
        }

        /// <summary>
        /// Lyhin komentojono pelkillä peruslohkoilla (eteen, vasen, oikea), joka vie pesään
        /// kaikki mustikat kerättyinä. Leveyshaku tiloihin (solu, suunta, kerätyt mustikat).
        /// </summary>
        public static List<CommandKind>? ShortestSequence(RouteMap map)
        {
            int full = (1 << map.Berries.Count) - 1;
            var startState = (map.Start, map.StartHeading, 0);
            var parents = new Dictionary<(Cell, Heading, int), ((Cell, Heading, int) Parent, CommandKind Command)>();
            var queue = new Queue<(Cell Cell, Heading Heading, int Mask)>();
            var seen = new HashSet<(Cell, Heading, int)> { startState };
            queue.Enqueue(startState);

            while (queue.Count > 0)
            {
                var state = queue.Dequeue();
                foreach (CommandKind command in Basic)
                {
                    (Cell, Heading, int) next;
                    if (command == CommandKind.TurnLeft)
                        next = (state.Cell, state.Heading.Left(), state.Mask);
                    else if (command == CommandKind.TurnRight)
                        next = (state.Cell, state.Heading.Right(), state.Mask);
                    else
                    {
                        Cell cell = state.Cell.Step(state.Heading);
                        if (!map.IsFree(cell))
                            continue;
                        int mask = state.Mask;
                        for (int i = 0; i < map.Berries.Count; i++)
                        {
                            if (map.Berries[i] == cell)
                                mask |= 1 << i;
                        }

                        next = (cell, state.Heading, mask);
                    }

                    if (!seen.Add(next))
                        continue;

                    parents[next] = ((state.Cell, state.Heading, state.Mask), command);
                    if (command == CommandKind.Forward && next.Item1 == map.Goal && next.Item3 == full)
                        return BuildPath(parents, startState, next);

                    queue.Enqueue((next.Item1, next.Item2, next.Item3));
                }
            }

            return null;
        }

        private static List<CommandKind> BuildPath(
            Dictionary<(Cell, Heading, int), ((Cell, Heading, int) Parent, CommandKind Command)> parents,
            (Cell, Heading, int) start,
            (Cell, Heading, int) end)
        {
            var path = new List<CommandKind>();
            var state = end;
            while (!state.Equals(start))
            {
                var (parent, command) = parents[state];
                path.Add(command);
                state = parent;
            }

            path.Reverse();
            return path;
        }
    }
}
