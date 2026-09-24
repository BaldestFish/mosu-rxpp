using System;
using System.Collections.Generic;

namespace MosuPp.Model
{
    /// <summary>Why a map is considered too suspicious for calculation.</summary>
    public enum TooSuspiciousReason
    {
        /// <summary>Too many objects within a short time span.</summary>
        Density,

        /// <summary>The map is longer than a day.</summary>
        Length,

        /// <summary>Too many objects in total.</summary>
        ObjectCount,

        /// <summary>A slider with both huge repeats and a far-away position.</summary>
        RedFlag,

        /// <summary>Too many sliders with far-away positions.</summary>
        SliderPositions,

        /// <summary>Too many sliders with huge repeat counts.</summary>
        SliderRepeats,
    }

    public sealed class TooSuspiciousException : Exception
    {
        public TooSuspiciousReason Reason { get; }

        public TooSuspiciousException(TooSuspiciousReason reason)
            : base($"the map seems too suspicious for further calculation (reason={reason})")
        {
            Reason = reason;
        }
    }

    internal static class TooSuspicious
    {
        private const int Threshold1S = 200;
        private const int Threshold10S = 500;

        public static TooSuspiciousReason? Check(Beatmap map)
        {
            if (map.HitObjects.Count > (map.Mode == GameMode.Taiko ? 30_000 : 500_000))
                return TooSuspiciousReason.ObjectCount;

            if (TooLong(map.HitObjects))
                return TooSuspiciousReason.Length;

            // Only osu!standard is supported by this library.
            return CheckOsu(map);
        }

        private static bool TooLong(List<HitObject> hitObjects)
        {
            const uint dayMs = 60 * 60 * 24 * 1000;

            if (hitObjects.Count < 2)
                return false;

            return hitObjects[hitObjects.Count - 1].StartTime - hitObjects[0].StartTime > dayMs;
        }

        private static TooSuspiciousReason? CheckOsu(Beatmap map)
        {
            int repeatsBeyondThreshold = 0;
            int posBeyondThreshold = 0;

            List<HitObject> objs = map.HitObjects;

            for (int i = 0; i < objs.Count; i++)
            {
                if (TooDense(objs, i, Threshold1S, Threshold10S))
                    return TooSuspiciousReason.Density;

                HitObject h = objs[i];

                if (h.Slider != null)
                {
                    bool farAway = Math.Abs(h.Pos.X) > 10_000f || Math.Abs(h.Pos.Y) > 10_000f;

                    if (h.Slider.Repeats > 1000)
                    {
                        if (farAway)
                            return TooSuspiciousReason.RedFlag;

                        repeatsBeyondThreshold++;
                    }
                    else if (farAway)
                    {
                        posBeyondThreshold++;
                    }
                }
            }

            const int cutoff = 128;

            if (posBeyondThreshold > cutoff)
                return TooSuspiciousReason.SliderPositions;

            if (repeatsBeyondThreshold > cutoff)
                return TooSuspiciousReason.SliderRepeats;

            return null;
        }

        private static bool TooDense(List<HitObject> hitObjects, int i, int per1S, int per10S)
        {
            return (hitObjects.Count > i + per1S && hitObjects[i + per1S].StartTime - hitObjects[i].StartTime < 1000.0)
                   || (hitObjects.Count > i + per10S && hitObjects[i + per10S].StartTime - hitObjects[i].StartTime < 10_000.0);
        }
    }
}
