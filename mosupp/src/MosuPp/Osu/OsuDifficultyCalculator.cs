using System;
using System.Collections.Generic;
using MosuPp.Model;
using MosuPp.Util;

namespace MosuPp.Osu
{
    /// <summary>Port of rosu-pp's <c>osu::difficulty</c> module.</summary>
    internal static class OsuDifficultyCalculator
    {
        private const double StarRatingMultiplier = 0.0265;

        internal const double HdFadeInDurationMultiplier = 0.4;
        internal const double HdFadeOutDurationMultiplier = 0.3;

        private sealed class Skills
        {
            public AimSkill Aim = null!;
            public AimSkill AimNoSliders = null!;
            public SpeedSkill Speed = null!;
            public FlashlightSkill Flashlight = null!;

            public void Process(OsuDifficultyObject curr, List<OsuDifficultyObject> objects)
            {
                Aim.Process(curr, objects);
                AimNoSliders.Process(curr, objects);
                Speed.Process(curr, objects);
                Flashlight.Process(curr, objects);
            }
        }

        private sealed class DifficultyValues
        {
            public OsuObject[] OsuObjects = null!;
            public Skills Skills = null!;
            public OsuDifficultyAttributes Attrs = null!;
        }

        private static void EnsureOsu(Beatmap map)
        {
            if (map.Mode != GameMode.Osu)
                throw new NotSupportedException($"Only osu!standard maps are supported (map mode: {map.Mode}).");
        }

        public static OsuDifficultyAttributes Calculate(Difficulty difficulty, Beatmap map)
        {
            EnsureOsu(map);

            DifficultyValues values = CalculateValues(difficulty, map);
            OsuDifficultyAttributes attrs = values.Attrs;

            GameMods mods = difficulty.GetMods();
            int passedObjects = difficulty.GetPassedObjects();

            Eval(attrs, mods, values.Skills);

            var simulator = new OsuLegacyScoreSimulator(values.OsuObjects, map, passedObjects);
            LegacyScoreAttributes scoreAttrs = simulator.Simulate();
            attrs.MaximumLegacyComboScore = scoreAttrs.ComboScore;

            BeatmapAttributes mapAttrs = BeatmapAttributes.Build(map, difficulty);

            attrs.LegacyScoreBaseMultiplier = OsuLegacyScoreSimulator.ScoreMultiplier(map, mapAttrs, passedObjects);
            attrs.NestedScorePerObject = NestedScorePerObject.Calculate(values.OsuObjects, passedObjects);

            return attrs;
        }

        public static OsuStrains Strains(Difficulty difficulty, Beatmap map)
        {
            EnsureOsu(map);

            DifficultyValues values = CalculateValues(difficulty, map);

            return new OsuStrains
            {
                Aim = values.Skills.Aim.GetCurrentStrainPeaks(),
                AimNoSliders = values.Skills.AimNoSliders.GetCurrentStrainPeaks(),
                Speed = values.Skills.Speed.GetCurrentStrainPeaks(),
                Flashlight = values.Skills.Flashlight.GetCurrentStrainPeaks(),
            };
        }

        private static DifficultyValues CalculateValues(Difficulty difficulty, Beatmap map)
        {
            GameMods mods = difficulty.GetMods();
            int take = difficulty.GetPassedObjects();
            double clockRate = difficulty.GetClockRate();

            // ---- OsuDifficultySetup ----
            BeatmapAttributes mapAttrs = BeatmapAttributes.Build(map, difficulty);
            HitWindows hitWindows = mapAttrs.GetHitWindows();
            var scalingFactor = new ScalingFactor(mapAttrs.Cs);

            var attrs = new OsuDifficultyAttributes
            {
                Ar = mapAttrs.ApplyClockRate().Ar,
                Hp = mapAttrs.Hp,
                GreatHitWindow = hitWindows.OdGreat,
                OkHitWindow = hitWindows.OdOk,
                MehHitWindow = hitWindows.OdMeh,
            };

            double timePreempt = (float)(hitWindows.Ar * clockRate);

            // ---- objects ----
            OsuObject[] osuObjects = OsuConvert.ConvertObjects(map, scalingFactor, mods.Reflection(), timePreempt, take, attrs);

            List<OsuDifficultyObject> diffObjects = CreateDifficultyObjects(take, clockRate, scalingFactor, osuObjects);

            double greatHitWindow = hitWindows.OdGreat;

            Skills skills = CreateSkills(mods, scalingFactor, greatHitWindow, timePreempt);

            // The first hit object has no difficulty object
            int takeDiffObjects = Math.Max(Math.Min(map.HitObjects.Count, take) - 1, 0);

            for (int i = 0; i < diffObjects.Count && i < takeDiffObjects; i++)
                skills.Process(diffObjects[i], diffObjects);

            return new DifficultyValues
            {
                OsuObjects = osuObjects,
                Skills = skills,
                Attrs = attrs,
            };
        }

        private static Skills CreateSkills(GameMods mods, ScalingFactor scalingFactor, double greatHitWindow, double timePreempt)
        {
            double hitWindow = 2.0 * greatHitWindow;

            // * Preempt time can go below 450ms. Normally, this is achieved via the DT mod
            // * which uniformly speeds up all animations game wide regardless of AR.
            // * This uniform speedup is hard to match 1:1, however we can at least make
            // * AR>10 (via mods) feel good by extending the upper linear function above.
            // * Note that this doesn't exactly match the AR>10 visuals as they're
            // * classically known, but it feels good.
            // * This adjustment is necessary for AR>10, otherwise TimePreempt can
            // * become smaller leading to hitcircles not fully fading in.
            double timeFadeIn = mods.Hd
                ? timePreempt * HdFadeInDurationMultiplier
                : 400.0 * RustMath.Min(timePreempt / OsuObject.PreemptMin, 1.0);

            return new Skills
            {
                Aim = new AimSkill(true),
                AimNoSliders = new AimSkill(false),
                Speed = new SpeedSkill(hitWindow, mods.Ap),
                Flashlight = new FlashlightSkill(mods.Hd, scalingFactor.Radius, timePreempt, timeFadeIn),
            };
        }

        private static List<OsuDifficultyObject> CreateDifficultyObjects(int take, double clockRate, ScalingFactor scalingFactor, OsuObject[] osuObjects)
        {
            var diffObjects = new List<OsuDifficultyObject>(Math.Max(osuObjects.Length - 1, 0));

            if (osuObjects.Length == 0 || take <= 0)
                return diffObjects;

            OsuObject last = osuObjects[0];

            for (int idx = 0; idx + 1 < osuObjects.Length; idx++)
            {
                OsuObject h = osuObjects[idx + 1];

                OsuDifficultyObject? lastDiff = idx > 0 ? diffObjects[idx - 1] : null;
                OsuDifficultyObject? lastLastDiff = idx > 1 ? diffObjects[idx - 2] : null;

                var diffObject = new OsuDifficultyObject(h, last, lastDiff, lastLastDiff, clockRate, idx, scalingFactor);

                last = h;

                diffObjects.Add(diffObject);
            }

            return diffObjects;
        }

        private static void Eval(OsuDifficultyAttributes attrs, GameMods mods, Skills skills)
        {
            AimSkill aim = skills.Aim;
            AimSkill aimNoSliders = skills.AimNoSliders;
            SpeedSkill speed = skills.Speed;
            FlashlightSkill flashlight = skills.Flashlight;

            double aimDifficultyValue = aim.GetDifficultyValue();

            double aimDifficultStrainCount = aim.CountTopWeightedStrains(aimDifficultyValue);

            double difficultSliders = aim.GetDifficultSliders();

            double aimNoSlidersDifficultyValue = aimNoSliders.GetDifficultyValue();

            double aimNoSlidersTopWeightedSliderCount = OsuStrainSkill.CountTopWeightedSliders(aimNoSliders.SliderStrains, aimNoSlidersDifficultyValue);

            double aimNoSlidersDifficultStrainCount = aimNoSliders.CountTopWeightedStrains(aimNoSlidersDifficultyValue);

            double aimTopWeightedSliderFactor = aimNoSlidersTopWeightedSliderCount
                                                / RustMath.Max(aimNoSlidersDifficultStrainCount - aimNoSlidersTopWeightedSliderCount, 1.0);

            double sliderFactor = aimDifficultyValue > 0.0
                ? OsuRatingCalculator.CalculateDifficultyRating(aimNoSlidersDifficultyValue) / OsuRatingCalculator.CalculateDifficultyRating(aimDifficultyValue)
                : 1.0;

            double speedDifficultyValue = speed.GetDifficultyValue();
            double speedTopWeightedSliderCount = OsuStrainSkill.CountTopWeightedSliders(speed.SliderStrains, speedDifficultyValue);

            double speedDifficultStrainCount = speed.CountTopWeightedStrains(speedDifficultyValue);

            double speedTopWeightedSliderFactor = speedTopWeightedSliderCount / RustMath.Max(speedDifficultStrainCount - speedTopWeightedSliderCount, 1.0);

            double mechanicalDifficultyRating = CalculateMechanicalDifficultyRating(aimDifficultyValue, speedDifficultyValue);

            var osuRatingCalculator = new OsuRatingCalculator(mods, attrs.NObjects, attrs.Ar, attrs.Od, mechanicalDifficultyRating, sliderFactor);

            double aimRating = osuRatingCalculator.ComputeAimRating(aimDifficultyValue);
            double speedRating = osuRatingCalculator.ComputeSpeedRating(speedDifficultyValue);

            double flashlightRating = mods.Fl ? osuRatingCalculator.ComputeFlashlightRating(flashlight.GetDifficultyValue()) : 0.0;

            double baseAimPerformance = AimSkill.DifficultyToPerformance(aimRating);
            double baseSpeedPerformance = SpeedSkill.DifficultyToPerformance(speedRating);
            double baseFlashlightPerformance = FlashlightSkill.DifficultyToPerformance(flashlightRating);

            double basePerformance = Math.Pow(
                Math.Pow(baseAimPerformance, 1.1) + Math.Pow(baseSpeedPerformance, 1.1) + Math.Pow(baseFlashlightPerformance, 1.1),
                1.0 / 1.1);

            double starRating = CalculateStarRating(basePerformance);

            attrs.Aim = aimRating;
            attrs.AimDifficultSliderCount = difficultSliders;
            attrs.Speed = speedRating;
            attrs.Flashlight = flashlightRating;
            attrs.SliderFactor = sliderFactor;
            attrs.AimTopWeightedSliderFactor = aimTopWeightedSliderFactor;
            attrs.SpeedTopWeightedSliderFactor = speedTopWeightedSliderFactor;
            attrs.AimDifficultStrainCount = aimDifficultStrainCount;
            attrs.SpeedDifficultStrainCount = speedDifficultStrainCount;
            attrs.Stars = starRating;
            attrs.SpeedNoteCount = speed.RelevantNoteCount();
        }

        private static double CalculateMechanicalDifficultyRating(double aimDifficultyValue, double speedDifficultyValue)
        {
            double aimValue = AimSkill.DifficultyToPerformance(OsuRatingCalculator.CalculateDifficultyRating(aimDifficultyValue));
            double speedValue = SpeedSkill.DifficultyToPerformance(OsuRatingCalculator.CalculateDifficultyRating(speedDifficultyValue));

            double totalValue = Math.Pow(Math.Pow(aimValue, 1.1) + Math.Pow(speedValue, 1.1), 1.0 / 1.1);

            return CalculateStarRating(totalValue);
        }

        private static double CalculateStarRating(double basePerformance)
        {
            if (basePerformance <= 0.00001)
                return 0.0;

            return RustMath.Cbrt(OsuPerformanceCalculator.PerformanceBaseMultiplier)
                   * StarRatingMultiplier
                   * (RustMath.Cbrt(100_000.0 / Math.Pow(2.0, 1.0 / 1.1) * basePerformance) + 4.0);
        }
    }
}
