// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "Extreme Jump Nerf (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): maps with a very large number of
    /// extreme-velocity jumps (e.g. glass beach [The summer sun sets]) lose part of their aim PP.
    /// </summary>
    /// <remarks>
    /// An extreme jump is a move between two consecutive objects (spinners skipped) with cursor velocity
    /// ≥ 4.5 osu!px/ms, measured on the map itself (clock rate 1), so NM, DT and HR get the same multiplier.
    /// Multiplier on aim PP: 1 − 0.31 · smoothstep(extreme jumps, 80, 180).
    /// Normal jump maps have far fewer such jumps (Attack 60, Lament 51, drivers license 20, jump pack 3 7) and are not touched.
    /// Example (SS, RX): glass beach (/b/5870318, 180 extreme jumps) −30% total.
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxExtremeJumpNerf
    {
        public const double EXTREME_VELOCITY = 4.5;
        public const double NERF_START_COUNT = 80;
        public const double NERF_FULL_COUNT = 180;
        public const double MAX_NERF = 0.31;

        public static double Multiplier(RxBeatmap beatmap)
            => 1 - MAX_NERF * smoothStep(ExtremeJumpCount(beatmap), NERF_START_COUNT, NERF_FULL_COUNT);

        public static void Apply(RxNativePerformanceResult result, RxBeatmap beatmap)
            => result.PpAim *= Multiplier(beatmap);

        public static int ExtremeJumpCount(RxBeatmap beatmap)
        {
            List<RxHitObject> objects = beatmap.HitObjects;
            int count = 0;

            for (int i = 1; i < objects.Count; i++)
            {
                RxHitObject previous = objects[i - 1];
                RxHitObject current = objects[i];

                if (previous.Kind == RxHitObjectKind.Spinner || current.Kind == RxHitObjectKind.Spinner)
                    continue;

                double deltaTime = current.StartTime - previous.StartTime;
                if (deltaTime <= 0)
                    continue;

                double dx = current.Position.X - previous.Position.X;
                double dy = current.Position.Y - previous.Position.Y;

                // Velocity in osu!px per ms (start position to start position).
                if (Math.Sqrt(dx * dx + dy * dy) / Math.Max(deltaTime, 1) >= EXTREME_VELOCITY)
                    count++;
            }

            return count;
        }

        private static double smoothStep(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
