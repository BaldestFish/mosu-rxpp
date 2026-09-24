using System;
using MosuPp.Model;
using MosuPp.Util;

namespace MosuPp.Osu
{
    internal sealed class OsuRatingCalculator
    {
        private const double DifficultyMultiplier = 0.0675;

        private readonly GameMods mods;
        private readonly uint totalHits;
        private readonly double approachRate;
        private readonly double overallDifficulty;
        private readonly double mechanicalDifficultyRating;
        private readonly double sliderFactor;

        public OsuRatingCalculator(GameMods mods, uint totalHits, double approachRate, double overallDifficulty, double mechanicalDifficultyRating,
                                   double sliderFactor)
        {
            this.mods = mods;
            this.totalHits = totalHits;
            this.approachRate = approachRate;
            this.overallDifficulty = overallDifficulty;
            this.mechanicalDifficultyRating = mechanicalDifficultyRating;
            this.sliderFactor = sliderFactor;
        }

        private double ArLengthBonus()
            => 0.95
               + 0.4 * RustMath.Min(totalHits / 2000.0, 1.0)
               + (totalHits > 2000 ? 1.0 : 0.0) * Math.Log10(totalHits / 2000.0) * 0.5;

        public double ComputeAimRating(double aimDifficultyValue)
        {
            if (mods.Ap)
                return 0.0;

            double aimRating = CalculateDifficultyRating(aimDifficultyValue);

            if (mods.Td)
                aimRating = Math.Pow(aimRating, 0.8);

            if (mods.Rx)
                aimRating *= 0.9;

            if (mods.AttractionStrength() is double magnetisedStrength)
                aimRating *= 1.0 - magnetisedStrength;

            double ratingMultiplier = 1.0;

            double arLengthBonus = ArLengthBonus();

            double arFactor;

            if (mods.Rx)
                arFactor = 0.0;
            else if (approachRate > 10.33)
                arFactor = 0.3 * (approachRate - 10.33);
            else if (approachRate < 8.0)
                arFactor = 0.05 * (8.0 - approachRate);
            else
                arFactor = 0.0;

            // * Buff for longer maps with high AR.
            ratingMultiplier += arFactor * arLengthBonus;

            if (mods.Hd)
            {
                double visibilityFactor = CalculateAimVisibilityFactor(mechanicalDifficultyRating, approachRate);

                ratingMultiplier += CalculateVisibilityBonus(mods, approachRate, visibilityFactor, sliderFactor);
            }

            // * It is important to consider accuracy difficulty when scaling with accuracy.
            ratingMultiplier *= 0.98 + RustMath.Pow2(RustMath.Max(overallDifficulty, 0.0)) / 2500.0;

            return aimRating * RustMath.Cbrt(ratingMultiplier);
        }

        public double ComputeSpeedRating(double speedDifficultyValue)
        {
            if (mods.Rx)
                return 0.0;

            double speedRating = CalculateDifficultyRating(speedDifficultyValue);

            if (mods.Ap)
                speedRating *= 0.5;

            if (mods.AttractionStrength() is double magnetisedStrength)
            {
                // * reduce speed rating because of the speed distance scaling, with maximum reduction being 0.7x
                speedRating *= 1.0 - magnetisedStrength * 0.3;
            }

            double ratingMultiplier = 1.0;

            double arLengthBonus = ArLengthBonus();

            double arFactor;

            if (mods.Ap)
                arFactor = 0.0;
            else if (approachRate > 10.33)
                arFactor = 0.3 * (approachRate - 10.33);
            else
                arFactor = 0.0;

            // * Buff for longer maps with high AR.
            ratingMultiplier += arFactor * arLengthBonus;

            if (mods.Hd)
            {
                double visibilityFactor = CalculateSpeedVisibilityFactor(mechanicalDifficultyRating, approachRate);

                ratingMultiplier += CalculateVisibilityBonus(mods, approachRate, visibilityFactor, null);
            }

            ratingMultiplier *= 0.95 + RustMath.Pow2(RustMath.Max(overallDifficulty, 0.0)) / 750.0;

            return speedRating * RustMath.Cbrt(ratingMultiplier);
        }

        public double ComputeFlashlightRating(double flashlightDifficultyValue)
        {
            if (!mods.Fl)
                return 0.0;

            double flashlightRating = CalculateDifficultyRating(flashlightDifficultyValue);

            if (mods.Td)
                flashlightRating = Math.Pow(flashlightRating, 0.8);

            if (mods.Rx)
                flashlightRating *= 0.7;
            else if (mods.Ap)
                flashlightRating *= 0.4;

            if (mods.AttractionStrength() is double magnetisedStrength)
                flashlightRating *= 1.0 - magnetisedStrength;

            if (mods.DeflateStartScale() is double deflateInitialScale)
                flashlightRating *= RustMath.Clamp(DifficultyUtil.ReverseLerp(deflateInitialScale, 11.0, 1.0), 0.1, 1.0);

            double ratingMultiplier = 1.0;

            // * Account for shorter maps having a higher ratio of 0 combo/100 combo flashlight radius.
            ratingMultiplier *= 0.7
                                + 0.1 * RustMath.Min(totalHits / 200.0, 1.0)
                                + (totalHits > 200 ? 1.0 : 0.0) * 0.2 * RustMath.Min(RustMath.SaturatingSub(totalHits, 200) / 200.0, 1.0);

            // * It is important to consider accuracy difficulty when scaling with accuracy.
            ratingMultiplier *= 0.98 + RustMath.Pow2(RustMath.Max(overallDifficulty, 0.0)) / 2500.0;

            return flashlightRating * Math.Sqrt(ratingMultiplier);
        }

        public static double CalculateVisibilityBonus(GameMods mods, double approachRate, double? visibilityFactor, double? sliderFactor)
        {
            // * NOTE: TC's effect is only noticeable in performance calculations until lazer mods are accounted for server-side.
            bool isAlwaysPartiallyVisible = mods.HdOnlyFadeApproachCircles() == true || mods.Tc;

            // * Start from normal curve, rewarding lower AR up to AR7
            double readingBonus = 0.04 * (12.0 - RustMath.Max(approachRate, 7.0));

            readingBonus *= visibilityFactor ?? 1.0;

            // * We want to reward slideraim on low AR less
            double sliderVisibilityFactor = Math.Pow(sliderFactor ?? 1.0, 3.0);

            // * For AR up to 0 - reduce reward for very low ARs when object is visible
            if (approachRate < 7.0)
            {
                double factor = isAlwaysPartiallyVisible ? 0.03 : 0.045;

                readingBonus += factor * (7.0 - RustMath.Max(approachRate, 0.0)) * sliderVisibilityFactor;
            }

            // * Starting from AR0 - cap values so they won't grow to infinity
            if (approachRate < 0.0)
            {
                double factor = isAlwaysPartiallyVisible ? 0.075 : 0.1;

                readingBonus += factor * (1.0 - Math.Pow(1.5, approachRate)) * sliderVisibilityFactor;
            }

            return readingBonus;
        }

        public static double CalculateDifficultyRating(double difficultyValue) => Math.Sqrt(difficultyValue) * DifficultyMultiplier;

        private static double CalculateAimVisibilityFactor(double mechanicalDifficultyRating, double approachRate)
        {
            const double arFactorEndPoint = 11.5;

            double mechanicalDifficultyFactor = DifficultyUtil.ReverseLerp(mechanicalDifficultyRating, 5.0, 10.0);
            double arFactorStartingPoint = RustMath.Lerp(9.0, 10.33, mechanicalDifficultyFactor);

            return DifficultyUtil.ReverseLerp(approachRate, arFactorEndPoint, arFactorStartingPoint);
        }

        private static double CalculateSpeedVisibilityFactor(double mechanicalDifficultyRating, double approachRate)
        {
            const double arFactorEndPoint = 11.5;

            double mechanicalDifficultyFactor = DifficultyUtil.ReverseLerp(mechanicalDifficultyRating, 5.0, 10.0);
            double arFactorStartingPoint = RustMath.Lerp(10.0, 10.33, mechanicalDifficultyFactor);

            return DifficultyUtil.ReverseLerp(approachRate, arFactorEndPoint, arFactorStartingPoint);
        }
    }
}
