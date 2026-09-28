// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "Heavy Miss Penalty (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): scores with more than 15 misses
    /// lose PP much faster than with the base Realistik miss penalty alone.
    /// </summary>
    /// <remarks>
    /// Total × e^(−0.11 · max(0, misses − 15)), on top of the base Realistik miss penalty (which is unchanged).
    /// Only real misses count (slider breaks keep the base penalty only). Tuned on Sky of Twilight (/b/5208003), share of
    /// the SS PP kept: 15 misses 41% (unchanged), 20 → 20%, 25 → 10%, 30 → 5%, 40 → 1%.
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxHeavyMissPenalty
    {
        public const double FREE_MISSES = 15;
        public const double DECAY_PER_MISS = 0.11;

        public static double Multiplier(uint misses)
            => Math.Exp(-DECAY_PER_MISS * Math.Max(0, misses - FREE_MISSES));
    }
}
