// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "Short High CS Nerf (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): short maps with high CS
    /// (e.g. Spider-Man Theme, CS 6.1, 256 objects) lose up to 15% of the total PP.
    /// </summary>
    /// <remarks>
    /// Multiplier on total PP: 1 − 0.15 · smoothstep(CS, 5, 6) · (1 − smoothstep(objects, 300, 700)).
    /// Both parts are smooth, so there is no jump at CS 6 or at a given object count:
    /// CS 6+ → −15% at ≤ 300 objects, −7.5% at 500, ±0% from 700; CS 5.5 → half of that; CS ≤ 5 → ±0%.
    /// CS is the playable beatmap's CS (includes HR/EZ/DA), like "CS PP Buff (RX)". Object count does not depend on
    /// DT/HT, so NM and DT get the same multiplier.
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxShortHighCsNerf
    {
        public const double MAX_NERF = 0.15;
        public const double CS_START = 5.0;
        public const double CS_FULL = 6.0;
        public const double OBJECTS_FULL_NERF = 300;
        public const double OBJECTS_NO_NERF = 700;

        public static double Multiplier(RxBeatmap beatmap)
            => 1 - MAX_NERF
                 * smoothStep(beatmap.CircleSize, CS_START, CS_FULL)
                 * (1 - smoothStep(beatmap.HitObjects.Count, OBJECTS_FULL_NERF, OBJECTS_NO_NERF));

        private static double smoothStep(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
