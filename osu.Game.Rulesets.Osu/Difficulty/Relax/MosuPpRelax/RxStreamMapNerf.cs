// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "Stream Map Nerf (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): every stream map loses up to 50% of
    /// the total PP, whatever the shape of its streams. Replaces "Simple Stream Nerf (RX)" (v10–v24), which only hit
    /// straight / one-curve streams.
    /// </summary>
    /// <remarks>
    /// Stream map = speed PP / aim PP of the SS reference ≥ 1.0–1.3 (smooth) AND stream share ≥ 30–60% (smooth).
    /// Stream notes: runs of ≥ 6 notes with equal spacing in time (±10%) and ≤ 125 ms between notes (raw map time, so NM and
    /// DT use the same share), counting only movements up to 2.5 circle radii (equal-timed jumps are not streams).
    /// Multiplier on the total: 1 − 0.50 · smoothstep(speed/aim, 1.0, 1.3) · smoothstep(stream share, 30%, 60%).
    /// Jump maps with streams (drivers license [full]: 73% streams but speed/aim 0.96 / 0.74, Novae Ruptis 0.80 / 0.65) are
    /// not touched. Examples (SS, RX): Expurget, Worms of Soul −50% NM and DT; A Tale Of Salt And Light −28% NM (speed/aim 1.16)
    /// and −50% DT; heat −1%.
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxStreamMapNerf
    {
        public const double MAX_NERF = 0.50;
        public const double STREAM_MAP_RATIO_START = 1.0;
        public const double STREAM_MAP_RATIO_FULL = 1.3;
        public const double SHARE_START = 0.30;
        public const double SHARE_FULL = 0.60;
        public const double STREAM_MAX_DELTA_MS = 125;
        public const double STREAM_DELTA_TOLERANCE = 0.10;
        public const int STREAM_MIN_NOTES = 6;
        public const double MAX_SPACING_IN_RADII = 2.5;

        // speedAimRatio: speed PP / aim PP of the SS reference (see ManagedRealistikRelaxCalculator).
        public static double Multiplier(RxBeatmap beatmap, double speedAimRatio)
            => 1 - MAX_NERF
                 * smoothStep(speedAimRatio, STREAM_MAP_RATIO_START, STREAM_MAP_RATIO_FULL)
                 * smoothStep(StreamShare(beatmap), SHARE_START, SHARE_FULL);

        /// <summary>Share of notes that belong to streams (0–1).</summary>
        public static double StreamShare(RxBeatmap beatmap)
        {
            List<RxHitObject> objects = beatmap.HitObjects;
            int count = objects.Count;
            if (count < STREAM_MIN_NOTES)
                return 0;

            bool[] inStream = new bool[count];
            double radius = 54.4 - 4.48 * beatmap.CircleSize;
            double maxSpacing = MAX_SPACING_IN_RADII * radius;
            int start = 1;

            while (start < count)
            {
                double delta = objects[start].StartTime - objects[start - 1].StartTime;
                if (delta <= 0 || delta > STREAM_MAX_DELTA_MS)
                {
                    start++;
                    continue;
                }

                int end = start;
                while (end + 1 < count
                       && Math.Abs((objects[end + 1].StartTime - objects[end].StartTime) / delta - 1) < STREAM_DELTA_TOLERANCE)
                    end++;

                // Notes start - 1 .. end form one run; only short movements count as streaming
                // (equal-timed full-screen jumps are not streams).
                if (end - start + 2 >= STREAM_MIN_NOTES)
                {
                    for (int i = start; i <= end; i++)
                    {
                        if (distance(objects[i], objects[i - 1]) > maxSpacing)
                            continue;

                        inStream[i] = true;
                        inStream[i - 1] = true;
                    }
                }

                start = end + 1;
            }

            int streamNotes = 0;
            foreach (bool value in inStream)
                if (value) streamNotes++;

            return (double)streamNotes / count;
        }

        private static double distance(RxHitObject a, RxHitObject b)
        {
            double dx = a.Position.X - b.Position.X;
            double dy = a.Position.Y - b.Position.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double smoothStep(double x, double start, double end)
        {
            double t = Math.Clamp((x - start) / (end - start), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
