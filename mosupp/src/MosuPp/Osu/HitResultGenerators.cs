using System;
using MosuPp.Util;

namespace MosuPp.Osu
{
    /// <summary>Which hit results to prefer when they have to be generated.</summary>
    public enum HitResultPriority
    {
        /// <summary>Prioritize good hit results over bad ones.</summary>
        BestCase,

        /// <summary>Prioritize bad hit results over good ones.</summary>
        WorstCase,
    }

    /// <summary>How hit results are generated from an accuracy.</summary>
    public enum HitResultGenerator
    {
        /// <summary>Generated as fast as possible (default).</summary>
        Fast,

        /// <summary>Guaranteed to be the closest to the target accuracy.</summary>
        Closest,

        /// <summary>Ignores accuracy; generated solely based on <see cref="HitResultPriority"/>.</summary>
        IgnoreAccuracy,
    }

    /// <summary>Port of rosu-pp's <c>InspectOsuPerformance</c>.</summary>
    internal sealed class InspectOsuPerformance
    {
        public OsuDifficultyAttributes Attrs = null!;
        public Difficulty Difficulty = null!;
        public double? Acc;
        public uint? Combo;
        public uint? LargeTickHits;
        public uint? SmallTickHits;
        public uint? SliderEndHits;
        public uint? N300;
        public uint? N100;
        public uint? N50;
        public uint? Misses;
        public HitResultPriority HitResultPriority;

        public uint TotalHits()
        {
            // `passed_objects as u32` where the default is usize::MAX (-> u32::MAX)
            uint passed = Difficulty.GetPassedObjectsRaw() ?? uint.MaxValue;
            return Math.Min(passed, Attrs.NObjects);
        }

        public uint GetMisses() => Misses.HasValue ? Math.Min(Misses.Value, TotalHits()) : 0;

        public bool Lazer => Difficulty.GetLazer();

        public bool UsingClassicSliderAcc => Difficulty.GetMods().NoSliderHeadAcc(Lazer);

        public OsuScoreOrigin Origin()
        {
            if (!Lazer)
                return OsuScoreOrigin.Stable;

            if (!UsingClassicSliderAcc)
                return OsuScoreOrigin.WithSliderAcc(Attrs.NLargeTicks, Attrs.NSliders);

            return OsuScoreOrigin.WithoutSliderAcc(Attrs.NSliders + Attrs.NLargeTicks, Attrs.NSliders);
        }

        /// <summary>Returns (slider end hits, large tick hits, small tick hits).</summary>
        public (uint SliderEndHits, uint LargeTickHits, uint SmallTickHits) TickHits()
        {
            if (!Lazer)
                return (0, 0, 0);

            if (!UsingClassicSliderAcc)
            {
                uint sliderEndHits = SliderEndHits.HasValue ? Math.Min(SliderEndHits.Value, Attrs.NSliders) : Attrs.NSliders;
                uint largeTickHits = LargeTickHits.HasValue ? Math.Min(LargeTickHits.Value, Attrs.NLargeTicks) : Attrs.NLargeTicks;

                return (sliderEndHits, largeTickHits, 0);
            }

            uint smallTickHits = SmallTickHits.HasValue ? Math.Min(SmallTickHits.Value, Attrs.NSliders) : Attrs.NSliders;
            uint maxLarge = Attrs.NSliders + Attrs.NLargeTicks;
            uint large = LargeTickHits.HasValue ? Math.Min(LargeTickHits.Value, maxLarge) : maxLarge;

            return (0, large, smallTickHits);
        }
    }

    internal static class OsuHitResultGenerators
    {
        public static OsuHitResults Generate(HitResultGenerator generator, InspectOsuPerformance inspect)
        {
            return generator switch
            {
                HitResultGenerator.Closest => Closest(inspect),
                HitResultGenerator.IgnoreAccuracy => IgnoreAccuracy(inspect),
                _ => Fast(inspect),
            };
        }

        public static OsuHitResults Fast(InspectOsuPerformance inspect)
        {
            if (inspect.Acc is not double acc)
                return IgnoreAccuracy(inspect);

            (uint sliderEndHits, uint largeTickHits, uint smallTickHits) = inspect.TickHits();

            uint totalHits = inspect.TotalHits();
            uint misses = inspect.GetMisses();
            uint remain = totalHits - misses;
            OsuScoreOrigin origin = inspect.Origin();

            if (remain == 0)
            {
                return new OsuHitResults
                {
                    LargeTickHits = largeTickHits,
                    SmallTickHits = smallTickHits,
                    SliderEndHits = sliderEndHits,
                    Misses = misses,
                };
            }

            (uint tickScore, uint tickMax) = origin.TickScores(largeTickHits, smallTickHits, sliderEndHits);

            uint prelim300 = inspect.N300.HasValue ? Math.Min(inspect.N300.Value, remain) : 0;
            uint prelim100 = inspect.N100.HasValue ? Math.Min(inspect.N100.Value, remain - prelim300) : 0;
            uint prelim50 = inspect.N50.HasValue ? Math.Min(inspect.N50.Value, remain - prelim300 - prelim100) : 0;

            uint n300, n100, n50;

            bool has300 = inspect.N300.HasValue, has100 = inspect.N100.HasValue, has50 = inspect.N50.HasValue;

            unchecked
            {
                if (has300 && has100 && has50)
                {
                    (n300, n100, n50) = (prelim300, prelim100, prelim50);
                }
                else if (has300 && has100)
                {
                    (n300, n100, n50) = (prelim300, prelim100, remain - prelim300 - prelim100);
                }
                else if (has300 && has50)
                {
                    (n300, n100, n50) = (prelim300, remain - prelim300 - prelim50, prelim50);
                }
                else if (has100 && has50)
                {
                    (n300, n100, n50) = (remain - prelim100 - prelim50, prelim100, prelim50);
                }
                else
                {
                    // acc = (300*n300 + 100*n100 + 50*n50 + tick_score) / (300*total_hits + tick_max)
                    // Simplify by dividing by 50: (reducing risk of overflow)
                    // acc = (6*n300 + 2*n100 + n50 + tick_score/50) / (6*total_hits + tick_max/50)

                    double numerator = (double)(6 * prelim300 + 2 * prelim100 + prelim50) + tickScore / 50.0;

                    double denominator = (double)(6 * totalHits) + tickMax / 50.0;

                    uint targetTotal = RustMath.ToU32(RustMath.RoundTiesEven(RustMath.Max(acc * denominator - numerator, 0.0)));

                    // Start by assuming every non-miss is an n50
                    // delta is how much we need to increase from the baseline (all n50s)
                    uint baseline = remain - prelim300 - prelim100 - prelim50;
                    uint delta = RustMath.SaturatingSub(targetTotal, baseline);

                    // Each n300 increases by 5 (6-1), each n100 increases by 1 (2-1)
                    // delta = 5*n300 + 1*n100

                    n300 = Math.Min(remain - prelim100 - prelim50, inspect.N300 ?? delta / 5);

                    if (!has300)
                        delta = RustMath.SaturatingSub(delta, 5 * n300);

                    n100 = Math.Min(remain - n300 - prelim50, inspect.N100 ?? delta);
                    n50 = Math.Min(remain - n300 - n100, inspect.N50 ?? remain);
                }
            }

            var hitResults = new OsuHitResults
            {
                LargeTickHits = largeTickHits,
                SmallTickHits = smallTickHits,
                SliderEndHits = sliderEndHits,
                N300 = n300,
                N100 = n100,
                N50 = n50,
                Misses = misses,
            };

            if (hitResults.TotalHits < totalHits)
            {
                uint left = totalHits - hitResults.TotalHits;

                if (inspect.HitResultPriority == HitResultPriority.BestCase)
                {
                    if (!has300) hitResults.N300 += left;
                    else if (!has100) hitResults.N100 += left;
                    else hitResults.N50 += left;
                }
                else
                {
                    if (!has50) hitResults.N50 += left;
                    else if (!has100) hitResults.N100 += left;
                    else hitResults.N300 += left;
                }
            }

            return hitResults;
        }

        public static OsuHitResults IgnoreAccuracy(InspectOsuPerformance inspect)
        {
            (uint sliderEndHits, uint largeTickHits, uint smallTickHits) = inspect.TickHits();

            uint totalHits = inspect.TotalHits();
            uint misses = inspect.GetMisses();
            uint remain = totalHits - misses;

            uint? AssignSpecified(uint? specified)
            {
                if (!specified.HasValue)
                    return null;

                uint assigned = Math.Min(specified.Value, remain);
                remain -= assigned;

                return assigned;
            }

            uint TakeRemain()
            {
                uint r = remain;
                remain = 0;
                return r;
            }

            uint n300, n100, n50;

            if (inspect.HitResultPriority == HitResultPriority.BestCase)
            {
                // First pass: assign specified values in priority order
                uint? s300 = AssignSpecified(inspect.N300);
                uint? s100 = AssignSpecified(inspect.N100);
                uint? s50 = AssignSpecified(inspect.N50);

                // Second pass: fill first unspecified with remainder
                n300 = s300 ?? TakeRemain();
                n100 = s100 ?? TakeRemain();
                n50 = s50 ?? TakeRemain();

                if (remain > 0)
                    n300 += remain;
            }
            else
            {
                // First pass: assign specified values in priority order (worst to best)
                uint? s50 = AssignSpecified(inspect.N50);
                uint? s100 = AssignSpecified(inspect.N100);
                uint? s300 = AssignSpecified(inspect.N300);

                // Second pass: fill first unspecified with remainder
                n50 = s50 ?? TakeRemain();
                n100 = s100 ?? TakeRemain();
                n300 = s300 ?? TakeRemain();

                if (remain > 0)
                    n50 += remain;
            }

            return new OsuHitResults
            {
                LargeTickHits = largeTickHits,
                SmallTickHits = smallTickHits,
                SliderEndHits = sliderEndHits,
                N300 = n300,
                N100 = n100,
                N50 = n50,
                Misses = misses,
            };
        }

        public static OsuHitResults Closest(InspectOsuPerformance inspect)
        {
            if (inspect.Acc is not double acc)
                return IgnoreAccuracy(inspect);

            (uint sliderEndHits, uint largeTickHits, uint smallTickHits) = inspect.TickHits();

            uint totalHits = inspect.TotalHits();
            uint misses = inspect.GetMisses();
            uint remain = totalHits - misses;
            OsuScoreOrigin origin = inspect.Origin();

            (uint tickScore, uint tickMax) = origin.TickScores(largeTickHits, smallTickHits, sliderEndHits);

            double targetTotal = acc * (double)unchecked(300 * totalHits + tickMax);

            OsuHitResults Make(uint a, uint b, uint c) => new OsuHitResults
            {
                LargeTickHits = largeTickHits,
                SmallTickHits = smallTickHits,
                SliderEndHits = sliderEndHits,
                N300 = a,
                N100 = b,
                N50 = c,
                Misses = misses,
            };

            (uint, uint, uint) ComputeN100N50(uint n300)
            {
                n300 = Math.Min(n300, remain);

                double raw100 = (targetTotal - (double)unchecked(50 * remain + 250 * n300 + tickScore)) / 50.0;

                uint rem = remain - n300;
                uint min100 = Math.Min(rem, RustMath.ToU32(Math.Floor(raw100)));
                uint max100 = Math.Min(rem, RustMath.ToU32(Math.Ceiling(raw100)));

                double bestDist = double.MaxValue;
                uint n100 = 0;
                uint n50 = rem;

                for (ulong new100 = min100; new100 <= max100; new100++)
                {
                    uint new50 = rem - (uint)new100;
                    double dist = Math.Abs(acc - Make(n300, (uint)new100, new50).Accuracy(origin));

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        n100 = (uint)new100;
                        n50 = new50;
                    }
                }

                return (n300, n100, n50);
            }

            (uint, uint, uint) ComputeN300N50(uint n100)
            {
                n100 = Math.Min(n100, remain);

                double raw300 = (targetTotal - (double)unchecked(50 * remain + 50 * n100 + tickScore)) / 250.0;

                uint rem = remain - n100;
                uint min300 = Math.Min(rem, RustMath.ToU32(Math.Floor(raw300)));
                uint max300 = Math.Min(rem, RustMath.ToU32(Math.Ceiling(raw300)));

                double bestDist = double.MaxValue;
                uint n300 = 0;
                uint n50 = rem;

                for (ulong new300 = min300; new300 <= max300; new300++)
                {
                    uint new50 = rem - (uint)new300;
                    double dist = Math.Abs(acc - Make((uint)new300, n100, new50).Accuracy(origin));

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        n300 = (uint)new300;
                        n50 = new50;
                    }
                }

                return (n300, n100, n50);
            }

            (uint, uint, uint) ComputeN300N100(uint n50)
            {
                n50 = Math.Min(n50, remain);

                double raw300 = (targetTotal + (double)unchecked(50 * n50) - (double)unchecked(100 * remain + tickScore)) / 200.0;

                uint rem = remain - n50;
                uint min300 = Math.Min(rem, RustMath.ToU32(Math.Floor(raw300)));
                uint max300 = Math.Min(rem, RustMath.ToU32(Math.Ceiling(raw300)));

                double bestDist = double.MaxValue;
                uint n300 = 0;
                uint n100 = rem;

                for (ulong new300 = min300; new300 <= max300; new300++)
                {
                    uint new100 = rem - (uint)new300;
                    double dist = Math.Abs(acc - Make((uint)new300, new100, n50).Accuracy(origin));

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        n300 = (uint)new300;
                        n100 = new100;
                    }
                }

                return (n300, n100, n50);
            }

            uint r300, r100, r50;
            uint? i300 = inspect.N300, i100 = inspect.N100, i50 = inspect.N50;

            if (i300.HasValue && i100.HasValue && i50.HasValue)
            {
                r300 = Math.Min(i300.Value, remain);
                r100 = Math.Min(i100.Value, remain - r300);
                r50 = Math.Min(i50.Value, remain - r300 - r100);
            }
            else if (i300.HasValue && i100.HasValue)
            {
                r300 = Math.Min(i300.Value, remain);
                r100 = Math.Min(i100.Value, remain - r300);
                r50 = remain - r300 - r100;
            }
            else if (i300.HasValue && i50.HasValue)
            {
                r300 = Math.Min(i300.Value, remain);
                r50 = Math.Min(i50.Value, remain - r300);
                r100 = remain - r300 - r50;
            }
            else if (i100.HasValue && i50.HasValue)
            {
                r100 = Math.Min(i100.Value, remain);
                r50 = Math.Min(i50.Value, remain - r100);
                r300 = remain - r100 - r50;
            }
            else if (i300.HasValue)
            {
                (r300, r100, r50) = ComputeN100N50(i300.Value);
            }
            else if (i100.HasValue)
            {
                (r300, r100, r50) = ComputeN300N50(i100.Value);
            }
            else if (i50.HasValue)
            {
                (r300, r100, r50) = ComputeN300N100(i50.Value);
            }
            else
            {
                // Deriving bounds on n300:
                // - Lower bound: minimize n300 by maximizing n50 (n100 = 0)
                double rawMin300 = (targetTotal - (double)unchecked(50 * remain + tickScore)) / 250.0;

                // - Upper bound: maximize n300 by minimizing n100 and n50 (both = 0)
                double rawMax300 = (targetTotal - tickScore) / 300.0;

                uint min300 = RustMath.ToU32(RustMath.Max(Math.Floor(rawMin300), 0.0));
                // 1+ to account for potential floating point inaccuracies
                uint max300 = Math.Min(remain, unchecked(1 + RustMath.ToU32(Math.Ceiling(rawMax300))));

                double bestDist = double.MaxValue;
                r300 = 0;
                r100 = 0;
                r50 = remain;

                for (ulong new300 = min300; new300 <= max300; new300++)
                {
                    (uint a, uint b, uint c) = ComputeN100N50((uint)new300);

                    double dist = Math.Abs(acc - Make(a, b, c).Accuracy(origin));

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        r300 = a;
                        r100 = b;
                        r50 = c;
                    }
                }
            }

            return Make(r300, r100, r50);
        }
    }
}
