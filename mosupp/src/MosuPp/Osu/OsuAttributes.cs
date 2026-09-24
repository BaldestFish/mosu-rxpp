using System.Collections.Generic;
using MosuPp.Model;

namespace MosuPp.Osu
{
    /// <summary>The result of a difficulty calculation on an osu!standard map.</summary>
    public sealed class OsuDifficultyAttributes
    {
        /// <summary>The difficulty of the aim skill.</summary>
        public double Aim;

        /// <summary>The number of sliders weighted by difficulty.</summary>
        public double AimDifficultSliderCount;

        /// <summary>The difficulty of the speed skill.</summary>
        public double Speed;

        /// <summary>The difficulty of the flashlight skill.</summary>
        public double Flashlight;

        /// <summary>The ratio of the aim strain with and without considering sliders.</summary>
        public double SliderFactor;

        /// <summary>Describes how much of aim's difficult strain count is contributed to by sliders.</summary>
        public double AimTopWeightedSliderFactor;

        /// <summary>Describes how much of speed's difficult strain count is contributed to by sliders.</summary>
        public double SpeedTopWeightedSliderFactor;

        /// <summary>The number of clickable objects weighted by difficulty.</summary>
        public double SpeedNoteCount;

        /// <summary>Weighted sum of aim strains.</summary>
        public double AimDifficultStrainCount;

        /// <summary>Weighted sum of speed strains.</summary>
        public double SpeedDifficultStrainCount;

        /// <summary>The amount of nested score per object.</summary>
        public double NestedScorePerObject;

        /// <summary>The legacy score base multiplier.</summary>
        public double LegacyScoreBaseMultiplier;

        /// <summary>The maximum legacy combo score.</summary>
        public double MaximumLegacyComboScore;

        /// <summary>The approach rate (clock rate applied).</summary>
        public double Ar;

        /// <summary>The great hit window.</summary>
        public double GreatHitWindow;

        /// <summary>The ok hit window.</summary>
        public double OkHitWindow;

        /// <summary>The meh hit window.</summary>
        public double MehHitWindow;

        /// <summary>The health drain rate.</summary>
        public double Hp;

        /// <summary>The amount of circles.</summary>
        public uint NCircles;

        /// <summary>The amount of sliders.</summary>
        public uint NSliders;

        /// <summary>
        /// The amount of "large ticks". The meaning depends on the kind of score:
        /// stable: irrelevant; lazer with slider accuracy: slider ticks and repeats;
        /// lazer without slider accuracy: slider heads, ticks and repeats.
        /// </summary>
        public uint NLargeTicks;

        /// <summary>The amount of spinners.</summary>
        public uint NSpinners;

        /// <summary>The final star rating.</summary>
        public double Stars;

        /// <summary>The maximum combo.</summary>
        public uint MaxCombo;

        /// <summary>The amount of hit objects.</summary>
        public uint NObjects => NCircles + NSliders + NSpinners;

        /// <summary>The overall difficulty (clock rate applied), derived from the great hit window.</summary>
        public double Od => BeatmapAttributesExt.OsuGreatHitWindowToOd(GreatHitWindow);

        public OsuDifficultyAttributes Clone() => (OsuDifficultyAttributes)MemberwiseClone();

        /// <summary>Returns a builder for performance calculation re-using these attributes.</summary>
        public OsuPerformance Performance() => new OsuPerformance(this);
    }

    /// <summary>The result of a performance calculation on an osu!standard map.</summary>
    public sealed class OsuPerformanceAttributes
    {
        /// <summary>The difficulty attributes that were used for the performance calculation.</summary>
        public OsuDifficultyAttributes Difficulty = new OsuDifficultyAttributes();

        /// <summary>The final performance points.</summary>
        public double Pp;

        /// <summary>The accuracy portion of the final pp.</summary>
        public double PpAcc;

        /// <summary>The aim portion of the final pp.</summary>
        public double PpAim;

        /// <summary>The flashlight portion of the final pp.</summary>
        public double PpFlashlight;

        /// <summary>The speed portion of the final pp.</summary>
        public double PpSpeed;

        /// <summary>Misses including an approximated amount of slider breaks.</summary>
        public double EffectiveMissCount;

        /// <summary>Approximated unstable-rate.</summary>
        public double? SpeedDeviation;

        public double ComboBasedEstimatedMissCount;
        public double? ScoreBasedEstimatedMissCount;
        public double AimEstimatedSliderBreaks;
        public double SpeedEstimatedSliderBreaks;

        /// <summary>The hit results that were used (useful when generated from an accuracy).</summary>
        public OsuScoreState State = new OsuScoreState();

        public double Stars => Difficulty.Stars;
        public uint MaxCombo => Difficulty.MaxCombo;
        public uint NObjects => Difficulty.NObjects;

        /// <summary>Returns a builder for performance calculation re-using the difficulty attributes.</summary>
        public OsuPerformance Performance() => new OsuPerformance(Difficulty);
    }

    /// <summary>Strain peaks of each skill; suitable to plot the difficulty of a map over time.</summary>
    public sealed class OsuStrains
    {
        /// <summary>Time between two strains in ms.</summary>
        public const double SectionLen = 400.0;

        public List<double> Aim = new List<double>();
        public List<double> AimNoSliders = new List<double>();
        public List<double> Speed = new List<double>();
        public List<double> Flashlight = new List<double>();
    }
}
