// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MosuPpRxCs;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp RX calculator: an exact, self-contained copy of the MosuPp development calculator (Realistik core +
    /// all MosuPp rules), used only when the Relax PP system is MosuPp. The "Mosu" system keeps using the Realistik
    /// calculator in Relax/Realistik; nothing here is shared with it.
    /// </summary>
    internal static class MosuPpRelaxCalculator
    {
        private static readonly ConcurrentDictionary<int, RxBeatmap> beatmaps = new ConcurrentDictionary<int, RxBeatmap>();

        /// <summary>
        /// MosuPp: speed PP / aim PP of an SS on the same beatmap + mods. The map-type decisions (aim-focused map, stream map,
        /// stream-only abuse) use these instead of the score's own values, so accuracy and misses cannot move a score across
        /// a rule threshold (a score with misses could otherwise get more PP than an SS).
        /// Cached per prepared <see cref="RxBeatmap"/> instance (a new one is created by every <see cref="Prepare"/>) and per
        /// mods / clock rate, so different beatmaps can never share an entry even when their hash is missing.
        /// </summary>
        private static readonly ConditionalWeakTable<RxBeatmap, ConcurrentDictionary<string, ReferenceRatios>> reference_ratios =
            new ConditionalWeakTable<RxBeatmap, ConcurrentDictionary<string, ReferenceRatios>>();

        // BeforeFlowGuard: ratio where the Aim-Focused Flow Guard runs; Final: after all aim/speed rules (Simple Stream Nerf, Stream-Only Guard).
        private readonly record struct ReferenceRatios(double BeforeFlowGuard, double Final);

        public static bool IsRelax(IEnumerable<Mod> mods) => RealistikRelaxBalance.IsRelax(mods);

        public static void Prepare(IBeatmap beatmap, Mod[] mods)
        {
            if (!IsRelax(mods))
                return;

            try
            {
                beatmaps[createCacheKey(beatmap.BeatmapInfo, mods)] = RealistikBeatmapConverter.Convert(beatmap);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Managed RX beatmap preparation failed for '{beatmap.BeatmapInfo.Hash}'.",
                    exception);
            }
        }

        public static OsuPerformanceAttributes CalculatePerformance(ScoreInfo score)
        {
            if (score.BeatmapInfo is null)
                throw new InvalidOperationException("Managed RX calculation requires BeatmapInfo.");
            if (!IsRelax(score.Mods))
                throw new InvalidOperationException("Managed RX calculator was called for a non-RX score.");

            int key = createCacheKey(score.BeatmapInfo, score.Mods);
            if (!beatmaps.TryGetValue(key, out RxBeatmap? beatmap))
            {
                throw new InvalidOperationException(
                    $"Managed RX beatmap was not prepared for '{score.BeatmapInfo.Hash}'. "
                    + "Run the matching difficulty calculation before performance calculation.");
            }

            try
            {
                RxScoreState scoreState = createScoreState(score);
                bool mosuPp = RelaxPpSystemSelection.Current == ForkRelaxPpSystem.MosuPp;

                // MosuPp "Traceable = Hidden (RX)": Traceable (TC) is calculated exactly like Hidden (HD) — reading bonus,
                // HD reading guard and every mod check below see HD. MosuRealistik keeps the original mods.
                Mod[] ppMods = mosuPp
                    ? score.Mods.Select(m => m.Acronym == "TC" ? new OsuModHidden() : m).ToArray()
                    : score.Mods.ToArray();

                RxMods performanceMods = getPerformanceMods(ppMods);
                double clockRate = ModUtils.CalculateRateWithMods(ppMods);
                RxNativePerformanceResult result = MosuRxCalculator.Calculate(new RxCalculationRequest
                {
                    Beatmap = beatmap,
                    Mods = performanceMods,
                    Score = scoreState,
                    EffectiveAr = beatmap.ApproachRate,
                    EffectiveOd = beatmap.OverallDifficulty,
                    EffectiveCs = beatmap.CircleSize,
                    EffectiveHp = beatmap.HpDrainRate,
                    ClockRate = clockRate,
                });

                double nativeMultiplier = calculateNativeMultiplier(result, scoreState);
                RealistikRelaxBalance.ApplyComponentGuards(
                    result,
                    ppMods,
                    scoreState,
                    beatmap,
                    (float)result.Difficulty.Cs,
                    clockRate);

                double patternMultiplier = RealistikRelaxBalance.PatternMultiplier(ppMods, result);
                double lengthAimMultiplier = RealistikRelaxBalance.LengthAimMultiplier(ppMods, result);
                result.PpAim *= lengthAimMultiplier;

                ReferenceRatios reference = mosuPp
                    ? getReferenceRatios(beatmap, performanceMods, ppMods, clockRate)
                    : default;

                if (mosuPp)
                {
                    // MosuPp "Aim-Focused Flow Guard (RX)": no flow bonus for aim-focused maps (flow comes from their spaced streams).
                    RxAimFocusedFlowGuard.Apply(result, reference.BeforeFlowGuard);

                    // MosuPp "Length Bonus (RX)" + "Spike Nerf (RX)": many hard moments are worth more than one short spike.
                    RxLengthBonus.Apply(result);

                    // MosuPp "Point Variety Nerf (RX)": 2–4 spot back-and-forth spam loses most aim/speed PP.
                    RxPointVarietyNerf.Apply(result, beatmap);

                    // MosuPp "Extreme Jump Nerf (RX)": maps with 80+ jumps faster than 4.5 px/ms (glass beach) lose up to 31% aim PP.
                    RxExtremeJumpNerf.Apply(result, beatmap);
                }

                result.Pp = RealistikRelaxBalance.RebuildTotal(result, scoreState, nativeMultiplier);

                if (RealistikRelaxBalance.IsVerticalBalanceEligible(ppMods))
                {
                    double verticalPressure = RealistikRelaxBalance.VerticalPressure(
                        beatmap,
                        (float)result.Difficulty.Cs,
                        scoreState.PassedObjects,
                        clockRate);
                    if (verticalPressure > 0)
                    {
                        result.PpAim *= 1 - 0.06 * verticalPressure;
                        result.Pp = RealistikRelaxBalance.RebuildTotal(result, scoreState, nativeMultiplier);
                    }
                }

                double odMultiplier = RealistikRelaxBalance.OdMultiplier(result.Difficulty.Od);

                // Product of the MosuPp total-PP rules below (1 for MosuRealistik).
                double mosuPpMultiplier = 1.0;

                if (mosuPp)
                {
                    // MosuPp "Stream Map Nerf (RX)": stream maps (speed PP ≥ 1.0–1.3× aim PP, ≥ 30–60% stream notes) lose up to 50%.
                    mosuPpMultiplier *= RxStreamMapNerf.Multiplier(beatmap, reference.Final);

                    // MosuPp "Stream-Only Guard (RX)": speed PP ≥ 5–10× aim PP (streams into one point): speed PP stops counting
                    // (none from 10×) and the rest keeps ≤ 10% of the PP.
                    mosuPpMultiplier *= RxStreamOnlyGuard.Apply(result, scoreState, nativeMultiplier, reference.Final);

                    // MosuPp "One-Point Map Guard (RX)": every note stacked on 1–2 spots (e.g. Iyul' [HS]) keeps ~3% of the PP.
                    mosuPpMultiplier *= RxOnePointMapGuard.Multiplier(beatmap);

                    // MosuPp "Short High CS Nerf (RX)": CS 5–6+ maps with 300–700 objects lose up to 15% (smooth in CS and length).
                    mosuPpMultiplier *= RxShortHighCsNerf.Multiplier(beatmap);

                    // MosuPp "CS PP Buff (RX)": CS 4 +5%, 5 +10%, 6 +15%, 7 +25%, 8 +50% ... (smooth, CS incl. HR/EZ/DA).
                    mosuPpMultiplier *= RxCsPpBuff.Multiplier(beatmap.CircleSize);

                    // MosuPp "Heavy Miss Penalty (RX)": every miss above 15 removes another ~10% (× e^(−0.11 · (misses − 15))).
                    mosuPpMultiplier *= RxHeavyMissPenalty.Multiplier(scoreState.Misses);
                }

                return new OsuPerformanceAttributes
                {
                    Aim = result.PpAim * odMultiplier * patternMultiplier * mosuPpMultiplier,
                    Speed = result.PpSpeed * odMultiplier * mosuPpMultiplier,
                    Accuracy = result.PpAccuracy * odMultiplier * mosuPpMultiplier,
                    Reading = result.PpReading * odMultiplier * mosuPpMultiplier,
                    Flashlight = 0,
                    EffectiveMissCount = result.EffectiveMissCount,
                    Total = result.Pp * odMultiplier * patternMultiplier * mosuPpMultiplier,
                };
            }
            catch (Exception exception) when (exception is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Managed RX PP calculation failed for '{score.BeatmapInfo.Hash}'.",
                    exception);
            }
        }

        /// <summary>
        /// Runs the same pipeline as <see cref="CalculatePerformance"/> for an SS (up to the MosuPp map-type rules) and returns
        /// its speed PP / aim PP ratios.
        /// </summary>
        private static ReferenceRatios getReferenceRatios(RxBeatmap beatmap, RxMods performanceMods, IEnumerable<Mod> mods, double clockRate)
        {
            var perBeatmap = reference_ratios.GetValue(beatmap, _ => new ConcurrentDictionary<string, ReferenceRatios>());
            string modsKey = $"{(uint)performanceMods}|{clockRate:R}|{string.Join(",", mods.Select(m => m.Acronym).OrderBy(a => a, StringComparer.Ordinal))}";
            return perBeatmap.GetOrAdd(modsKey, _ => calculateReferenceRatios(beatmap, performanceMods, mods, clockRate));
        }

        private static ReferenceRatios calculateReferenceRatios(RxBeatmap beatmap, RxMods performanceMods, IEnumerable<Mod> mods, double clockRate)
        {
            uint objects = (uint)beatmap.HitObjects.Count;
            var ss = new RxScoreState { Count300 = objects, PassedObjects = objects > 0 ? objects : null };

            RxNativePerformanceResult result = MosuRxCalculator.Calculate(new RxCalculationRequest
            {
                Beatmap = beatmap,
                Mods = performanceMods,
                Score = ss,
                EffectiveAr = beatmap.ApproachRate,
                EffectiveOd = beatmap.OverallDifficulty,
                EffectiveCs = beatmap.CircleSize,
                EffectiveHp = beatmap.HpDrainRate,
                ClockRate = clockRate,
            });

            RealistikRelaxBalance.ApplyComponentGuards(result, mods, ss, beatmap, (float)result.Difficulty.Cs, clockRate);
            result.PpAim *= RealistikRelaxBalance.LengthAimMultiplier(mods, result);

            double beforeFlowGuard = RxStreamOnlyGuard.SpeedAimRatio(result);

            RxAimFocusedFlowGuard.Apply(result, beforeFlowGuard);
            RxLengthBonus.Apply(result);
            RxPointVarietyNerf.Apply(result, beatmap);
            RxExtremeJumpNerf.Apply(result, beatmap);

            if (RealistikRelaxBalance.IsVerticalBalanceEligible(mods))
            {
                double verticalPressure = RealistikRelaxBalance.VerticalPressure(beatmap, (float)result.Difficulty.Cs, ss.PassedObjects, clockRate);
                if (verticalPressure > 0)
                    result.PpAim *= 1 - 0.06 * verticalPressure;
            }

            return new ReferenceRatios(beforeFlowGuard, RxStreamOnlyGuard.SpeedAimRatio(result));
        }

        private static RxScoreState createScoreState(ScoreInfo score)
        {
            uint count300 = (uint)Math.Max(0, score.Statistics.GetValueOrDefault(HitResult.Great));
            uint count100 = (uint)Math.Max(0, score.Statistics.GetValueOrDefault(HitResult.Ok));
            uint count50 = (uint)Math.Max(0, score.Statistics.GetValueOrDefault(HitResult.Meh));
            uint misses = (uint)Math.Max(0, score.Statistics.GetValueOrDefault(HitResult.Miss));
            uint passedObjects = count300 + count100 + count50 + misses;

            return new RxScoreState
            {
                Count300 = count300,
                Count100 = count100,
                Count50 = count50,
                Misses = misses,
                MaxCombo = score.MaxCombo > 0 ? (uint)score.MaxCombo : null,
                PassedObjects = passedObjects > 0 ? passedObjects : null,
            };
        }

        private static RxMods getPerformanceMods(IEnumerable<Mod> mods)
        {
            RxMods value = RxMods.Relax;

            foreach (Mod mod in mods)
            {
                value |= mod.Acronym switch
                {
                    "NF" => RxMods.NoFail,
                    "TD" => RxMods.TouchDevice,
                    "HD" => RxMods.Hidden,
                    "EZ" => RxMods.Easy,
                    "FL" => RxMods.Flashlight,
                    "SO" => RxMods.SpunOut,
                    _ => RxMods.None,
                };
            }

            return value;
        }

        private static double calculateNativeMultiplier(RxNativePerformanceResult result, RxScoreState score)
        {
            double componentNorm = RealistikRelaxBalance.RebuildTotal(result, score, 1);
            if (componentNorm <= 0)
                return 1.09;

            // Preserves the exact native global multiplier, including SpunOut.
            return result.Pp / componentNorm;
        }

        private static int createCacheKey(IBeatmapInfo beatmapInfo, IEnumerable<Mod> mods)
        {
            var hash = new HashCode();
            hash.Add(beatmapInfo.Hash);

            foreach (Mod mod in mods.OrderBy(m => m.Acronym))
                hash.Add(mod);

            return hash.ToHashCode();
        }
    }
}
