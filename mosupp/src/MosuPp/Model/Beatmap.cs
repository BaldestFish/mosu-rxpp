using System;
using System.Collections.Generic;
using System.IO;

namespace MosuPp.Model
{
    public enum GameMode
    {
        Osu = 0,
        Taiko = 1,
        Catch = 2,
        Mania = 3,
    }

    /// <summary>All beatmap data that is relevant for difficulty and performance calculation.</summary>
    public sealed class Beatmap
    {
        public const int LatestFormatVersion = 14;
        internal const float DefaultStackLeniency = 0.7f;

        public int Version = LatestFormatVersion;

        // General
        public float StackLeniency = DefaultStackLeniency;
        public GameMode Mode = GameMode.Osu;

        // Difficulty
        public float Ar = 5f;
        public float Cs = 5f;
        public float Hp = 5f;
        public float Od = 5f;
        public double SliderMultiplier = 1.4;
        public double SliderTickRate = 1.0;

        // Events
        public List<BreakPeriod> Breaks = new List<BreakPeriod>();

        // TimingPoints
        public List<TimingPoint> TimingPoints = new List<TimingPoint>();
        public List<DifficultyPoint> DifficultyPoints = new List<DifficultyPoint>();
        public List<EffectPoint> EffectPoints = new List<EffectPoint>();

        // HitObjects
        public List<HitObject> HitObjects = new List<HitObject>();
        public List<byte> HitSounds = new List<byte>();

        /// <summary>Parse a beatmap from a <c>.osu</c> file path.</summary>
        public static Beatmap FromPath(string path) => FromBytes(File.ReadAllBytes(path));

        /// <summary>Parse a beatmap from the raw bytes of a <c>.osu</c> file.</summary>
        public static Beatmap FromBytes(byte[] bytes) => BeatmapDecoder.Decode(bytes);

        /// <summary>Parse a beatmap from a stream containing a <c>.osu</c> file.</summary>
        public static Beatmap FromStream(Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return FromBytes(ms.ToArray());
        }

        /// <summary>Parse a beatmap from the content of a <c>.osu</c> file.</summary>
        public static Beatmap FromString(string content) => BeatmapDecoder.DecodeString(content);

        /// <summary>Returns the attributes (AR/OD/CS/HP, hit windows) after applying the given settings.</summary>
        public BeatmapAttributes Attributes(MosuPp.Difficulty? difficulty = null) => BeatmapAttributes.Build(this, difficulty);

        internal TimingPoint? TimingPointAt(double time) => ControlPointLookup.TimingPointAt(TimingPoints, time);

        internal DifficultyPoint? DifficultyPointAt(double time) => ControlPointLookup.DifficultyPointAt(DifficultyPoints, time);

        /// <summary>Sum up the duration of all breaks (in milliseconds).</summary>
        public double TotalBreakTime()
        {
            double sum = 0;
            foreach (BreakPeriod b in Breaks) sum += b.Duration;
            return sum;
        }

        /// <summary>
        /// Checks whether hit objects appear too suspicious for further calculation
        /// (e.g. maps built to stress-test osu!). Throws <see cref="TooSuspiciousException"/> if so.
        /// </summary>
        public void CheckSuspicion()
        {
            TooSuspiciousReason? reason = TooSuspicious.Check(this);

            if (reason.HasValue)
                throw new TooSuspiciousException(reason.Value);
        }

        /// <summary>Returns the suspicion reason or <c>null</c> if the map is fine.</summary>
        public TooSuspiciousReason? GetSuspicion() => TooSuspicious.Check(this);
    }
}
