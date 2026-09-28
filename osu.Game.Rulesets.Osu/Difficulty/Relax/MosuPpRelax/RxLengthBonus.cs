// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rules about how the difficulty is spread over the map (only for <c>ForkRelaxPpSystem.MosuPp</c>),
    /// applied to aim and speed PP separately.
    /// </summary>
    /// <remarks>
    /// "Length Bonus (RX)": many hard moments are worth more than one short spike. Realistik's own length bonus
    /// stops growing at 2000 objects and only counts objects; this uses the difficult strain count instead:
    /// × (1 + 0.20 · smoothstep(DifficultStrainCount, 100, 400)) (+40% before v6, +60% in v6–v15).
    /// <para/>
    /// "Spike Nerf (RX)": maps whose PP comes from one short, extremely hard part (e.g. a short kiai of full-screen
    /// jumps, the rest easy) lose PP: × (1 − 0.20 · smoothstep(PeakRatio, 1.8, 4.0)) (−45% before v16, −30% in v16–v21), where PeakRatio is the average of
    /// the 10 hardest 400 ms sections divided by the 75th percentile of played sections. Evenly hard maps, short or long,
    /// have a ratio below 1.8 and are not touched.
    /// <para/>
    /// Examples (SS, RX, both rules, +40% version): A Tale Of Salt And Light +14%, Attack (Nightcore) −45%, Lament moment −32%,
    /// Worms of Soul −21%, Harumachi Clover / bbydoll ±0%. Going from +40% to +60%: Tale NM +6% / DT +8%,
    /// Worms +1% / +2%, WE ♥ YURI +13%, most other maps 0–1%.
    /// When changing anything here, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxLengthBonus
    {
        public const double MAX_BONUS = 0.20;
        public const double COUNT_START = 100;
        public const double COUNT_FULL = 400;

        public const double MAX_SPIKE_NERF = 0.20;
        public const double PEAK_RATIO_START = 1.8;
        public const double PEAK_RATIO_FULL = 4.0;

        public static double Multiplier(double difficultStrainCount, double peakRatio)
            => (1 + MAX_BONUS * smoothStep(difficultStrainCount, COUNT_START, COUNT_FULL))
               * (1 - MAX_SPIKE_NERF * smoothStep(peakRatio, PEAK_RATIO_START, PEAK_RATIO_FULL));

        public static void Apply(RxNativePerformanceResult result)
        {
            result.PpAim *= Multiplier(result.Difficulty.AimDifficultStrainCount, result.Difficulty.AimPeakRatio);
            result.PpSpeed *= Multiplier(result.Difficulty.SpeedDifficultStrainCount, result.Difficulty.SpeedPeakRatio);
        }

        private static double smoothStep(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
