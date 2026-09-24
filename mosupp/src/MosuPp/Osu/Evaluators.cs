using System;
using System.Collections.Generic;
using MosuPp.Model;
using MosuPp.Util;

namespace MosuPp.Osu
{
    internal static class AimEvaluator
    {
        private const double WideAngleMultiplier = 1.5;
        private const double AcuteAngleMultiplier = 2.55;
        private const double SliderMultiplier = 1.35;
        private const double VelocityChangeMultiplier = 0.75;
        private const double WiggleMultiplier = 1.02;

        public static double EvaluateDiffOf(OsuDifficultyObject curr, List<OsuDifficultyObject> diffObjects, bool withSliderTravelDist)
        {
            OsuDifficultyObject osuCurrObj = curr;
            OsuDifficultyObject? osuLastLastObj = curr.Previous(1, diffObjects);
            OsuDifficultyObject? osuLastObj = curr.Previous(0, diffObjects);

            if (osuLastLastObj == null || osuLastObj == null || curr.Base.IsSpinner || osuLastObj.Base.IsSpinner)
                return 0.0;

            const int radius = OsuDifficultyObject.NormalizedRadius;
            const int diameter = OsuDifficultyObject.NormalizedDiameter;

            // * Calculate the velocity to the current hitobject, which starts
            // * with a base distance / time assuming the last object is a hitcircle.
            double currVel = osuCurrObj.LazyJumpDist / osuCurrObj.AdjustedDeltaTime;

            // * But if the last object is a slider, then we extend the travel
            // * velocity through the slider into the current object.
            if (osuLastObj.Base.IsSlider && withSliderTravelDist)
            {
                // * calculate the slider velocity from slider head to slider end.
                double travelVel = osuLastObj.TravelDist / osuLastObj.TravelTime;
                // * calculate the movement velocity from slider end to current object
                double movementVel = osuCurrObj.MinJumpDist / osuCurrObj.MinJumpTime;

                // * take the larger total combined velocity.
                currVel = RustMath.Max(currVel, movementVel + travelVel);
            }

            // * As above, do the same for the previous hitobject.
            double prevVel = osuLastObj.LazyJumpDist / osuLastObj.AdjustedDeltaTime;

            if (osuLastLastObj.Base.IsSlider && withSliderTravelDist)
            {
                double travelVel = osuLastLastObj.TravelDist / osuLastLastObj.TravelTime;
                double movementVel = osuLastObj.MinJumpDist / osuLastObj.MinJumpTime;

                prevVel = RustMath.Max(prevVel, movementVel + travelVel);
            }

            double wideAngleBonus = 0.0;
            double acuteAngleBonus = 0.0;
            double sliderBonus = 0.0;
            double velChangeBonus = 0.0;
            double wiggleBonus = 0.0;

            // * Start strain with regular velocity.
            double aimStrain = currVel;

            if (osuCurrObj.Angle is double currAngle && osuLastObj.Angle is double lastAngle)
            {
                // * Rewarding angles, take the smaller velocity as base.
                double angleBonus = RustMath.Min(currVel, prevVel);

                // * If rhythms are the same.
                if (RustMath.Max(osuCurrObj.AdjustedDeltaTime, osuLastObj.AdjustedDeltaTime)
                    < 1.25 * RustMath.Min(osuCurrObj.AdjustedDeltaTime, osuLastObj.AdjustedDeltaTime))
                {
                    acuteAngleBonus = CalcAcuteAngleBonus(currAngle);

                    // * Penalize angle repetition.
                    acuteAngleBonus *= 0.08 + 0.92 * (1.0 - RustMath.Min(acuteAngleBonus, Math.Pow(CalcAcuteAngleBonus(lastAngle), 3.0)));

                    // * Apply acute angle bonus for BPM above 300 1/2 and distance more than one diameter
                    acuteAngleBonus *= angleBonus
                                       * DifficultyUtil.Smootherstep(DifficultyUtil.MillisecondsToBpm(osuCurrObj.AdjustedDeltaTime, 2), 300.0, 400.0)
                                       * DifficultyUtil.Smootherstep(osuCurrObj.LazyJumpDist, diameter, diameter * 2);
                }

                wideAngleBonus = CalcWideAngleBonus(currAngle);

                // * Penalize angle repetition.
                wideAngleBonus *= 1.0 - RustMath.Min(wideAngleBonus, Math.Pow(CalcWideAngleBonus(lastAngle), 3.0));

                // * Apply full wide angle bonus for distance more than one diameter
                wideAngleBonus *= angleBonus * DifficultyUtil.Smootherstep(osuCurrObj.LazyJumpDist, 0.0, diameter);

                // * Apply wiggle bonus for jumps that are [radius, 3*diameter] in distance, with < 110 angle
                // * https://www.desmos.com/calculator/dp0v0nvowc
                wiggleBonus = angleBonus
                              * DifficultyUtil.Smootherstep(osuCurrObj.LazyJumpDist, radius, diameter)
                              * Math.Pow(DifficultyUtil.ReverseLerp(osuCurrObj.LazyJumpDist, diameter * 3, diameter), 1.8)
                              * DifficultyUtil.Smootherstep(currAngle, RustMath.ToRadians(110.0), RustMath.ToRadians(60.0))
                              * DifficultyUtil.Smootherstep(osuLastObj.LazyJumpDist, radius, diameter)
                              * Math.Pow(DifficultyUtil.ReverseLerp(osuLastObj.LazyJumpDist, diameter * 3, diameter), 1.8)
                              * DifficultyUtil.Smootherstep(lastAngle, RustMath.ToRadians(110.0), RustMath.ToRadians(60.0));

                OsuDifficultyObject? osuLast2Obj = curr.Previous(2, diffObjects);

                if (osuLast2Obj != null)
                {
                    float distance = (osuLast2Obj.Base.StackedPos - osuLastObj.Base.StackedPos).Length();

                    if (distance < 1f)
                        wideAngleBonus *= 1.0 - 0.35 * (1f - distance);
                }
            }

            if (RustMath.NotEq(RustMath.Max(prevVel, currVel), 0.0))
            {
                // * We want to use the average velocity over the whole object when awarding
                // * differences, not the individual jump and slider path velocities.
                prevVel = (osuLastObj.LazyJumpDist + osuLastLastObj.TravelDist) / osuLastObj.AdjustedDeltaTime;
                currVel = (osuCurrObj.LazyJumpDist + osuLastObj.TravelDist) / osuCurrObj.AdjustedDeltaTime;

                // * Scale with ratio of difference compared to 0.5 * max dist.
                double distRatio = DifficultyUtil.Smoothstep(Math.Abs(prevVel - currVel) / RustMath.Max(prevVel, currVel), 0.0, 1.0);

                // * Reward for % distance up to 125 / strainTime for overlaps where velocity is still changing.
                double overlapVelBuff = RustMath.Min(
                    diameter * 1.25 / RustMath.Min(osuCurrObj.AdjustedDeltaTime, osuLastObj.AdjustedDeltaTime),
                    Math.Abs(prevVel - currVel));

                velChangeBonus = overlapVelBuff * distRatio;

                // * Penalize for rhythm changes.
                double bonusBase = RustMath.Min(osuCurrObj.AdjustedDeltaTime, osuLastObj.AdjustedDeltaTime)
                                   / RustMath.Max(osuCurrObj.AdjustedDeltaTime, osuLastObj.AdjustedDeltaTime);
                velChangeBonus *= RustMath.Pow2(bonusBase);
            }

            if (osuLastObj.Base.IsSlider)
            {
                // * Reward sliders based on velocity.
                sliderBonus = osuLastObj.TravelDist / osuLastObj.TravelTime;
            }

            aimStrain += wiggleBonus * WiggleMultiplier;
            aimStrain += velChangeBonus * VelocityChangeMultiplier;

            // * Add in acute angle bonus or wide angle bonus, whichever is larger.
            aimStrain += RustMath.Max(acuteAngleBonus * AcuteAngleMultiplier, wideAngleBonus * WideAngleMultiplier);
            aimStrain *= osuCurrObj.SmallCircleBonus;

            // * Add in additional slider velocity bonus.
            if (withSliderTravelDist)
                aimStrain += sliderBonus * SliderMultiplier;

            return aimStrain;
        }

        private static double CalcWideAngleBonus(double angle) => DifficultyUtil.Smoothstep(angle, RustMath.ToRadians(40.0), RustMath.ToRadians(140.0));

        private static double CalcAcuteAngleBonus(double angle) => DifficultyUtil.Smoothstep(angle, RustMath.ToRadians(140.0), RustMath.ToRadians(40.0));
    }

    internal static class SpeedEvaluator
    {
        private const double SingleSpacingThreshold = OsuDifficultyObject.NormalizedDiameter * 1.25; // 1.25 circles distance between centers
        private const double MinSpeedBonus = 200.0; // 200 BPM 1/4th
        private const double SpeedBalancingFactor = 40.0;
        private const double DistMultiplier = 0.8;

        public static double EvaluateDiffOf(OsuDifficultyObject curr, List<OsuDifficultyObject> diffObjects, double hitWindow, bool autopilot)
        {
            if (curr.Base.IsSpinner)
                return 0.0;

            // * derive strainTime for calculation
            OsuDifficultyObject osuCurrObj = curr;
            OsuDifficultyObject? osuPrevObj = curr.Previous(0, diffObjects);
            OsuDifficultyObject? osuNextObj = curr.Next(0, diffObjects);

            double strainTime = curr.AdjustedDeltaTime;
            double doubletapness = 1.0 - osuCurrObj.GetDoubletapness(osuNextObj, hitWindow);

            // * Cap deltatime to the OD 300 hitwindow.
            // * 0.93 is derived from making sure 260bpm OD8 streams aren't nerfed harshly, whilst 0.92 limits the effect of the cap.
            strainTime /= RustMath.Clamp((strainTime / hitWindow) / 0.93, 0.92, 1.0);

            double speedBonus = 0.0;

            if (DifficultyUtil.MillisecondsToBpm(strainTime) > MinSpeedBonus)
            {
                // * Add additional scaling bonus for streams/bursts higher than 200bpm
                double @base = (DifficultyUtil.BpmToMilliseconds(MinSpeedBonus) - strainTime) / SpeedBalancingFactor;
                speedBonus = 0.75 * RustMath.Pow2(@base);
            }

            double travelDist = osuPrevObj?.TravelDist ?? 0.0;
            double dist = travelDist + osuCurrObj.MinJumpDist;

            // * Cap distance at single_spacing_threshold
            dist = RustMath.Min(SingleSpacingThreshold, dist);

            // * Max distance bonus is 1 * `distance_multiplier` at single_spacing_threshold
            double distBonus = Math.Pow(dist / SingleSpacingThreshold, 3.95) * DistMultiplier;

            distBonus *= Math.Sqrt(osuCurrObj.SmallCircleBonus);

            if (autopilot)
                distBonus = 0.0;

            // * Base difficulty with all bonuses
            double difficulty = (1.0 + speedBonus + distBonus) * 1000.0 / strainTime;

            // * Apply penalty if there's doubletappable doubles
            return difficulty * doubletapness;
        }
    }

    internal static class RhythmEvaluator
    {
        private const int HistoryTimeMax = 5 * 1000; // 5 seconds
        private const int HistoryObjectsMax = 32;
        private const double RhythmOverallMultiplier = 1.0;
        private const double RhythmRatioMultiplier = 15.0;
        private const int MinDeltaTime = 25;

        private struct RhythmIsland
        {
            public double DeltaDifferenceEps;
            public int Delta;
            public int DeltaCount;

            public static RhythmIsland New(double deltaDifferenceEps) => new RhythmIsland { DeltaDifferenceEps = deltaDifferenceEps };

            public static RhythmIsland NewWithDelta(int delta, double deltaDifferenceEps)
                => new RhythmIsland { DeltaDifferenceEps = deltaDifferenceEps, Delta = Math.Max(delta, MinDeltaTime), DeltaCount = 1 };

            public void AddDelta(int delta)
            {
                if (Delta == int.MaxValue)
                    Delta = Math.Max(delta, MinDeltaTime);

                DeltaCount++;
            }

            public bool IsSimilarPolarity(in RhythmIsland other) => DeltaCount % 2 == other.DeltaCount % 2;

            public bool IsDefault() => Math.Abs(DeltaDifferenceEps) < RustMath.F64Epsilon && Delta == int.MaxValue && DeltaCount == 0;

            public bool Eq(in RhythmIsland other) => Math.Abs((double)(Delta - other.Delta)) < DeltaDifferenceEps && DeltaCount == other.DeltaCount;
        }

        private sealed class IslandCount
        {
            public RhythmIsland Island;
            public int Count;
        }

        public static double EvaluateDiffOf(OsuDifficultyObject curr, List<OsuDifficultyObject> diffObjects, double hitWindow)
        {
            if (curr.Base.IsSpinner)
                return 0.0;

            double rhythmComplexitySum = 0.0;

            double deltaDifferenceEps = hitWindow * 0.3;

            RhythmIsland island = RhythmIsland.New(deltaDifferenceEps);
            RhythmIsland prevIsland = RhythmIsland.New(deltaDifferenceEps);

            // * we can't use dictionary here because we need to compare island with a tolerance
            // * which is impossible to pass into the hash comparer
            var islandCounts = new List<IslandCount>();

            // * store the ratio of the current start of an island to buff for tighter rhythms
            double startRatio = 0.0;

            bool firstDeltaSwitch = false;

            int historicalNoteCount = Math.Min(curr.Idx, HistoryObjectsMax);

            int rhythmStart = 0;

            while (true)
            {
                OsuDifficultyObject? prev = curr.Previous(rhythmStart, diffObjects);

                if (prev == null || !(rhythmStart + 2 < historicalNoteCount && curr.StartTime - prev.StartTime < HistoryTimeMax))
                    break;

                rhythmStart++;
            }

            OsuDifficultyObject? prevObj = curr.Previous(rhythmStart, diffObjects);
            OsuDifficultyObject? lastObj = curr.Previous(rhythmStart + 1, diffObjects);

            if (prevObj != null && lastObj != null)
            {
                // * we go from the furthest object back to the current one
                for (int i = rhythmStart; i >= 1; i--)
                {
                    OsuDifficultyObject? currObj = curr.Previous(i - 1, diffObjects);

                    if (currObj == null)
                        break;

                    // * scales note 0 to 1 from history to now
                    double timeDecay = (HistoryTimeMax - (curr.StartTime - currObj.StartTime)) / HistoryTimeMax;
                    double noteDecay = (double)(historicalNoteCount - i) / historicalNoteCount;

                    // * either we're limited by time or limited by object count.
                    double currHistoricalDecay = RustMath.Min(noteDecay, timeDecay);

                    // * Use custom cap value to ensure that at this point delta time is actually zero
                    double currDelta = RustMath.Max(currObj.DeltaTime, 1e-7);
                    double prevDelta = RustMath.Max(prevObj.DeltaTime, 1e-7);
                    double lastDelta = RustMath.Max(lastObj.DeltaTime, 1e-7);

                    // * calculate how much current delta difference deserves a rhythm bonus
                    // * this function is meant to reduce rhythm bonus for deltas that are multiples of each other (i.e 100 and 200)
                    double deltaDifference = RustMath.Max(prevDelta, currDelta) / RustMath.Min(prevDelta, currDelta);

                    // * Take only the fractional part of the value since we're only interested in punishing multiples
                    double deltaDifferenceFraction = deltaDifference - Math.Truncate(deltaDifference);

                    double currRatio = 1.0 + RhythmRatioMultiplier * RustMath.Min(DifficultyUtil.SmoothstepBellCurve(deltaDifferenceFraction, 0.5, 0.5), 0.5);

                    // * reduce ratio bonus if delta difference is too big
                    double differenceMultiplier = RustMath.Clamp(2.0 - deltaDifference / 8.0, 0.0, 1.0);

                    double windowPenalty = RustMath.Min(RustMath.Max(Math.Abs(prevDelta - currDelta) - deltaDifferenceEps, 0.0) / deltaDifferenceEps, 1.0);

                    double effectiveRatio = windowPenalty * currRatio * differenceMultiplier;

                    if (firstDeltaSwitch)
                    {
                        if (Math.Abs(prevDelta - currDelta) < deltaDifferenceEps)
                        {
                            // * island is still progressing
                            island.AddDelta(RustMath.ToI32(currDelta));
                        }
                        else
                        {
                            // * bpm change is into slider, this is easy acc window
                            if (currObj.Base.IsSlider)
                                effectiveRatio *= 0.125;

                            // * bpm change was from a slider, this is easier typically than circle -> circle
                            // * unintentional side effect is that bursts with kicksliders at the ends might have lower difficulty than bursts without sliders
                            if (prevObj.Base.IsSlider)
                                effectiveRatio *= 0.3;

                            // * repeated island polarity (2 -> 4, 3 -> 5)
                            if (island.IsSimilarPolarity(prevIsland))
                                effectiveRatio *= 0.5;

                            // * previous increase happened a note ago, 1/1->1/2-1/4, dont want to buff this.
                            if (lastDelta > prevDelta + deltaDifferenceEps && prevDelta > currDelta + deltaDifferenceEps)
                                effectiveRatio *= 0.125;

                            // * repeated island size (ex: triplet -> triplet)
                            // * TODO: remove this nerf since its staying here only for balancing purposes because of the flawed ratio calculation
                            if (prevIsland.DeltaCount == island.DeltaCount)
                                effectiveRatio *= 0.5;

                            IslandCount? islandCount = null;

                            foreach (IslandCount entry in islandCounts)
                            {
                                if (entry.Island.Eq(island))
                                {
                                    islandCount = entry;
                                    break;
                                }
                            }

                            if (islandCount != null && !islandCount.Island.IsDefault())
                            {
                                // * only add island to island counts if they're going one after another
                                if (prevIsland.Eq(island))
                                    islandCount.Count++;

                                // * repeated island (ex: triplet -> triplet)
                                double power = DifficultyUtil.Logistic(island.Delta, 58.33, 0.24, 2.75);
                                effectiveRatio *= RustMath.Min(3.0 / islandCount.Count, Math.Pow(1.0 / islandCount.Count, power));
                            }
                            else
                            {
                                islandCounts.Add(new IslandCount { Island = island, Count = 1 });
                            }

                            // * scale down the difficulty if the object is doubletappable
                            double doubletapness = prevObj.GetDoubletapness(currObj, hitWindow);
                            effectiveRatio *= 1.0 - doubletapness * 0.75;

                            rhythmComplexitySum += Math.Sqrt(effectiveRatio * startRatio) * currHistoricalDecay;

                            startRatio = effectiveRatio;

                            prevIsland = island;

                            // * we're slowing down, stop counting
                            if (prevDelta + deltaDifferenceEps < currDelta)
                            {
                                // * if we're speeding up, this stays true and we keep counting island size.
                                firstDeltaSwitch = false;
                            }

                            island = RhythmIsland.NewWithDelta(RustMath.ToI32(currDelta), deltaDifferenceEps);
                        }
                    }
                    else if (prevDelta > currDelta + deltaDifferenceEps)
                    {
                        // * we're speeding up.
                        // * Begin counting island until we change speed again.
                        firstDeltaSwitch = true;

                        // * bpm change is into slider, this is easy acc window
                        if (currObj.Base.IsSlider)
                            effectiveRatio *= 0.6;

                        // * bpm change was from a slider, this is easier typically than circle -> circle
                        // * unintentional side effect is that bursts with kicksliders at the ends might have lower difficulty than bursts without sliders
                        if (prevObj.Base.IsSlider)
                            effectiveRatio *= 0.6;

                        startRatio = effectiveRatio;

                        island = RhythmIsland.NewWithDelta(RustMath.ToI32(currDelta), deltaDifferenceEps);
                    }

                    lastObj = prevObj;
                    prevObj = currObj;
                }
            }

            // * produces multiplier that can be applied to strain. range [1, infinity) (not really though)
            double rhythmDifficulty = Math.Sqrt(4.0 + rhythmComplexitySum * RhythmOverallMultiplier) / 2.0;
            rhythmDifficulty *= 1.0 - curr.GetDoubletapness(curr.Next(0, diffObjects), hitWindow);

            return rhythmDifficulty;
        }
    }

    internal sealed class FlashlightEvaluator
    {
        private const double MaxOpacityBonus = 0.4;
        private const double HiddenBonus = 0.2;

        private const double MinVelocity = 0.5;
        private const double SliderMultiplier = 1.3;

        private const double MinAngleMultiplier = 0.2;

        private readonly double scalingFactor;
        private readonly double timePreempt;
        private readonly double timeFadeIn;

        public FlashlightEvaluator(double scalingFactor, double timePreempt, double timeFadeIn)
        {
            this.scalingFactor = scalingFactor;
            this.timePreempt = timePreempt;
            this.timeFadeIn = timeFadeIn;
        }

        public double EvaluateDiffOf(OsuDifficultyObject curr, List<OsuDifficultyObject> diffObjects, bool hidden)
        {
            if (curr.Base.IsSpinner)
                return 0.0;

            OsuDifficultyObject osuCurr = curr;
            OsuObject osuHitObj = curr.Base;

            double smallDistNerf = 1.0;
            double cumulativeStrainTime = 0.0;

            double result = 0.0;

            OsuDifficultyObject lastObj = osuCurr;

            double angleRepeatCount = 0.0;

            // * This is iterating backwards in time from the current object.
            for (int i = 0; i < Math.Min(curr.Idx, 10); i++)
            {
                OsuDifficultyObject? currObj = curr.Previous(i, diffObjects);

                if (currObj == null)
                    break;

                cumulativeStrainTime += lastObj.AdjustedDeltaTime;

                OsuObject currHitObj = currObj.Base;

                if (!currObj.Base.IsSpinner)
                {
                    double jumpDist = (osuHitObj.StackedPos - currHitObj.StackedEndPos).Length();

                    // * We want to nerf objects that can be easily seen within the Flashlight circle radius.
                    if (i == 0)
                        smallDistNerf = RustMath.Min(jumpDist / 75.0, 1.0);

                    // * We also want to nerf stacks so that only the first object of the stack is accounted for.
                    double stackNerf = RustMath.Min((currObj.LazyJumpDist / scalingFactor) / 25.0, 1.0);

                    // * Bonus based on how visible the object is.
                    double opacityBonus = 1.0 + MaxOpacityBonus * (1.0 - osuCurr.OpacityAt(currHitObj.StartTime, hidden, timePreempt, timeFadeIn));

                    result += stackNerf * opacityBonus * scalingFactor * jumpDist / cumulativeStrainTime;

                    if (currObj.Angle is double currObjAngle && osuCurr.Angle is double osuCurrAngle)
                    {
                        // * Objects further back in time should count less for the nerf.
                        if (Math.Abs(currObjAngle - osuCurrAngle) < 0.02)
                            angleRepeatCount += RustMath.Max(1.0 - 0.1 * i, 0.0);
                    }
                }

                lastObj = currObj;
            }

            result = RustMath.Pow2(smallDistNerf * result);

            // * Additional bonus for Hidden due to there being no approach circles.
            if (hidden)
                result *= 1.0 + HiddenBonus;

            // * Nerf patterns with repeated angles.
            result *= MinAngleMultiplier + (1.0 - MinAngleMultiplier) / (angleRepeatCount + 1.0);

            double sliderBonus = 0.0;

            if (osuCurr.Base.Kind == OsuObjectKind.Slider)
            {
                // * Invert the scaling factor to determine the true travel distance independent of circle size.
                double pixelTravelDist = osuCurr.LazyTravelDist / scalingFactor;

                // * Reward sliders based on velocity.
                sliderBonus = RustMath.PowHalf(RustMath.Max(pixelTravelDist / osuCurr.TravelTime - MinVelocity, 0.0));

                // * Longer sliders require more memorisation.
                sliderBonus *= pixelTravelDist;

                // * Nerf sliders with repeats, as less memorisation is required.
                int repeatCount = osuCurr.Base.Slider!.RepeatCount();

                if (repeatCount > 0)
                    sliderBonus /= repeatCount + 1;
            }

            result += sliderBonus * SliderMultiplier;

            return result;
        }
    }
}
