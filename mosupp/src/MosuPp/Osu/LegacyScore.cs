using System;
using MosuPp.Model;
using MosuPp.Util;

namespace MosuPp.Osu
{
    internal sealed class LegacyScoreAttributes
    {
        public int AccuracyScore;
        public long ComboScore;
        public double BonusScoreRatio;
        public int BonusScore;
        public uint MaxCombo;
    }

    /// <summary>Port of rosu-pp's <c>OsuLegacyScoreSimulator</c>.</summary>
    internal sealed class OsuLegacyScoreSimulator
    {
        private readonly OsuObject[] osuObjects;
        private readonly int passedObjects;
        private readonly double scoreMultiplier;

        private int legacyBonusScore;
        private int standardisedBonusScore;
        private uint combo;

        public OsuLegacyScoreSimulator(OsuObject[] osuObjects, Beatmap map, int passedObjects)
        {
            // Note that no mods are being applied here. Apparently, this is how lazer wants it.
            BeatmapAttributes mapAttrs = BeatmapAttributes.Build(map, null);

            this.osuObjects = osuObjects;
            this.passedObjects = passedObjects;
            scoreMultiplier = ScoreMultiplier(map, mapAttrs, passedObjects);
        }

        public static int ScoreMultiplier(Beatmap map, BeatmapAttributes mapAttrs, int passedObjects)
        {
            var hitObjects = map.HitObjects;

            int objectCount = Math.Min(hitObjects.Count, passedObjects);

            int drainLen = 0;

            if (hitObjects.Count > 0)
            {
                HitObject first = hitObjects[0];
                int lastIdx = Math.Max(passedObjects, 1) - 1; // saturating_sub(1)
                HitObject last = lastIdx < hitObjects.Count ? hitObjects[lastIdx] : hitObjects[hitObjects.Count - 1];

                int breakLen = 0;

                foreach (BreakPeriod b in map.Breaks)
                {
                    // Note that this does not account for notes appearing during
                    // breaks or overlapping breaks.
                    if (!(b.EndTime < last.StartTime))
                        break;

                    breakLen += RustMath.ToI32(RustMath.RoundTiesEven(b.EndTime)) - RustMath.ToI32(RustMath.RoundTiesEven(b.StartTime));
                }

                int fullLen = RustMath.ToI32(RustMath.RoundTiesEven(last.StartTime)) - RustMath.ToI32(RustMath.RoundTiesEven(first.StartTime));

                drainLen = (fullLen - breakLen) / 1000;
            }

            return CalculateDifficultyPeppyStars(mapAttrs, objectCount, drainLen);
        }

        private static int CalculateDifficultyPeppyStars(BeatmapAttributes mapAttrs, int objectCount, int drainLen)
        {
            // NOTE: rosu-pp uses f64 instead of C#'s decimal type for simplicity reasons and
            // sacrifices precision while doing so; this port does the same to stay identical.
            double objectToDrainRatio = drainLen != 0 ? RustMath.Clamp((double)objectCount / drainLen * 8.0, 0.0, 16.0) : 16.0;

            double drainRate = mapAttrs.Hp;
            double overallDifficulty = mapAttrs.Od;
            double circleSize = mapAttrs.Cs;

            return RustMath.ToI32(RustMath.RoundTiesEven((drainRate + overallDifficulty + circleSize + objectToDrainRatio) / 38.0 * 5.0));
        }

        public LegacyScoreAttributes Simulate()
        {
            var attrs = new LegacyScoreAttributes();

            for (int i = 0; i < osuObjects.Length && i < passedObjects; i++)
                SimulateHit(osuObjects[i], attrs);

            attrs.BonusScoreRatio = legacyBonusScore == 0 ? 0.0 : (double)standardisedBonusScore / legacyBonusScore;
            attrs.BonusScore = legacyBonusScore;
            attrs.MaxCombo = combo;

            return attrs;
        }

        private enum HitResult
        {
            None,
            SmallBonus,
            LargeBonus,
        }

        private void SimulateHit(OsuObject hitObject, LegacyScoreAttributes attrs)
        {
            switch (hitObject.Kind)
            {
                case OsuObjectKind.Circle:
                    Unrolled(attrs, true, false, true, 300, HitResult.None);
                    break;

                case OsuObjectKind.Slider:
                    // Slider head
                    Unrolled(attrs, false, false, true, 30, HitResult.None);

                    foreach (NestedSliderObject nested in hitObject.Slider!.NestedObjects)
                    {
                        if (nested.Kind == NestedSliderObjectKind.Tick)
                            Unrolled(attrs, false, false, true, 10, HitResult.None);
                        else
                            Unrolled(attrs, false, false, true, 30, HitResult.None);
                    }

                    Unrolled(attrs, true, false, false, 300, HitResult.None);
                    break;

                case OsuObjectKind.Spinner:
                {
                    // * The spinner object applies a lenience because gameplay mechanics differ from osu-stable.
                    // * We'll redo the calculations to match osu-stable here...
                    const double maximumRotationsPerSecond = 477.0 / 60.0;

                    // * Normally, this value depends on the final overall difficulty. For simplicity, we'll only consider the worst case that maximises bonus score.
                    // * As we're primarily concerned with computing the maximum theoretical final score,
                    // * this will have the final effect of slightly underestimating bonus score achieved on stable when converting from score V1.
                    const double minimumRotationsPerSecond = 3.0;

                    double secondsDuration = hitObject.SpinnerDuration / 1000.0;

                    // * The total amount of half spins possible for the entire spinner.
                    int totalHalfSpinsPossible = RustMath.ToI32(secondsDuration * maximumRotationsPerSecond * 2.0);
                    // * The amount of half spins that are required to successfully complete the spinner (i.e. get a 300).
                    int halfSpinsRequiredForCompletion = RustMath.ToI32(secondsDuration * minimumRotationsPerSecond);
                    // * To be able to receive bonus points, the spinner must be rotated another 1.5 times.
                    int halfSpinsRequiredBeforeBonus = unchecked(halfSpinsRequiredForCompletion + 3);

                    for (long i = 0; i <= totalHalfSpinsPossible; i++)
                    {
                        if (i > halfSpinsRequiredBeforeBonus && (i - halfSpinsRequiredBeforeBonus) % 2 == 0)
                            Unrolled(attrs, false, true, false, 1100, HitResult.LargeBonus);
                        else if (i > 1 && i % 2 == 0)
                            Unrolled(attrs, false, true, false, 100, HitResult.SmallBonus);
                    }

                    Unrolled(attrs, true, false, true, 300, HitResult.None);
                    break;
                }
            }
        }

        private void Unrolled(LegacyScoreAttributes attrs, bool addScoreComboMultiplier, bool isBonus, bool increaseCombo, int scoreIncrease,
                              HitResult bonusResult)
        {
            double? factor = null;

            if (addScoreComboMultiplier)
                factor = (double)RustMath.SaturatingSub(combo, 1) * (scoreIncrease / 25);

            unchecked
            {
                if (isBonus)
                {
                    legacyBonusScore += scoreIncrease;
                    standardisedBonusScore += bonusResult switch
                    {
                        HitResult.SmallBonus => 10,
                        HitResult.LargeBonus => 50,
                        _ => 0,
                    };
                }
                else
                {
                    attrs.AccuracyScore += scoreIncrease;
                }

                if (increaseCombo)
                    combo++;

                if (factor is double f)
                    attrs.ComboScore += RustMath.ToI32(f * scoreMultiplier);
            }
        }
    }

    /// <summary>Port of rosu-pp's <c>NestedScorePerObject</c>.</summary>
    internal static class NestedScorePerObject
    {
        public static double Calculate(OsuObject[] objects, int passedObjects)
        {
            int nSliders = 0;
            int nRepeats = 0;
            int amountOfSmallTicks = 0;
            double spinnerScore = 0.0;
            int objectCount = 0;

            for (int i = 0; i < objects.Length && i < passedObjects; i++)
            {
                OsuObject h = objects[i];
                objectCount++;

                switch (h.Kind)
                {
                    case OsuObjectKind.Slider:
                        nSliders++;
                        nRepeats += h.Slider!.RepeatCount();
                        amountOfSmallTicks += h.Slider.TickCount();
                        break;
                    case OsuObjectKind.Spinner:
                        spinnerScore += CalculateSpinnerScore(h.SpinnerDuration);
                        break;
                }
            }

            const double bigTickScore = 30.0;
            const double smallTickScore = 10.0;

            // * 1 for head, 1 for tail
            int amountOfBigTicks = nSliders * 2;

            // * Add slider repeats
            amountOfBigTicks += nRepeats;

            double sliderScore = amountOfBigTicks * bigTickScore + amountOfSmallTicks * smallTickScore;

            return (sliderScore + spinnerScore) / objectCount;
        }

        private static double CalculateSpinnerScore(double duration)
        {
            const int spinScore = 100;
            const int bonusSpinScore = 1000;

            // * The spinner object applies a lenience because gameplay mechanics differ from osu-stable.
            // * We'll redo the calculations to match osu-stable here...
            const double maximumRotationsPerSecond = 477.0 / 60.0;

            // * Normally, this value depends on the final overall difficulty. For simplicity, we'll only consider the worst case that maximises bonus score.
            const double minimumRotationsPerSecond = 3.0;

            double secondsDuration = duration / 1000.0;

            unchecked
            {
                // * The total amount of half spins possible for the entire spinner.
                int totalHalfSpinsPossible = RustMath.ToI32(secondsDuration * maximumRotationsPerSecond * 2.0);
                // * The amount of half spins that are required to successfully complete the spinner (i.e. get a 300).
                int halfSpinsRequiredForCompletion = RustMath.ToI32(secondsDuration * minimumRotationsPerSecond);
                // * To be able to receive bonus points, the spinner must be rotated another 1.5 times.
                int halfSpinsRequiredBeforeBonus = halfSpinsRequiredForCompletion + 3;

                long score = 0;

                int fullSpins = totalHalfSpinsPossible / 2;

                // * Normal spin score
                score += spinScore * fullSpins;

                int bonusSpins = (totalHalfSpinsPossible - halfSpinsRequiredBeforeBonus) / 2;

                // * Reduce amount of bonus spins because we want to represent the more average case, rather than the best one.
                bonusSpins = Math.Max(bonusSpins - fullSpins / 2, 0);

                score += bonusSpinScore * bonusSpins;

                return score;
            }
        }
    }
}
