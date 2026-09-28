// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "CS PP Buff (RX)", applied on top of the Realistik RX calculator when
    /// <c>ForkRelaxPpSystem.MosuPp</c> is selected: RX PP is multiplied by a factor that depends only on CS
    /// (after mods: HR/EZ/DA). The displayed star rating is not affected.
    /// </summary>
    /// <remarks>
    /// Target growth (MosuPp version 21, half of the version 6–18 table): CS ≤ 3.5 +0%, CS 4 +5%, CS 5 +10%, CS 6 +15%,
    /// CS 7 +25%, CS 8 +50%, CS 9 +100%, CS 10 +200%.
    /// Between the points the curve is a monotone cubic (PCHIP), so it is smooth and never overshoots.
    /// Beyond CS 10 (extended DA) the CS 10 value is kept.
    /// A jump-distance multiplier was tried first; with Realistik's own high-CS handling it could not follow
    /// these targets (per-map spread and non-monotonic between integer CS), hence the direct multiplier.
    /// When changing the table, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c> so cached song select PP is recalculated.
    /// </remarks>
    internal static class RxCsPpBuff
    {
        private static readonly double[] cs_points = { 3.5, 4, 5, 6, 7, 8, 9, 10 };
        private static readonly double[] multiplier_points = { 1.0, 1.05, 1.10, 1.15, 1.25, 1.5, 2.0, 3.0 };

        private static readonly double[] slopes = calculateSlopes();

        public static double Multiplier(double cs)
        {
            if (double.IsNaN(cs) || cs <= cs_points[0])
                return 1.0;

            int last = cs_points.Length - 1;

            if (cs >= cs_points[last])
                return multiplier_points[last];

            int i = 0;
            while (cs > cs_points[i + 1])
                i++;

            // Cubic Hermite segment between point i and i + 1.
            double h = cs_points[i + 1] - cs_points[i];
            double t = (cs - cs_points[i]) / h;
            double t2 = t * t;
            double t3 = t2 * t;

            return (2 * t3 - 3 * t2 + 1) * multiplier_points[i]
                   + (t3 - 2 * t2 + t) * h * slopes[i]
                   + (-2 * t3 + 3 * t2) * multiplier_points[i + 1]
                   + (t3 - t2) * h * slopes[i + 1];
        }

        /// <summary>Fritsch–Carlson slopes (PCHIP): monotone data gives a monotone curve.</summary>
        private static double[] calculateSlopes()
        {
            int n = cs_points.Length;
            double[] secants = new double[n - 1];

            for (int i = 0; i < n - 1; i++)
                secants[i] = (multiplier_points[i + 1] - multiplier_points[i]) / (cs_points[i + 1] - cs_points[i]);

            double[] result = new double[n];
            result[0] = secants[0];
            result[n - 1] = secants[n - 2];

            for (int i = 1; i < n - 1; i++)
            {
                if (secants[i - 1] * secants[i] <= 0)
                {
                    result[i] = 0;
                    continue;
                }

                double h0 = cs_points[i] - cs_points[i - 1];
                double h1 = cs_points[i + 1] - cs_points[i];
                double w0 = 2 * h1 + h0;
                double w1 = h1 + 2 * h0;
                result[i] = (w0 + w1) / (w0 / secants[i - 1] + w1 / secants[i]);
            }

            return result;
        }
    }
}
