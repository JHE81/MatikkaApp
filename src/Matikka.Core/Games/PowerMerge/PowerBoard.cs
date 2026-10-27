using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Matikka.Core.Games.PowerMerge
{
    public sealed class SlideResult
    {
        public SlideResult(PowerBoard board, IReadOnlyList<MergeInfo> merges, bool changed)
        {
            Board = board;
            Merges = merges;
            Changed = changed;
        }

        public PowerBoard Board { get; }

        public IReadOnlyList<MergeInfo> Merges { get; }

        /// <summary>Muuttuiko lauta. Siirto, joka ei muuta mitään, ei ole sallittu.</summary>
        public bool Changed { get; }
    }

    /// <summary>Muuttumaton pelilauta. Siirto palauttaa uuden laudan.</summary>
    public sealed class PowerBoard
    {
        private readonly PowerTile?[] _cells;
        private string? _key;

        public PowerBoard(int size, IReadOnlyList<PowerTile?> cells)
        {
            if (cells.Count != size * size)
                throw new ArgumentException("Solujen määrä ei vastaa laudan kokoa.", nameof(cells));

            Size = size;
            _cells = cells.ToArray();
        }

        private PowerBoard(int size, PowerTile?[] cells, bool _)
        {
            Size = size;
            _cells = cells;
        }

        public int Size { get; }

        public PowerTile? this[int row, int col] => _cells[row * Size + col];

        /// <summary>Laatat sijainteineen rivi kerrallaan.</summary>
        public IEnumerable<(PowerTile Tile, int Row, int Col)> Tiles
        {
            get
            {
                for (int i = 0; i < _cells.Length; i++)
                {
                    PowerTile? tile = _cells[i];
                    if (tile != null)
                        yield return (tile, i / Size, i % Size);
                }
            }
        }

        public int TileCount => _cells.Count(c => c != null);

        /// <summary>Laudan tila ilman tunnisteita: samat arvot samoissa paikoissa = sama avain.</summary>
        public string Key
        {
            get
            {
                if (_key != null)
                    return _key;

                var sb = new StringBuilder(_cells.Length * 4);
                foreach (PowerTile? tile in _cells)
                    sb.Append(tile == null ? "." : tile.Key).Append(',');
                _key = sb.ToString();
                return _key;
            }
        }

        public bool Contains(PowerGoal goal) => _cells.Any(c => c != null && goal.Matches(c));

        public bool ContainsAll(IReadOnlyList<PowerGoal> goals) => goals.All(Contains);

        /// <summary>
        /// Liu'uttaa kaikki laatat annettuun suuntaan kuten 2048:ssa. Rivillä seinää lähin
        /// yhdistyvä pari yhdistyy ensin, ja jokainen laatta yhdistyy enintään kerran siirrossa.
        /// </summary>
        public SlideResult Slide(Direction direction)
        {
            var cells = new PowerTile?[_cells.Length];
            var merges = new List<MergeInfo>();
            var line = new List<PowerTile>(Size);

            for (int i = 0; i < Size; i++)
            {
                line.Clear();
                for (int k = 0; k < Size; k++)
                {
                    PowerTile? tile = _cells[Index(direction, i, k)];
                    if (tile != null)
                        line.Add(tile);
                }

                int write = 0;
                int j = 0;
                while (j < line.Count)
                {
                    if (j + 1 < line.Count && PowerRules.TryCombine(line[j], line[j + 1], out PowerTile? result, out MergeRule rule))
                    {
                        cells[Index(direction, i, write++)] = result;
                        merges.Add(new MergeInfo(line[j], line[j + 1], result, rule));
                        j += 2;
                    }
                    else
                    {
                        cells[Index(direction, i, write++)] = line[j];
                        j++;
                    }
                }
            }

            bool changed = false;
            for (int i = 0; i < cells.Length && !changed; i++)
                changed = !Equals(cells[i], _cells[i]);

            return changed
                ? new SlideResult(new PowerBoard(Size, cells, true), merges, true)
                : new SlideResult(this, Array.Empty<MergeInfo>(), false);
        }

        /// <summary>Solun indeksi: rivi/sarake <paramref name="line"/>, k:s solu seinästä lukien.</summary>
        private int Index(Direction direction, int line, int k)
        {
            switch (direction)
            {
                case Direction.Left: return line * Size + k;
                case Direction.Right: return line * Size + (Size - 1 - k);
                case Direction.Up: return k * Size + line;
                default: return (Size - 1 - k) * Size + line;
            }
        }
    }
}
