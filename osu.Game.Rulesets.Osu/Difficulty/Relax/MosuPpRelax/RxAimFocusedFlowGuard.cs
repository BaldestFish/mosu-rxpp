// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "Aim-Focused Flow Guard (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): Realistik's flow bonus
    /// (up to +17% aim strain for fast, evenly spaced, wide-angle notes) is meant for stream/flow maps. On aim-focused
    /// jump maps it is triggered by their spaced streams and adds ~+16% PP on top of the jumps; it is removed there.
    /// </summary>
    /// <remarks>
    /// Aim focus = 1 − smoothstep(speedPP / aimPP, 0.8, 1.1): maps with speed PP below 0.8× aim PP lose the whole flow
    /// bonus, maps with speed PP above 1.1× aim PP (stream maps) keep it. The aim strain is recomputed without the removed
    /// part of the bonus and aim PP is scaled by the Realistik aim value curve (5·max(strain/0.0675, 1) − 4)³.
    /// Examples (SS, RX): Sky of Twilight NM −13% / DT −14%, Novae Ruptis NM −13% / DT −14%, Attack / Lament −14%,
    /// Calm Down Juliet −10% / −4%; A Tale Of Salt And Light, Worms of Soul, heat and jump maps without flow ±0%.
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxAimFocusedFlowGuard
    {
        public const double AIM_FOCUSED_BELOW = 0.8;
        public const double NOT_AIM_FOCUSED_FROM = 1.1;
        public const double MAX_FLOW_BONUS = 0.17;

        // speedAimRatio: speed PP / aim PP of the SS reference (see ManagedRealistikRelaxCalculator), so the
        // decision does not change with the accuracy or misses of a score.
        public static double AimMultiplier(RxNativePerformanceResult result, double speedAimRatio)
        {
            double flowBonus = Math.Clamp(result.Difficulty.CustomFlowAimBonusRatio, 0, MAX_FLOW_BONUS);
            double aimStrain = result.Difficulty.AimStrain;
            if (flowBonus <= 0 || aimStrain <= 0 || result.PpAim <= 0)
                return 1;

            double aimFocus = 1 - smoothStep(speedAimRatio, AIM_FOCUSED_BELOW, NOT_AIM_FOCUSED_FROM);
            if (aimFocus <= 0)
                return 1;

            double guardedStrain = aimStrain * (1 + flowBonus * (1 - aimFocus)) / (1 + flowBonus);
            return aimValue(guardedStrain) / aimValue(aimStrain);
        }

        public static void Apply(RxNativePerformanceResult result, double speedAimRatio) => result.PpAim *= AimMultiplier(result, speedAimRatio);

        // Realistik aim value curve (without the constant /100000, which cancels in the ratio).
        private static double aimValue(double strain) => Math.Pow(5 * Math.Max(strain / 0.0675, 1) - 4, 3);

        private static double smoothStep(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
