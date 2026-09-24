using System;
using MosuPp.Model;
using MosuPp.Util;

namespace MosuPp.Osu
{
    /// <summary>
    /// Performance (pp) calculator for osu!standard maps (port of rosu-pp's <c>OsuPerformance</c>).
    /// All setters return <c>this</c> for chaining.
    /// </summary>
    /// <example>
    /// <code>
    /// var map = Beatmap.FromPath("map.osu");
    /// var result = new OsuPerformance(map)
    ///     .Mods(GameMods.FromAcronyms("RXHD"))
    ///     .Combo(1234)
    ///     .N100(5).Misses(1)
    ///     .Calculate();
    /// Console.WriteLine($"{result.Pp:F2}pp, {result.Stars:F2}*");
    /// </code>
    /// </example>
    public sealed class OsuPerformance
    {
        private Beatmap? map;
        private OsuDifficultyAttributes? attrs;
        private Difficulty difficulty = new Difficulty();

        private double? acc;
        private uint? combo;
        private uint? largeTickHits;
        private uint? smallTickHits;
        private uint? sliderEndHits;
        private uint? n300;
        private uint? n100;
        private uint? n50;
        private uint? misses;
        private uint? legacyTotalScore;
        private HitResultPriority hitResultPriority = Osu.HitResultPriority.BestCase;
        private HitResultGenerator hitResultGenerator = Osu.HitResultGenerator.Fast;

        /// <summary>Create a calculator for a beatmap (difficulty attributes will be calculated internally).</summary>
        public OsuPerformance(Beatmap map)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
        }

        /// <summary>
        /// Create a calculator from previously calculated difficulty attributes (much faster).
        /// Make sure they were calculated for the same map and the same mods / difficulty settings!
        /// </summary>
        public OsuPerformance(OsuDifficultyAttributes attributes)
        {
            attrs = attributes ?? throw new ArgumentNullException(nameof(attributes));
        }

        /// <summary>Specify mods.</summary>
        public OsuPerformance Mods(GameMods mods)
        {
            difficulty.Mods(mods);
            return this;
        }

        /// <summary>Specify legacy mod bits, e.g. <c>128</c> for Relax.</summary>
        public OsuPerformance Mods(uint legacyBits)
        {
            difficulty.Mods(legacyBits);
            return this;
        }

        /// <summary>Use the settings of the given <see cref="MosuPp.Difficulty"/> (mods, clock rate, AR/OD/CS/HP overrides, ...).</summary>
        public OsuPerformance Difficulty(Difficulty settings)
        {
            difficulty = settings.Clone();
            return this;
        }

        /// <summary>The max combo of the play.</summary>
        public OsuPerformance Combo(uint value)
        {
            combo = value;
            return this;
        }

        /// <summary>Accuracy between 0 and 100; used to generate matching hit results.</summary>
        public OsuPerformance Accuracy(double accuracy)
        {
            acc = RustMath.Clamp(accuracy, 0.0, 100.0) / 100.0;
            return this;
        }

        public OsuPerformance N300(uint value)
        {
            n300 = value;
            return this;
        }

        public OsuPerformance N100(uint value)
        {
            n100 = value;
            return this;
        }

        public OsuPerformance N50(uint value)
        {
            n50 = value;
            return this;
        }

        public OsuPerformance Misses(uint value)
        {
            misses = value;
            return this;
        }

        /// <summary>
        /// Amount of "large tick" hits (lazer only). With slider accuracy: hit slider ticks and repeats;
        /// without slider accuracy (Classic): hit slider heads, ticks and repeats.
        /// </summary>
        public OsuPerformance LargeTickHits(uint value)
        {
            largeTickHits = value;
            return this;
        }

        /// <summary>Amount of "small tick" hits: slider tail hits for lazer scores without slider accuracy.</summary>
        public OsuPerformance SmallTickHits(uint value)
        {
            smallTickHits = value;
            return this;
        }

        /// <summary>Amount of hit slider ends (lazer scores with slider accuracy).</summary>
        public OsuPerformance SliderEndHits(uint value)
        {
            sliderEndHits = value;
            return this;
        }

        /// <summary>Legacy (score v1) total score; improves the miss estimation of classic scores.</summary>
        public OsuPerformance LegacyTotalScore(uint value)
        {
            legacyTotalScore = value;
            return this;
        }

        /// <summary>Whether the score was set on osu!lazer (default true). Stable scores ignore slider heads for accuracy.</summary>
        public OsuPerformance Lazer(bool isLazer)
        {
            difficulty.Lazer(isLazer);
            return this;
        }

        /// <summary>Amount of passed objects for partial plays, e.g. a fail.</summary>
        public OsuPerformance PassedObjects(uint value)
        {
            difficulty.PassedObjects(value);
            return this;
        }

        /// <summary>Custom clock rate (0.01..=100). Defaults to the mods' clock rate.</summary>
        public OsuPerformance ClockRate(double rate)
        {
            difficulty.ClockRate(rate);
            return this;
        }

        public OsuPerformance Ar(float value, bool fixedValue)
        {
            difficulty.Ar(value, fixedValue);
            return this;
        }

        public OsuPerformance Cs(float value, bool fixedValue)
        {
            difficulty.Cs(value, fixedValue);
            return this;
        }

        public OsuPerformance Hp(float value, bool fixedValue)
        {
            difficulty.Hp(value, fixedValue);
            return this;
        }

        public OsuPerformance Od(float value, bool fixedValue)
        {
            difficulty.Od(value, fixedValue);
            return this;
        }

        /// <summary>BestCase sacrifices 300s and 100s to reduce 50s, WorstCase does the opposite.</summary>
        public OsuPerformance HitResultPriority(HitResultPriority priority)
        {
            hitResultPriority = priority;
            return this;
        }

        public OsuPerformance HitResultGenerator(HitResultGenerator generator)
        {
            hitResultGenerator = generator;
            return this;
        }

        /// <summary>Provide all hit results at once.</summary>
        public OsuPerformance HitResults(OsuHitResults hitResults)
        {
            largeTickHits = hitResults.LargeTickHits;
            smallTickHits = hitResults.SmallTickHits;
            sliderEndHits = hitResults.SliderEndHits;
            n300 = hitResults.N300;
            n100 = hitResults.N100;
            n50 = hitResults.N50;
            misses = hitResults.Misses;
            return this;
        }

        /// <summary>Provide combo, hit results and legacy total score at once.</summary>
        public OsuPerformance State(OsuScoreState state)
        {
            combo = state.MaxCombo;
            legacyTotalScore = state.LegacyTotalScore;
            return HitResults(state.HitResults);
        }

        /// <summary>Difficulty attributes for the current settings (calculated and cached if a map was given).</summary>
        public OsuDifficultyAttributes GetDifficultyAttributes(bool checkSuspicion = false)
        {
            if (attrs == null)
            {
                if (checkSuspicion)
                    map!.CheckSuspicion();

                attrs = difficulty.Calculate(map!);
                map = null;
            }

            return attrs;
        }

        /// <summary>Create the score state (hit results and combo) that will be used for the calculation.</summary>
        public OsuScoreState GenerateState(bool checkSuspicion = false)
        {
            OsuDifficultyAttributes a = GetDifficultyAttributes(checkSuspicion);

            var inspect = new InspectOsuPerformance
            {
                Attrs = a,
                Difficulty = difficulty,
                Acc = acc,
                Combo = combo,
                LargeTickHits = largeTickHits,
                SmallTickHits = smallTickHits,
                SliderEndHits = sliderEndHits,
                N300 = n300,
                N100 = n100,
                N50 = n50,
                Misses = misses,
                HitResultPriority = hitResultPriority,
            };

            uint totalHits = inspect.TotalHits();
            uint missCount = inspect.GetMisses();

            OsuHitResults hitResults = OsuHitResultGenerators.Generate(hitResultGenerator, inspect);

            uint remain = RustMath.SaturatingSub(totalHits, hitResults.TotalHits);

            unchecked
            {
                if (hitResultPriority == Osu.HitResultPriority.BestCase)
                {
                    if (!n300.HasValue) hitResults.N300 += remain;
                    else if (!n100.HasValue) hitResults.N100 += remain;
                    else if (!n50.HasValue) hitResults.N50 += remain;
                    else hitResults.N300 += remain;
                }
                else
                {
                    if (!n50.HasValue) hitResults.N50 += remain;
                    else if (!n100.HasValue) hitResults.N100 += remain;
                    else if (!n300.HasValue) hitResults.N300 += remain;
                    else hitResults.N50 += remain;
                }
            }

            uint maxPossibleCombo = RustMath.SaturatingSub(a.MaxCombo, missCount);
            uint maxCombo = combo.HasValue ? Math.Min(combo.Value, maxPossibleCombo) : maxPossibleCombo;

            combo = maxCombo;
            sliderEndHits = hitResults.SliderEndHits;
            largeTickHits = hitResults.LargeTickHits;
            smallTickHits = hitResults.SmallTickHits;
            n300 = hitResults.N300;
            n100 = hitResults.N100;
            n50 = hitResults.N50;
            misses = hitResults.Misses;

            return new OsuScoreState
            {
                MaxCombo = maxCombo,
                HitResults = hitResults,
                LegacyTotalScore = legacyTotalScore,
            };
        }

        /// <summary>Calculate all performance related values, including pp and stars.</summary>
        public OsuPerformanceAttributes Calculate() => CalculateInner(false);

        /// <summary>Same as <see cref="Calculate"/> but throws <see cref="TooSuspiciousException"/> for suspicious maps.</summary>
        public OsuPerformanceAttributes CheckedCalculate() => CalculateInner(true);

        private OsuPerformanceAttributes CalculateInner(bool checkSuspicion)
        {
            OsuScoreState state = GenerateState(checkSuspicion);
            OsuDifficultyAttributes a = attrs!;

            GameMods mods = difficulty.GetMods();
            bool lazer = difficulty.GetLazer();
            bool usingClassicSliderAcc = mods.NoSliderHeadAcc(lazer);

            OsuScoreOrigin origin;

            if (!lazer)
                origin = OsuScoreOrigin.Stable;
            else if (!usingClassicSliderAcc)
                origin = OsuScoreOrigin.WithSliderAcc(a.NLargeTicks, a.NSliders);
            else
                origin = OsuScoreOrigin.WithoutSliderAcc(unchecked(a.NSliders + a.NLargeTicks), a.NSliders);

            double accuracy = state.HitResults.Accuracy(origin);

            return new OsuPerformanceCalculator(a, mods, accuracy, state, usingClassicSliderAcc).Calculate();
        }
    }
}
