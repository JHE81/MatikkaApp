using System.Globalization;

namespace Matikka.Core.Games.EquationBalance
{
    public enum Operation
    {
        Add,
        Subtract,
        Multiply,
        Divide,
    }

    /// <summary>
    /// Siirto, joka tehdään yhtälön molemmille puolille. Vaa'an idea: kun teet saman
    /// molemmille puolille, tasapaino säilyy.
    /// </summary>
    /// <param name="Operation">Laskutoimitus.</param>
    /// <param name="Value">Luku, jolla toimitus tehdään.</param>
    /// <param name="IsXTerm">Lisätäänkö/vähennetäänkö x-termejä (esim. "-2x"). Vain yhteen- ja vähennyslaskussa.</param>
    public sealed record BalanceMove(Operation Operation, int Value, bool IsXTerm = false)
    {
        public static BalanceMove Add(int value) => new BalanceMove(Operation.Add, value);
        public static BalanceMove Subtract(int value) => new BalanceMove(Operation.Subtract, value);
        public static BalanceMove AddX(int coefficient) => new BalanceMove(Operation.Add, coefficient, true);
        public static BalanceMove SubtractX(int coefficient) => new BalanceMove(Operation.Subtract, coefficient, true);
        public static BalanceMove MultiplyBy(int value) => new BalanceMove(Operation.Multiply, value);
        public static BalanceMove DivideBy(int value) => new BalanceMove(Operation.Divide, value);

        /// <summary>Lyhyt merkintä, esim. "- 4", "+ 2x", "· (-3)", ": 5".</summary>
        public override string ToString()
        {
            string number = Value < 0
                ? "(" + Value.ToString(CultureInfo.InvariantCulture) + ")"
                : Value.ToString(CultureInfo.InvariantCulture);

            string term = IsXTerm
                ? (Value == 1 ? "x" : number + "x")
                : number;

            switch (Operation)
            {
                case Operation.Add: return "+ " + term;
                case Operation.Subtract: return "- " + term;
                case Operation.Multiply: return "· " + term;
                default: return ": " + term;
            }
        }
    }
}
