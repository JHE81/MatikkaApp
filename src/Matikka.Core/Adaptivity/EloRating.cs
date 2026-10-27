using System;

namespace Matikka.Core.Adaptivity
{
    /// <summary>
    /// Elo-tyyppinen adaptiivisuus (vrt. Math Garden): pelaajalla on taitoluku, tasolla vaikeus.
    /// Molemmat ovat samalla logit-asteikolla, jolloin onnistumisen odotettu todennäköisyys on
    /// logistinen funktio niiden erotuksesta. Jokainen pelattu tehtävä siirtää taitolukua
    /// sen verran kuin tulos poikkesi odotetusta.
    /// </summary>
    public static class EloRating
    {
        /// <summary>Tavoiteltu onnistumisprosentti: riittävän haastavaa, mutta onnistumisia tulee.</summary>
        public const double TargetSuccess = 0.75;

        /// <summary>ln(0.75 / 0.25): taitoluvun ja vaikeuden erotus, jolla odotus on 75 %.</summary>
        public static readonly double TargetLogit = Math.Log(TargetSuccess / (1 - TargetSuccess));

        public static double Expected(double rating, double difficulty) =>
            1.0 / (1.0 + Math.Exp(difficulty - rating));

        /// <summary>
        /// Oppimisnopeus: aluksi suuri, jotta taso löytyy nopeasti, sitten pienenee,
        /// ettei yksi huono päivä romahduta lukua.
        /// </summary>
        public static double KFactor(int previousAttempts) =>
            0.15 + 0.35 * Math.Exp(-previousAttempts / 8.0);

        /// <param name="score">Tulos väliltä 0–1 (katso <see cref="Progress.Scoring"/>).</param>
        public static double Update(double rating, double difficulty, double score, int previousAttempts)
        {
            if (score < 0 || score > 1)
                throw new ArgumentOutOfRangeException(nameof(score));

            return rating + KFactor(previousAttempts) * (score - Expected(rating, difficulty));
        }
    }
}
