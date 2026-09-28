// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "Stream-Only Guard (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): anti-abuse for streams into
    /// (almost) one point at absurd BPM. Such maps have speed PP many times higher than aim PP.
    /// </summary>
    /// <remarks>
    /// ratio = speedPP / aimPP (after all other aim/speed rules).
    /// 1) Speed PP stops counting: speed × (1 − smoothstep(ratio, 5, 10)) — from 10× and above speed PP is not part of
    ///    the total at all (added in MosuPp version 11).
    /// 2) The rest is cut too: total × (1 − 0.9 · smoothstep(ratio, 6, 10)), because tiny movements on high CS still
    ///    produce aim/reading PP that the CS PP Buff multiplies.
    /// Real stream maps stay far below 5× (Worms of Soul DT 3.1×, A Tale Of Salt And Light DT 2.1×).
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxStreamOnlyGuard
    {
        public const double SPEED_FADE_FROM_RATIO = 5;
        public const double NO_SPEED_AT_RATIO = 10;
        public const double NERF_FROM_RATIO = 6;
        public const double FULL_NERF_AT_RATIO = 10;
        public const double MAX_NERF = 0.9;

        public static double SpeedAimRatio(RxNativePerformanceResult result)
            => Math.Max(0, result.PpSpeed) / Math.Max(1, result.PpAim);

        /// <summary>
        /// Removes speed PP from the total for stream-only maps (rebuilding the total) and returns the multiplier
        /// for the rest of the total.
        /// </summary>
        // speedAimRatio: speed PP / aim PP of the SS reference (see ManagedRealistikRelaxCalculator).
        public static double Apply(RxNativePerformanceResult result, RxScoreState score, double nativeMultiplier, double speedAimRatio)
        {
            double ratio = speedAimRatio;
            double speedWeight = 1 - smoothStep(ratio, SPEED_FADE_FROM_RATIO, NO_SPEED_AT_RATIO);

            if (speedWeight < 1)
            {
                result.PpSpeed *= speedWeight;
                result.Pp = RealistikRelaxBalance.RebuildTotal(result, score, nativeMultiplier);
            }

            return 1 - MAX_NERF * smoothStep(ratio, NERF_FROM_RATIO, FULL_NERF_AT_RATIO);
        }

        private static double smoothStep(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
