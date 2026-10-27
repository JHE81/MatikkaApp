using System.Text;

namespace Matikka.Core.Games.EquationBalance
{
    /// <summary>Yhtälön toinen puoli muodossa ax + b.</summary>
    public sealed record LinearExpression(int X, int Constant)
    {
        public static readonly LinearExpression Zero = new LinearExpression(0, 0);

        public bool IsJustX => X == 1 && Constant == 0;

        public bool HasX => X != 0;

        /// <summary>Esim. "3x + 4", "-x - 2", "x", "7", "0".</summary>
        public override string ToString()
        {
            if (X == 0)
                return Constant.ToString(System.Globalization.CultureInfo.InvariantCulture);

            var sb = new StringBuilder();
            if (X == -1)
                sb.Append("-x");
            else if (X == 1)
                sb.Append('x');
            else
                sb.Append(X.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('x');

            if (Constant > 0)
                sb.Append(" + ").Append(Constant.ToString(System.Globalization.CultureInfo.InvariantCulture));
            else if (Constant < 0)
                sb.Append(" - ").Append((-(long)Constant).ToString(System.Globalization.CultureInfo.InvariantCulture));

            return sb.ToString();
        }
    }
}
