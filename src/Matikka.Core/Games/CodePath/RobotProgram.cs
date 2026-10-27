using System;
using System.Collections.Generic;
using System.Linq;

namespace Matikka.Core.Games.CodePath
{
    public enum CommandKind
    {
        Forward,
        TurnLeft,
        TurnRight,

        /// <summary>Toista n kertaa: lohkon sisältö ajetaan n kertaa.</summary>
        Repeat,

        /// <summary>Kutsu aliohjelmaa.</summary>
        Call,
    }

    /// <summary>
    /// Ohjelman lohko. Muokattava, koska pelaaja rakentaa ohjelmaa lohko kerrallaan.
    /// Tunniste yksilöi lohkon, jotta käyttöliittymä voi korostaa ajettavan lohkon.
    /// </summary>
    public sealed class Block
    {
        public const int MinRepeat = 2;
        public const int MaxRepeat = 9;

        private int _count = MinRepeat;

        public Block(int id, CommandKind kind, int count = MinRepeat, IEnumerable<Block>? body = null)
        {
            Id = id;
            Kind = kind;
            Count = count;
            Body = body?.ToList() ?? new List<Block>();
        }

        public int Id { get; }

        public CommandKind Kind { get; }

        /// <summary>Toistokerrat (vain Repeat-lohkolla).</summary>
        public int Count
        {
            get => _count;
            set => _count = Math.Max(MinRepeat, Math.Min(MaxRepeat, value));
        }

        /// <summary>Toistettavat lohkot (vain Repeat-lohkolla).</summary>
        public List<Block> Body { get; }
    }

    /// <summary>Pääohjelma ja yksi aliohjelma.</summary>
    public sealed class RobotProgram
    {
        private int _nextId = 1;

        public List<Block> Main { get; } = new List<Block>();

        public List<Block> Procedure { get; } = new List<Block>();

        /// <summary>Lohkojen määrä yhteensä (sisäkkäiset mukaan lukien). Tähdet ja lohkoraja lasketaan tästä.</summary>
        public int Size => Count(Main) + Count(Procedure);

        public Block NewBlock(CommandKind kind, int count = Block.MinRepeat) => new Block(_nextId++, kind, count);

        public void Clear()
        {
            Main.Clear();
            Procedure.Clear();
        }

        /// <summary>Etsii lohkon ja sen säiliön (lista, jossa lohko on).</summary>
        public bool TryFind(int blockId, out Block? block, out List<Block>? container)
        {
            return TryFind(Main, blockId, out block, out container) || TryFind(Procedure, blockId, out block, out container);
        }

        public bool Remove(int blockId)
        {
            if (!TryFind(blockId, out Block? block, out List<Block>? container))
                return false;

            container!.Remove(block!);
            return true;
        }

        public static int Count(IEnumerable<Block> blocks) =>
            blocks.Sum(b => 1 + (b.Kind == CommandKind.Repeat ? Count(b.Body) : 0));

        private static bool TryFind(List<Block> list, int id, out Block? block, out List<Block>? container)
        {
            foreach (Block b in list)
            {
                if (b.Id == id)
                {
                    block = b;
                    container = list;
                    return true;
                }

                if (b.Kind == CommandKind.Repeat && TryFind(b.Body, id, out block, out container))
                    return true;
            }

            block = null;
            container = null;
            return false;
        }
    }
}
