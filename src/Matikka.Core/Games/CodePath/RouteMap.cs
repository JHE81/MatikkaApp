using System;
using System.Collections.Generic;
using System.Linq;

namespace Matikka.Core.Games.CodePath
{
    /// <summary>Suunta. Rivit kasvavat alaspäin, joten pohjoinen on y − 1.</summary>
    public enum Heading
    {
        East,
        South,
        West,
        North,
    }

    public readonly struct Cell : IEquatable<Cell>
    {
        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public Cell Step(Heading heading)
        {
            switch (heading)
            {
                case Heading.East: return new Cell(X + 1, Y);
                case Heading.South: return new Cell(X, Y + 1);
                case Heading.West: return new Cell(X - 1, Y);
                default: return new Cell(X, Y - 1);
            }
        }

        public bool Equals(Cell other) => X == other.X && Y == other.Y;

        public override bool Equals(object? obj) => obj is Cell other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Y;

        public static bool operator ==(Cell a, Cell b) => a.Equals(b);

        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);

        public override string ToString() => "(" + X + ", " + Y + ")";
    }

    public static class Headings
    {
        public static Heading Left(this Heading h) => (Heading)(((int)h + 3) % 4);

        public static Heading Right(this Heading h) => (Heading)(((int)h + 1) % 4);
    }

    /// <summary>Kenttä: puut (esteet), lähtö, pesä ja mustikat. Muuttumaton.</summary>
    public sealed class RouteMap
    {
        private readonly bool[] _trees;

        public RouteMap(int width, int height, IEnumerable<Cell> trees, Cell start, Heading startHeading, Cell goal, IEnumerable<Cell> berries)
        {
            Width = width;
            Height = height;
            _trees = new bool[width * height];
            foreach (Cell tree in trees)
                _trees[tree.Y * width + tree.X] = true;

            Start = start;
            StartHeading = startHeading;
            Goal = goal;
            Berries = berries.ToList();

            if (IsTree(start) || IsTree(goal) || Berries.Any(IsTree))
                throw new ArgumentException("Lähtö, pesä ja mustikat eivät voi olla puun kohdalla.");
        }

        public int Width { get; }

        public int Height { get; }

        public Cell Start { get; }

        public Heading StartHeading { get; }

        public Cell Goal { get; }

        /// <summary>Kerättävät mustikat; indeksi on mustikan tunniste.</summary>
        public IReadOnlyList<Cell> Berries { get; }

        public bool IsInside(Cell c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;

        public bool IsTree(Cell c) => IsInside(c) && _trees[c.Y * Width + c.X];

        /// <summary>Voiko soluun astua (kartan sisällä eikä puu).</summary>
        public bool IsFree(Cell c) => IsInside(c) && !_trees[c.Y * Width + c.X];

        public IEnumerable<Cell> Trees
        {
            get
            {
                for (int i = 0; i < _trees.Length; i++)
                {
                    if (_trees[i])
                        yield return new Cell(i % Width, i / Width);
                }
            }
        }
    }
}
