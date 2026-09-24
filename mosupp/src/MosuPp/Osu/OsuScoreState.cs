using System;
using MosuPp.Util;

namespace MosuPp.Osu
{
    /// <summary>osu!standard hit results.</summary>
    public sealed class OsuHitResults
    {
        /// <summary>
        /// "Large tick" hits. Only relevant for osu!lazer:
        /// with slider accuracy: hit slider ticks and repeats; without slider accuracy (Classic): hit slider heads, ticks and repeats.
        /// </summary>
        public uint LargeTickHits;

        /// <summary>"Small tick" hits: slider end hits for lazer scores without slider accuracy.</summary>
        public uint SmallTickHits;

        /// <summary>Successfully hit slider ends (lazer scores with slider accuracy).</summary>
        public uint SliderEndHits;

        public uint N300;
        public uint N100;
        public uint N50;
        public uint Misses;

        public uint TotalHits => unchecked(N300 + N100 + N50 + Misses);

        public OsuHitResults Clone() => (OsuHitResults)MemberwiseClone();

        /// <summary>Accuracy between 0.0 and 1.0.</summary>
        public double Accuracy(OsuScoreOrigin origin)
        {
            unchecked
            {
                double numerator = 6 * N300 + 2 * N100 + N50;
                double denominator = 6 * (N300 + N100 + N50 + Misses);

                switch (origin.Kind)
                {
                    case OsuScoreOriginKind.WithSliderAcc:
                    {
                        uint sliderEndHits = Math.Min(SliderEndHits, origin.MaxSliderEnds);
                        uint largeTickHits = Math.Min(LargeTickHits, origin.MaxLargeTicks);

                        numerator += (double)(3 * sliderEndHits) + 0.6 * largeTickHits;
                        denominator += (double)(3 * origin.MaxSliderEnds) + 0.6 * origin.MaxLargeTicks;
                        break;
                    }
                    case OsuScoreOriginKind.WithoutSliderAcc:
                    {
                        uint largeTickHits = Math.Min(LargeTickHits, origin.MaxLargeTicks);
                        uint smallTickHits = Math.Min(SmallTickHits, origin.MaxSmallTicks);

                        numerator += 0.6 * largeTickHits + 0.2 * smallTickHits;
                        denominator += 0.6 * origin.MaxLargeTicks + 0.2 * origin.MaxSmallTicks;
                        break;
                    }
                }

                return RustMath.AlmostEq(denominator, 0.0) ? 0.0 : numerator / denominator;
            }
        }

        public override string ToString() => $"300: {N300}, 100: {N100}, 50: {N50}, miss: {Misses}, largeTicks: {LargeTickHits}, smallTicks: {SmallTickHits}, sliderEnds: {SliderEndHits}";
    }

    /// <summary>A score's state: hit results plus combo (and optionally the legacy total score).</summary>
    public sealed class OsuScoreState
    {
        /// <summary>Maximum combo that the score has had so far (not the map's max combo).</summary>
        public uint MaxCombo;

        public OsuHitResults HitResults = new OsuHitResults();

        /// <summary>Legacy total score. Only relevant for osu!stable scores.</summary>
        public uint? LegacyTotalScore;
    }

    public enum OsuScoreOriginKind
    {
        /// <summary>Scores set on osu!stable.</summary>
        Stable,

        /// <summary>Scores set on osu!lazer with slider accuracy.</summary>
        WithSliderAcc,

        /// <summary>Scores set on osu!lazer without slider accuracy (Classic mod).</summary>
        WithoutSliderAcc,
    }

    public readonly struct OsuScoreOrigin
    {
        public readonly OsuScoreOriginKind Kind;
        public readonly uint MaxLargeTicks;
        public readonly uint MaxSliderEnds;
        public readonly uint MaxSmallTicks;

        private OsuScoreOrigin(OsuScoreOriginKind kind, uint maxLargeTicks, uint maxSliderEnds, uint maxSmallTicks)
        {
            Kind = kind;
            MaxLargeTicks = maxLargeTicks;
            MaxSliderEnds = maxSliderEnds;
            MaxSmallTicks = maxSmallTicks;
        }

        public static OsuScoreOrigin Stable => default;

        public static OsuScoreOrigin WithSliderAcc(uint maxLargeTicks, uint maxSliderEnds)
            => new OsuScoreOrigin(OsuScoreOriginKind.WithSliderAcc, maxLargeTicks, maxSliderEnds, 0);

        public static OsuScoreOrigin WithoutSliderAcc(uint maxLargeTicks, uint maxSmallTicks)
            => new OsuScoreOrigin(OsuScoreOriginKind.WithoutSliderAcc, maxLargeTicks, 0, maxSmallTicks);

        /// <summary>(tick_score, tick_max) for <c>acc = (300*n300 + 100*n100 + 50*n50 + tick_score) / (300*total + tick_max)</c>.</summary>
        internal (uint TickScore, uint TickMax) TickScores(uint largeTickHits, uint smallTickHits, uint sliderEndHits)
        {
            unchecked
            {
                switch (Kind)
                {
                    case OsuScoreOriginKind.WithSliderAcc:
                        return (150 * sliderEndHits + 30 * largeTickHits, 150 * MaxSliderEnds + 30 * MaxLargeTicks);
                    case OsuScoreOriginKind.WithoutSliderAcc:
                        return (30 * largeTickHits + 10 * smallTickHits, 30 * MaxLargeTicks + 10 * MaxSmallTicks);
                    default:
                        return (0, 0);
                }
            }
        }
    }
}
