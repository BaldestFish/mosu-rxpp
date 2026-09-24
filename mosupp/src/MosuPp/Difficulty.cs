using MosuPp.Model;
using MosuPp.Osu;
using MosuPp.Util;

namespace MosuPp
{
    /// <summary>
    /// Difficulty calculation settings (port of rosu-pp's <c>Difficulty</c> builder).
    /// All setters return <c>this</c> for chaining.
    /// </summary>
    public sealed class Difficulty
    {
        private GameMods mods = GameMods.NoMod;
        private uint? passedObjects;
        private double? clockRate;
        internal BeatmapDifficulty MapDifficulty;
        private bool? lazer;

        public Difficulty Clone()
        {
            return new Difficulty
            {
                mods = mods,
                passedObjects = passedObjects,
                clockRate = clockRate,
                MapDifficulty = MapDifficulty,
                lazer = lazer,
            };
        }

        /// <summary>Specify mods.</summary>
        public Difficulty Mods(GameMods gameMods)
        {
            mods = gameMods;
            return this;
        }

        /// <summary>Specify legacy mod bits, e.g. <c>128</c> for Relax.</summary>
        public Difficulty Mods(uint legacyBits) => Mods(GameMods.FromLegacy(legacyBits));

        /// <summary>Amount of passed objects for partial plays, e.g. a fail.</summary>
        public Difficulty PassedObjects(uint passed)
        {
            passedObjects = passed;
            return this;
        }

        /// <summary>Adjust the clock rate (clamped to 0.01..=100). Defaults to the mods' clock rate.</summary>
        public Difficulty ClockRate(double rate)
        {
            clockRate = RustMath.Clamp(rate, 0.01, 100.0);
            return this;
        }

        /// <summary>
        /// Override the map's AR (clamped to -20..=20).
        /// <paramref name="fixedValue"/> = true: use the value as-is; false: it will still be adjusted by mods and clock rate.
        /// </summary>
        public Difficulty Ar(float ar, bool fixedValue)
        {
            MapDifficulty.Ar = Attr(ar, fixedValue);
            return this;
        }

        /// <summary>Override the map's CS (clamped to -20..=20).</summary>
        public Difficulty Cs(float cs, bool fixedValue)
        {
            MapDifficulty.Cs = Attr(cs, fixedValue);
            return this;
        }

        /// <summary>Override the map's HP (clamped to -20..=20).</summary>
        public Difficulty Hp(float hp, bool fixedValue)
        {
            MapDifficulty.Hp = Attr(hp, fixedValue);
            return this;
        }

        /// <summary>Override the map's OD (clamped to -20..=20).</summary>
        public Difficulty Od(float od, bool fixedValue)
        {
            MapDifficulty.Od = Attr(od, fixedValue);
            return this;
        }

        /// <summary>Whether the score was set on osu!lazer (default: true). Affects slider accuracy handling.</summary>
        public Difficulty Lazer(bool isLazer)
        {
            lazer = isLazer;
            return this;
        }

        private static BeatmapAttribute Attr(float value, bool fixedValue)
            => new BeatmapAttribute(fixedValue ? AttributeKind.Fixed : AttributeKind.Given, RustMath.Clamp(value, -20f, 20f));

        /// <summary>Calculate the osu!standard difficulty attributes (stars etc.).</summary>
        public OsuDifficultyAttributes Calculate(Beatmap map) => OsuDifficultyCalculator.Calculate(this, map);

        /// <summary>Same as <see cref="Calculate"/> but throws <see cref="TooSuspiciousException"/> for suspicious maps.</summary>
        public OsuDifficultyAttributes CheckedCalculate(Beatmap map)
        {
            map.CheckSuspicion();
            return Calculate(map);
        }

        /// <summary>Strain peaks of each skill, e.g. to plot the difficulty over time.</summary>
        public OsuStrains Strains(Beatmap map) => OsuDifficultyCalculator.Strains(this, map);

        internal GameMods GetMods() => mods;

        internal double GetClockRate() => clockRate ?? mods.ClockRate();

        /// <summary>usize::MAX in rosu-pp; <see cref="int.MaxValue"/> is plenty here.</summary>
        internal int GetPassedObjects() => passedObjects.HasValue ? (int)System.Math.Min(passedObjects.Value, int.MaxValue) : int.MaxValue;

        internal uint? GetPassedObjectsRaw() => passedObjects;

        internal bool GetLazer() => lazer ?? true;
    }
}
