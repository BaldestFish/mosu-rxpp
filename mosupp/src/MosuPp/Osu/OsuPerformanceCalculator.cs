using System;
using MosuPp.Model;
using MosuPp.Util;

namespace MosuPp.Osu
{
    /// <summary>Port of rosu-pp's <c>OsuPerformanceCalculator</c>.</summary>
    internal sealed class OsuPerformanceCalculator
    {
        // * This is being adjusted to keep the final pp value scaled around what it used to be when changing things.
        public const double PerformanceBaseMultiplier = 1.14;

        private readonly OsuDifficultyAttributes attrs;
        private readonly GameMods mods;
        private readonly double acc;
        private readonly OsuScoreState state;
        private readonly bool usingClassicSliderAcc;

        public OsuPerformanceCalculator(OsuDifficultyAttributes attrs, GameMods mods, double acc, OsuScoreState state, bool usingClassicSliderAcc)
        {
            this.attrs = attrs;
            this.mods = mods;
            this.acc = acc;
            this.state = state;
            this.usingClassicSliderAcc = usingClassicSliderAcc;
        }

        public OsuPerformanceAttributes Calculate()
        {
            uint totalHitsU = state.HitResults.TotalHits;

            if (totalHitsU == 0)
                return new OsuPerformanceAttributes { Difficulty = attrs, State = state };

            double comboBasedEstimatedMissCount = CalculateComboBasedEstimatedMissCount();
            double? scoreBasedEstimatedMissCount = null;

            double effectiveMissCount;

            if (usingClassicSliderAcc && state.LegacyTotalScore.HasValue)
            {
                var legacyScoreMissCalc = new OsuLegacyScoreMissCalculator(state, acc, mods, attrs);
                scoreBasedEstimatedMissCount = legacyScoreMissCalc.Calculate();
                effectiveMissCount = scoreBasedEstimatedMissCount.Value;
            }
            else
            {
                // * Use combo-based miss count if this isn't a legacy score
                effectiveMissCount = comboBasedEstimatedMissCount;
            }

            effectiveMissCount = RustMath.Max(effectiveMissCount, state.HitResults.Misses);
            effectiveMissCount = RustMath.Min(effectiveMissCount, state.HitResults.TotalHits);

            double totalHits = totalHitsU;

            double multiplier = PerformanceBaseMultiplier;

            if (mods.Nf)
                multiplier *= RustMath.Max(1.0 - 0.02 * effectiveMissCount, 0.9);

            if (mods.So && totalHits > 0.0)
                multiplier *= 1.0 - Math.Pow(attrs.NSpinners / totalHits, 0.85);

            if (mods.Rx)
            {
                double od = attrs.Od;

                // * https://www.desmos.com/calculator/vspzsop6td
                // * we use OD13.3 as maximum since it's the value at which great hitwidow becomes 0
                // * this is well beyond currently maximum achievable OD which is 12.17 (DTx2 + DA with OD11)
                double n100Mult, n50Mult;

                if (od > 0.0)
                {
                    n100Mult = 0.75 * RustMath.Max(1.0 - od / 13.33, 0.0);
                    n50Mult = RustMath.Max(1.0 - Math.Pow(od / 13.33, 5.0), 0.0);
                }
                else
                {
                    n100Mult = 1.0;
                    n50Mult = 1.0;
                }

                // * As we're adding Oks and Mehs to an approximated number of combo breaks the result can be
                // * higher than total hits in specific scenarios (which breaks some calculations) so we need to clamp it.
                effectiveMissCount = RustMath.Min(
                    effectiveMissCount + state.HitResults.N100 * n100Mult + state.HitResults.N50 * n50Mult,
                    totalHits);
            }

            double? speedDeviation = CalculateSpeedDeviation();

            double aimValue = ComputeAimValue(effectiveMissCount, out double aimEstimatedSliderBreaks);
            double speedValue = ComputeSpeedValue(speedDeviation, effectiveMissCount, out double speedEstimatedSliderBreaks);
            double accValue = ComputeAccuracyValue();
            double flashlightValue = ComputeFlashlightValue(effectiveMissCount);

            double pp = Math.Pow(
                            Math.Pow(aimValue, 1.1) + Math.Pow(speedValue, 1.1) + Math.Pow(accValue, 1.1) + Math.Pow(flashlightValue, 1.1),
                            1.0 / 1.1)
                        * multiplier;

            return new OsuPerformanceAttributes
            {
                Difficulty = attrs,
                PpAcc = accValue,
                PpAim = aimValue,
                PpFlashlight = flashlightValue,
                PpSpeed = speedValue,
                Pp = pp,
                EffectiveMissCount = effectiveMissCount,
                SpeedDeviation = speedDeviation,
                ComboBasedEstimatedMissCount = comboBasedEstimatedMissCount,
                ScoreBasedEstimatedMissCount = scoreBasedEstimatedMissCount,
                AimEstimatedSliderBreaks = aimEstimatedSliderBreaks,
                SpeedEstimatedSliderBreaks = speedEstimatedSliderBreaks,
                State = state,
            };
        }

        private double LengthBonus(double totalHits)
            => 0.95 + 0.4 * RustMath.Min(totalHits / 2000.0, 1.0) + (totalHits > 2000.0 ? 1.0 : 0.0) * Math.Log10(totalHits / 2000.0) * 0.5;

        private double ComputeAimValue(double effectiveMissCount, out double aimEstimatedSliderBreaks)
        {
            aimEstimatedSliderBreaks = 0.0;

            if (mods.Ap)
                return 0.0;

            double aimDifficulty = attrs.Aim;

            if (attrs.NSliders > 0 && attrs.AimDifficultSliderCount > 0.0)
            {
                double estimateImproperlyFollowedDifficultSliders;

                if (usingClassicSliderAcc)
                {
                    // * When the score is considered classic (regardless if it was made on old client or not)
                    // * we consider all missing combo to be dropped difficult sliders
                    double maximumPossibleDroppedSliders = TotalImperfectHits();

                    estimateImproperlyFollowedDifficultSliders = RustMath.Clamp(
                        RustMath.Min(maximumPossibleDroppedSliders, unchecked(attrs.MaxCombo - state.MaxCombo)),
                        0.0,
                        attrs.AimDifficultSliderCount);
                }
                else
                {
                    // * We add tick misses here since they too mean that the player didn't follow the slider properly
                    // * We however aren't adding misses here because missing slider heads has a harsh penalty
                    // * by itself and doesn't mean that the rest of the slider wasn't followed properly
                    estimateImproperlyFollowedDifficultSliders = RustMath.Clamp(
                        unchecked(NSliderEndsDropped() + NLargeTickMiss()),
                        0.0,
                        attrs.AimDifficultSliderCount);
                }

                double sliderNerfFactor = (1.0 - attrs.SliderFactor)
                                          * Math.Pow(1.0 - estimateImproperlyFollowedDifficultSliders / attrs.AimDifficultSliderCount, 3.0)
                                          + attrs.SliderFactor;
                aimDifficulty *= sliderNerfFactor;
            }

            double aimValue = AimSkill.DifficultyToPerformance(aimDifficulty);

            double totalHits = TotalHits();

            aimValue *= LengthBonus(totalHits);

            if (effectiveMissCount > 0.0)
            {
                aimEstimatedSliderBreaks = CalculateEstimatedSliderBreaks(attrs.AimTopWeightedSliderFactor, effectiveMissCount);

                double relevantMissCount = RustMath.Min(effectiveMissCount + aimEstimatedSliderBreaks, TotalImperfectHits() + NLargeTickMiss());

                aimValue *= CalculateMissPenalty(relevantMissCount, attrs.AimDifficultStrainCount);
            }

            // * TC bonuses are excluded when blinds is present as the increased visual difficulty is unimportant when notes cannot be seen.
            if (mods.Bl)
            {
                aimValue *= 1.3 + (totalHits * (0.0016 / (1.0 + 2.0 * effectiveMissCount)) * Math.Pow(acc, 16.0)) * (1.0 - 0.003 * attrs.Hp * attrs.Hp);
            }
            else if (mods.Tc)
            {
                aimValue *= 1.0 + OsuRatingCalculator.CalculateVisibilityBonus(mods, attrs.Ar, attrs.SliderFactor, null);
            }

            aimValue *= acc;

            return aimValue;
        }

        private double ComputeSpeedValue(double? speedDeviationOpt, double effectiveMissCount, out double speedEstimatedSliderBreaks)
        {
            speedEstimatedSliderBreaks = 0.0;

            if (speedDeviationOpt is not double speedDeviation || mods.Rx)
                return 0.0;

            double speedValue = SpeedSkill.DifficultyToPerformance(attrs.Speed);

            double totalHits = TotalHits();

            speedValue *= LengthBonus(totalHits);

            if (effectiveMissCount > 0.0)
            {
                speedEstimatedSliderBreaks = CalculateEstimatedSliderBreaks(attrs.SpeedTopWeightedSliderFactor, effectiveMissCount);

                double relevantMissCount = RustMath.Min(effectiveMissCount + speedEstimatedSliderBreaks, TotalImperfectHits() + NLargeTickMiss());

                speedValue *= CalculateMissPenalty(relevantMissCount, attrs.SpeedDifficultStrainCount);
            }

            // * TC bonuses are excluded when blinds is present as the increased visual difficulty is unimportant when notes cannot be seen.
            if (mods.Bl)
            {
                // * Increasing the speed value by object count for Blinds isn't
                // * ideal, so the minimum buff is given.
                speedValue *= 1.12;
            }
            else if (mods.Tc)
            {
                speedValue *= 1.0 + OsuRatingCalculator.CalculateVisibilityBonus(mods, attrs.Ar, null, null);
            }

            double speedHighDeviationMult = CalculateSpeedHighDeviationNerf(speedDeviation);
            speedValue *= speedHighDeviationMult;

            // * Calculate accuracy assuming the worst case scenario
            OsuHitResults h = state.HitResults;
            double relevantTotalDiff = RustMath.Max(0.0, totalHits - attrs.SpeedNoteCount);
            double relevantN300 = RustMath.Max(h.N300 - relevantTotalDiff, 0.0);
            double relevantN100 = RustMath.Max(h.N100 - RustMath.Max(relevantTotalDiff - h.N300, 0.0), 0.0);
            double relevantN50 = RustMath.Max(h.N50 - RustMath.Max(relevantTotalDiff - unchecked(h.N300 + h.N100), 0.0), 0.0);

            double relevantAcc = RustMath.AlmostEq(attrs.SpeedNoteCount, 0.0)
                ? 0.0
                : (relevantN300 * 6.0 + relevantN100 * 2.0 + relevantN50) / (attrs.SpeedNoteCount * 6.0);

            double od = attrs.Od;

            // * Scale the speed value with accuracy and OD.
            speedValue *= Math.Pow((acc + relevantAcc) / 2.0, (14.5 - od) / 2.0);

            return speedValue;
        }

        private double ComputeAccuracyValue()
        {
            if (mods.Rx)
                return 0.0;

            // * This percentage only considers HitCircles of any value - in this part
            // * of the calculation we focus on hitting the timing hit window.
            uint amountHitObjectsWithAcc = attrs.NCircles;

            if (!usingClassicSliderAcc)
                amountHitObjectsWithAcc = unchecked(amountHitObjectsWithAcc + attrs.NSliders);

            OsuHitResults h = state.HitResults;

            double betterAccPercentage;

            unchecked
            {
                betterAccPercentage = amountHitObjectsWithAcc > 0
                    ? (double)(((int)h.N300 - Math.Max((int)h.TotalHits - (int)amountHitObjectsWithAcc, 0)) * 6 + (int)h.N100 * 2 + (int)h.N50)
                      / (double)(amountHitObjectsWithAcc * 6)
                    : 0.0;
            }

            // * It is possible to reach a negative accuracy with this formula. Cap it at zero - zero points.
            if (betterAccPercentage < 0.0)
                betterAccPercentage = 0.0;

            // * Lots of arbitrary values from testing.
            // * Considering to use derivation from perfect accuracy in a probabilistic manner - assume normal distribution.
            double accValue = Math.Pow(1.52163, attrs.Od) * Math.Pow(betterAccPercentage, 24.0) * 2.83;

            // * Bonus for many hitcircles - it's harder to keep good accuracy up for longer.
            accValue *= RustMath.Min(Math.Pow(amountHitObjectsWithAcc / 1000.0, 0.3), 1.15);

            // * Increasing the accuracy value by object count for Blinds isn't
            // * ideal, so the minimum buff is given.
            if (mods.Bl)
            {
                accValue *= 1.14;
            }
            else if (mods.Hd || mods.Tc)
            {
                // * Decrease bonus for AR > 10
                accValue *= 1.0 + 0.08 * DifficultyUtil.ReverseLerp(attrs.Ar, 11.5, 10.0);
            }

            if (mods.Fl)
                accValue *= 1.02;

            return accValue;
        }

        private double ComputeFlashlightValue(double effectiveMissCount)
        {
            if (!mods.Fl)
                return 0.0;

            double flashlightValue = FlashlightSkill.DifficultyToPerformance(attrs.Flashlight);

            double totalHits = TotalHits();

            // * Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0.0)
            {
                flashlightValue *= 0.97 * Math.Pow(1.0 - Math.Pow(effectiveMissCount / totalHits, 0.775), Math.Pow(effectiveMissCount, 0.875));
            }

            flashlightValue *= GetComboScalingFactor();

            // * Scale the flashlight value with accuracy _slightly_.
            flashlightValue *= 0.5 + acc / 2.0;

            return flashlightValue;
        }

        private double CalculateComboBasedEstimatedMissCount()
        {
            if (attrs.NSliders == 0)
                return state.HitResults.Misses;

            double missCount = state.HitResults.Misses;

            if (usingClassicSliderAcc)
            {
                // * Consider that full combo is maximum combo minus dropped slider tails since they don't contribute to combo but also don't break it
                // * In classic scores we can't know the amount of dropped sliders so we estimate to 10% of all sliders on the map
                double fullComboThreshold = attrs.MaxCombo - 0.1 * attrs.NSliders;

                if (state.MaxCombo < fullComboThreshold)
                    missCount = fullComboThreshold / RustMath.Max(state.MaxCombo, 1.0);

                // * In classic scores there can't be more misses than a sum of all non-perfect judgements
                missCount = RustMath.Min(missCount, TotalImperfectHits());

                // * Every slider has *at least* 2 combo attributed in classic mechanics.
                // * If they broke on a slider with a tick, then this still works since they would have lost at least 2 combo (the tick and the end)
                // * Using this as a max means a score that loses 1 combo on a map can't possibly have been a slider break.
                // * It must have been a slider end.
                uint maxPossibleSliderBreaks = Math.Min(attrs.NSliders, RustMath.SaturatingSub(attrs.MaxCombo, state.MaxCombo) / 2);

                double sliderBreaks = missCount - state.HitResults.Misses;

                if (sliderBreaks > maxPossibleSliderBreaks)
                    missCount = unchecked(state.HitResults.Misses + maxPossibleSliderBreaks);
            }
            else
            {
                double fullComboThreshold = unchecked(attrs.MaxCombo - NSliderEndsDropped());

                if (state.MaxCombo < fullComboThreshold)
                    missCount = fullComboThreshold / RustMath.Max(state.MaxCombo, 1.0);

                // * Combine regular misses with tick misses since tick misses break combo as well
                missCount = RustMath.Min(missCount, unchecked(NLargeTickMiss() + state.HitResults.Misses));
            }

            return missCount;
        }

        private double CalculateEstimatedSliderBreaks(double topWeightedSliderFactor, double effectiveMissCount)
        {
            if (!usingClassicSliderAcc || state.HitResults.N100 == 0)
                return 0.0;

            double missedComboPercent = 1.0 - (double)state.MaxCombo / attrs.MaxCombo;
            double estimatedSliderBreaks = RustMath.Min(effectiveMissCount * topWeightedSliderFactor, state.HitResults.N100);

            // * Scores with more Oks are more likely to have slider breaks.
            double okAdjustment = ((state.HitResults.N100 - estimatedSliderBreaks) + 0.5) / state.HitResults.N100;

            // * There is a low probability of extra slider breaks on effective miss counts close to 1, as score based calculations are good at indicating if only a single break occurred.
            estimatedSliderBreaks *= DifficultyUtil.Smoothstep(effectiveMissCount, 1.0, 2.0);

            return estimatedSliderBreaks * okAdjustment * DifficultyUtil.Logistic(missedComboPercent, 0.33, 15.0);
        }

        private double? CalculateSpeedDeviation()
        {
            if (TotalSuccessfulHits() == 0)
                return null;

            OsuHitResults h = state.HitResults;

            // * Calculate accuracy assuming the worst case scenario
            double speedNoteCount = attrs.SpeedNoteCount;
            speedNoteCount += (h.TotalHits - attrs.SpeedNoteCount) * 0.1;

            // * Assume worst case: all mistakes were on speed notes
            double relevantCountMiss = RustMath.Min(h.Misses, speedNoteCount);
            double relevantCountMeh = RustMath.Min(h.N50, speedNoteCount - relevantCountMiss);
            double relevantCountOk = RustMath.Min(h.N100, speedNoteCount - relevantCountMiss - relevantCountMeh);
            double relevantCountGreat = RustMath.Max(0.0, speedNoteCount - relevantCountMiss - relevantCountMeh - relevantCountOk);

            return CalculateDeviation(relevantCountGreat, relevantCountOk, relevantCountMeh);
        }

        private double? CalculateDeviation(double relevantCountGreat, double relevantCountOk, double relevantCountMeh)
        {
            if (relevantCountGreat + relevantCountOk + relevantCountMeh <= 0.0)
                return null;

            // * The sample proportion of successful hits.
            double n = RustMath.Max(1.0, relevantCountGreat + relevantCountOk);
            double p = relevantCountGreat / n;

            // * 99% critical value for the normal distribution (one-tailed).
            const double z = 2.32634787404;

            // * We can be 99% confident that the population proportion is at least this value.
            double pLowerBound = RustMath.Min((n * p + z * z / 2.0) / (n + z * z) - z / (n + z * z) * Math.Sqrt(n * p * (1.0 - p) + z * z / 4.0), p);

            double greatHitWindow = attrs.GreatHitWindow;
            double okHitWindow = attrs.OkHitWindow;
            double mehHitWindow = attrs.MehHitWindow;

            double deviation;

            if (pLowerBound > 0.01)
            {
                deviation = greatHitWindow / (Math.Sqrt(2.0) * DifficultyUtil.ErfInv(pLowerBound));

                // * Subtract the deviation provided by tails that land outside the ok hit window from the deviation computed above.
                // * This is equivalent to calculating the deviation of a normal distribution truncated at +-okHitWindow.
                double okHitWindowTailAmount = Math.Sqrt(2.0 / Math.PI)
                                               * okHitWindow
                                               * Math.Exp(-0.5 * RustMath.Pow2(okHitWindow / deviation))
                                               / (deviation * DifficultyUtil.Erf(okHitWindow / (Math.Sqrt(2.0) * deviation)));

                deviation *= Math.Sqrt(1.0 - okHitWindowTailAmount);
            }
            else
            {
                // * A tested limit value for the case of a score only containing oks.
                deviation = okHitWindow / Math.Sqrt(3.0);
            }

            // * Compute and add the variance for mehs, assuming that they are uniformly distributed.
            double mehVariance = (mehHitWindow * mehHitWindow + okHitWindow * mehHitWindow + okHitWindow * okHitWindow) / 3.0;

            return Math.Sqrt(((relevantCountGreat + relevantCountOk) * RustMath.Pow2(deviation) + relevantCountMeh * mehVariance)
                             / (relevantCountGreat + relevantCountOk + relevantCountMeh));
        }

        private double CalculateSpeedHighDeviationNerf(double speedDeviation)
        {
            double speedValue = SpeedSkill.DifficultyToPerformance(attrs.Speed);

            // * Decides a point where the PP value achieved compared to the speed deviation is assumed to be tapped improperly. Any PP above this point is considered "excess" speed difficulty.
            // * This is used to cause PP above the cutoff to scale logarithmically towards the original speed value thus nerfing the value.
            double excessSpeedDifficultyCutoff = 100.0 + 220.0 * Math.Pow(22.0 / speedDeviation, 6.5);

            if (speedValue <= excessSpeedDifficultyCutoff)
                return 1.0;

            const double scale = 50.0;

            double adjustedSpeedValue = scale * (Math.Log((speedValue - excessSpeedDifficultyCutoff) / scale + 1.0) + excessSpeedDifficultyCutoff / scale);

            // * 220 UR and less are considered tapped correctly to ensure that normal scores will be punished as little as possible
            double lerp = 1.0 - DifficultyUtil.ReverseLerp(speedDeviation, 22.0, 27.0);
            adjustedSpeedValue = RustMath.Lerp(adjustedSpeedValue, speedValue, lerp);

            return adjustedSpeedValue / speedValue;
        }

        // * Miss penalty assumes that a player will miss on the hardest parts of a map,
        // * so we use the amount of relatively difficult sections to adjust miss penalty
        // * to make it more punishing on maps with lower amount of hard sections.
        private static double CalculateMissPenalty(double missCount, double diffStrainCount)
            => 0.96 / ((missCount / (4.0 * Math.Pow(Math.Log(diffStrainCount), 0.94))) + 1.0);

        private double GetComboScalingFactor()
        {
            if (attrs.MaxCombo == 0)
                return 1.0;

            return RustMath.Min(Math.Pow(state.MaxCombo, 0.8) / Math.Pow(attrs.MaxCombo, 0.8), 1.0);
        }

        private double TotalHits() => state.HitResults.TotalHits;

        private uint TotalSuccessfulHits() => unchecked(state.HitResults.N300 + state.HitResults.N100 + state.HitResults.N50);

        private double TotalImperfectHits() => unchecked(state.HitResults.N100 + state.HitResults.N50 + state.HitResults.Misses);

        private uint NSliderEndsDropped() => unchecked(attrs.NSliders - state.HitResults.SliderEndHits);

        private uint NLargeTickMiss()
        {
            // Lazer unconditionally uses the "large tick miss" hitresult and
            // relies on this value being correctly provided by the caller / user.
            // On stable, this value should always be 0.
            //
            // This is a best-effort workaround to achieve the same behavior.
            if (usingClassicSliderAcc)
                return 0;

            return unchecked(attrs.NLargeTicks - state.HitResults.LargeTickHits);
        }
    }

    /// <summary>Port of rosu-pp's <c>OsuLegacyScoreMissCalculator</c>.</summary>
    internal sealed class OsuLegacyScoreMissCalculator
    {
        private readonly OsuScoreState state;
        private readonly double acc;
        private readonly GameMods mods;
        private readonly OsuDifficultyAttributes attrs;

        public OsuLegacyScoreMissCalculator(OsuScoreState state, double acc, GameMods mods, OsuDifficultyAttributes attrs)
        {
            this.state = state;
            this.acc = acc;
            this.mods = mods;
            this.attrs = attrs;
        }

        public double Calculate()
        {
            if (attrs.MaxCombo == 0)
                return 0.0;

            if (state.LegacyTotalScore is not uint legacyTotalScore)
                return 0.0;

            double scoreV1Multiplier = attrs.LegacyScoreBaseMultiplier * GetLegacyScoreMultiplier();
            double relevantComboPerObject = CalculateRelevantScoreComboPerObject();

            double maximumMissCount = CalculateMaximumComboBasedMissCount();

            double scoreObtainedDuringMaxCombo = CalculateScoreAtCombo(state.MaxCombo, relevantComboPerObject, scoreV1Multiplier);
            double remainingScore = legacyTotalScore - scoreObtainedDuringMaxCombo;

            if (remainingScore <= 0.0)
                return maximumMissCount;

            uint remainingCombo = unchecked(attrs.MaxCombo - state.MaxCombo);
            double expectedRemainingScore = CalculateScoreAtCombo(remainingCombo, relevantComboPerObject, scoreV1Multiplier);

            double scoreBasedMissCount = expectedRemainingScore / remainingScore;

            // * If there's less than one miss detected - let combo-based miss count decide if this is FC or not
            scoreBasedMissCount = RustMath.Max(scoreBasedMissCount, 1.0);

            // * Cap result by very harsh version of combo-based miss count
            return RustMath.Min(scoreBasedMissCount, maximumMissCount);
        }

        private double CalculateScoreAtCombo(uint combo, double relevantComboPerObject, double scoreV1Multiplier)
        {
            uint totalHits = state.HitResults.TotalHits;

            double estimatedObjects = combo / relevantComboPerObject - 1.0;

            // * The combo portion of ScoreV1 follows arithmetic progression
            // * Therefore, we calculate the combo portion of score using the combo per object and our current combo.
            double comboScore = relevantComboPerObject > 0.0
                ? (2.0 * (relevantComboPerObject - 1.0) + (estimatedObjects - 1.0) * relevantComboPerObject) * estimatedObjects / 2.0
                : 0.0;

            // * We then apply the accuracy and ScoreV1 multipliers to the resulting score.
            comboScore *= acc * 300.0 / 25.0 * scoreV1Multiplier;

            double objectsHit = (double)unchecked(totalHits - state.HitResults.Misses) * combo / attrs.MaxCombo;

            // * Score also has a non-combo portion we need to create the final score value.
            double nonComboScore = (300.0 + attrs.NestedScorePerObject) * acc * objectsHit;

            return comboScore + nonComboScore;
        }

        // * Calculates the relevant combo per object for legacy score.
        // * This assumes a uniform distribution for circles and sliders.
        // * This handles cases where objects (such as buzz sliders) do not fit a normal arithmetic progression model.
        private double CalculateRelevantScoreComboPerObject()
        {
            double comboScore = attrs.MaximumLegacyComboScore;

            // * We then reverse apply the ScoreV1 multipliers to get the raw value.
            comboScore /= 300.0 / 25.0 * attrs.LegacyScoreBaseMultiplier;

            // * Reverse the arithmetic progression to work out the amount of combo per object based on the score.
            double result = unchecked(((int)attrs.MaxCombo - 2) * (int)attrs.MaxCombo);
            result /= RustMath.Max(attrs.MaxCombo + 2.0 * (comboScore - 1.0), 1.0);

            return result;
        }

        private double CalculateMaximumComboBasedMissCount()
        {
            if (attrs.NSliders == 0)
                return state.HitResults.Misses;

            uint totalImperfectHits = unchecked(state.HitResults.N100 + state.HitResults.N50 + state.HitResults.Misses);

            double missCount = 0.0;

            // * Consider that full combo is maximum combo minus dropped slider tails since they don't contribute to combo but also don't break it
            // * In classic scores we can't know the amount of dropped sliders so we estimate to 10% of all sliders on the map
            double fullComboThreshold = attrs.MaxCombo - 0.1 * attrs.NSliders;

            if (state.MaxCombo < fullComboThreshold)
                missCount = Math.Pow(fullComboThreshold / RustMath.Max(state.MaxCombo, 1.0), 2.5);

            // * In classic scores there can't be more misses than a sum of all non-perfect judgements
            missCount = RustMath.Min(missCount, totalImperfectHits);

            // * Every slider has *at least* 2 combo attributed in classic mechanics.
            // * If they broke on a slider with a tick, then this still works since they would have lost at least 2 combo (the tick and the end)
            // * Using this as a max means a score that loses 1 combo on a map can't possibly have been a slider break.
            // * It must have been a slider end.
            uint maxPossibleSliderBreaks = Math.Min(unchecked(attrs.MaxCombo - state.MaxCombo) / 2, attrs.NSliders);

            double sliderBreaks = missCount - state.HitResults.Misses;

            if (sliderBreaks > maxPossibleSliderBreaks)
                missCount = unchecked(state.HitResults.Misses + maxPossibleSliderBreaks);

            return missCount;
        }

        private double GetLegacyScoreMultiplier()
        {
            bool scoreV2 = mods.Sv2;
            double multiplier = 1.0;

            if (mods.Nf)
                multiplier *= scoreV2 ? 1.0 : 0.5;

            if (mods.Ez)
                multiplier *= 0.5;

            if (mods.ClockRate() < 1.0)
                multiplier *= 0.3;

            if (mods.Hd)
                multiplier *= 1.06;

            if (mods.Hr)
                multiplier *= scoreV2 ? 1.10 : 1.06;

            if (mods.ClockRate() > 1.0)
                multiplier *= scoreV2 ? 1.20 : 1.12;

            if (mods.Fl)
                multiplier *= 1.12;

            if (mods.So)
                multiplier *= 0.9;

            if (mods.Rx || mods.Ap)
                multiplier *= 0.0;

            return multiplier;
        }
    }
}
