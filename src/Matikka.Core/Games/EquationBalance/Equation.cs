using System;

namespace Matikka.Core.Games.EquationBalance
{
    public enum MoveError
    {
        None,

        /// <summary>Lisäys tai vähennys nollalla ei muuta mitään.</summary>
        NoEffect,

        /// <summary>Nollalla kertominen hävittää yhtälön (0 = 0).</summary>
        MultiplyByZero,

        /// <summary>Nollalla ei voi jakaa.</summary>
        DivideByZero,

        /// <summary>Kaikki termit eivät jakaudu tasan. Peli pysyy kokonaisluvuissa.</summary>
        NotDivisible,

        /// <summary>x-termillä voi vain lisätä tai vähentää.</summary>
        XTermNotAllowed,

        /// <summary>Luvut kasvaisivat yli pelin rajan.</summary>
        NumbersTooLarge,
    }

    public readonly struct MoveResult
    {
        private MoveResult(Equation? equation, MoveError error)
        {
            Equation = equation;
            Error = error;
        }

        public Equation? Equation { get; }

        public MoveError Error { get; }

        public bool Succeeded => Error == MoveError.None;

        public static MoveResult Ok(Equation equation) => new MoveResult(equation, MoveError.None);

        public static MoveResult Fail(MoveError error) => new MoveResult(null, error);
    }

    /// <summary>Lineaarinen yhtälö ax + b = cx + d. Muuttumaton: siirto palauttaa uuden yhtälön.</summary>
    public sealed record Equation(LinearExpression Left, LinearExpression Right)
    {
        /// <summary>Suurin sallittu itseisarvo kertoimille ja vakioille.</summary>
        public const int MaxAbsValue = 999;

        public static Equation Of(int leftX, int leftConstant, int rightX, int rightConstant) =>
            new Equation(new LinearExpression(leftX, leftConstant), new LinearExpression(rightX, rightConstant));

        /// <summary>Ratkaistu, kun toisella puolella on pelkkä x ja toisella pelkkä luku.</summary>
        public bool IsSolved =>
            (Left.IsJustX && !Right.HasX) || (Right.IsJustX && !Left.HasX);

        /// <summary>Ratkaisun arvo, jos yhtälö on ratkaistu.</summary>
        public int? SolvedValue =>
            Left.IsJustX && !Right.HasX ? Right.Constant
            : Right.IsJustX && !Left.HasX ? Left.Constant
            : (int?)null;

        public Equation Swap() => new Equation(Right, Left);

        /// <summary>Toteutuuko yhtälö, kun x saa annetun arvon.</summary>
        public bool IsSatisfiedBy(int x) =>
            (long)Left.X * x + Left.Constant == (long)Right.X * x + Right.Constant;

        public MoveResult Apply(BalanceMove move)
        {
            if (move.IsXTerm && (move.Operation == Operation.Multiply || move.Operation == Operation.Divide))
                return MoveResult.Fail(MoveError.XTermNotAllowed);

            switch (move.Operation)
            {
                case Operation.Add:
                case Operation.Subtract:
                {
                    if (move.Value == 0)
                        return MoveResult.Fail(MoveError.NoEffect);

                    long delta = move.Operation == Operation.Add ? move.Value : -(long)move.Value;
                    return move.IsXTerm
                        ? Build(Left.X + delta, Left.Constant, Right.X + delta, Right.Constant)
                        : Build(Left.X, Left.Constant + delta, Right.X, Right.Constant + delta);
                }

                case Operation.Multiply:
                {
                    if (move.Value == 0)
                        return MoveResult.Fail(MoveError.MultiplyByZero);
                    if (move.Value == 1)
                        return MoveResult.Fail(MoveError.NoEffect);

                    long m = move.Value;
                    return Build(Left.X * m, Left.Constant * m, Right.X * m, Right.Constant * m);
                }

                case Operation.Divide:
                {
                    if (move.Value == 0)
                        return MoveResult.Fail(MoveError.DivideByZero);
                    if (move.Value == 1)
                        return MoveResult.Fail(MoveError.NoEffect);

                    int d = move.Value;
                    if (Left.X % d != 0 || Left.Constant % d != 0 || Right.X % d != 0 || Right.Constant % d != 0)
                        return MoveResult.Fail(MoveError.NotDivisible);

                    return Build((long)Left.X / d, (long)Left.Constant / d, (long)Right.X / d, (long)Right.Constant / d);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(move));
            }
        }

        public override string ToString() => Left + " = " + Right;

        private static MoveResult Build(long leftX, long leftConstant, long rightX, long rightConstant)
        {
            if (Math.Abs(leftX) > MaxAbsValue || Math.Abs(leftConstant) > MaxAbsValue ||
                Math.Abs(rightX) > MaxAbsValue || Math.Abs(rightConstant) > MaxAbsValue)
            {
                return MoveResult.Fail(MoveError.NumbersTooLarge);
            }

            return MoveResult.Ok(Of((int)leftX, (int)leftConstant, (int)rightX, (int)rightConstant));
        }
    }
}
