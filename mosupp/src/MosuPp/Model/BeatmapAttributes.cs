using System;
using MosuPp.Util;

namespace MosuPp.Model
{
    internal enum AttributeKind
    {
        /// <summary>Not set; treated as the default value (5.0).</summary>
        None,

        /// <summary>From the beatmap (or mutated default); may be overridden and adjusted by mods / clock rate.</summary>
        Value,

        /// <summary>Given by the user; not overridable by mod settings but adjusted by mods / clock rate.</summary>
        Given,

        /// <summary>Final value that is not adjusted by mods or clock rate.</summary>
        Fixed,
    }

    internal struct BeatmapAttribute
    {
        public const float Default = 5.0f;

        public AttributeKind Kind;
        public float Value;

        public BeatmapAttribute(AttributeKind kind, float value)
        {
            Kind = kind;
            Value = value;
        }

        public static BeatmapAttribute None => default;

        public BeatmapAttribute Overwrite(BeatmapAttribute other) => other.Kind == AttributeKind.None ? this : other;

        public void TryMutate(Func<float, float> f)
        {
            if (Kind == AttributeKind.None)
            {
                Kind = AttributeKind.Value;
                Value = Default;
            }

            if (Kind == AttributeKind.Value || Kind == AttributeKind.Given)
                Value = f(Value);
        }

        public void TrySet(float value)
        {
            switch (Kind)
            {
                case AttributeKind.None:
                    Kind = AttributeKind.Value;
                    Value = value;
                    break;
                case AttributeKind.Value:
                    Value = value;
                    break;
            }
        }

        public T MapOrElse<T>(Func<float, T> fixedFn, Func<float, T> f)
        {
            switch (Kind)
            {
                case AttributeKind.None:
                    return f(Default);
                case AttributeKind.Fixed:
                    return fixedFn(Value);
                default:
                    return f(Value);
            }
        }

        public float GetRaw() => Kind == AttributeKind.None ? Default : Value;
    }

    internal struct BeatmapDifficulty
    {
        public BeatmapAttribute Ar;
        public BeatmapAttribute Cs;
        public BeatmapAttribute Hp;
        public BeatmapAttribute Od;

        public void ApplyMods(GameMods mods)
        {
            // First we *set* values (osu!lazer DifficultyAdjust)
            LazerMod? da = mods.DifficultyAdjust();

            if (da != null)
            {
                if (da.GetDouble("approach_rate") is double ar) Ar.TrySet((float)ar);
                if (da.GetDouble("circle_size") is double cs) Cs.TrySet((float)cs);
                if (da.GetDouble("drain_rate") is double hp) Hp.TrySet((float)hp);
                if (da.GetDouble("overall_difficulty") is double od) Od.TrySet((float)od);
            }

            // Then we *adjust* values
            if (mods.Ez)
            {
                const float adjustRatio = 0.5f;

                Ar.TryMutate(ar => ar * adjustRatio);
                Cs.TryMutate(cs => cs * adjustRatio);
                Hp.TryMutate(hp => hp * adjustRatio);
                Od.TryMutate(od => od * adjustRatio);
            }
            else if (mods.Hr)
            {
                const float adjustRatio = 1.4f;

                Hp.TryMutate(hp => RustMath.Min(hp * adjustRatio, 10f));
                Od.TryMutate(od => RustMath.Min(od * adjustRatio, 10f));
                // * CS uses a custom 1.3 ratio.
                Cs.TryMutate(cs => RustMath.Min(cs * 1.3f, 10f));
                Ar.TryMutate(ar => RustMath.Min(ar * adjustRatio, 10f));
            }
        }
    }

    /// <summary>AR and OD hit windows (osu!standard).</summary>
    public readonly struct HitWindows
    {
        /// <summary>Hit window for approach rate i.e. <c>TimePreempt</c> in milliseconds.</summary>
        public readonly double Ar;

        /// <summary>Great (300) hit window in milliseconds.</summary>
        public readonly double OdGreat;

        /// <summary>Ok (100) hit window in milliseconds.</summary>
        public readonly double OdOk;

        /// <summary>Meh (50) hit window in milliseconds.</summary>
        public readonly double OdMeh;

        public HitWindows(double ar, double odGreat, double odOk, double odMeh)
        {
            Ar = ar;
            OdGreat = odGreat;
            OdOk = odOk;
            OdMeh = odMeh;
        }
    }

    internal readonly struct GameModeHitWindows
    {
        public readonly double Min;
        public readonly double Mid;
        public readonly double Max;

        public GameModeHitWindows(double min, double mid, double max)
        {
            Min = min;
            Mid = mid;
            Max = max;
        }

        public double DifficultyRange(double difficulty) => BeatmapAttributesExt.DifficultyRange(difficulty, Min, Mid, Max);

        public double InverseDifficultyRange(double difficultyValue) => BeatmapAttributesExt.InverseDifficultyRange(difficultyValue, Min, Mid, Max);

        public static readonly GameModeHitWindows OsuGreat = new GameModeHitWindows(80.0, 50.0, 20.0);
        public static readonly GameModeHitWindows OsuOk = new GameModeHitWindows(140.0, 100.0, 60.0);
        public static readonly GameModeHitWindows OsuMeh = new GameModeHitWindows(200.0, 150.0, 100.0);
        public static readonly GameModeHitWindows ArWindow = new GameModeHitWindows(1800.0, 1200.0, 450.0);
    }

    internal static class BeatmapAttributesExt
    {
        public static double DifficultyRange(double difficulty, double min, double mid, double max)
        {
            if (difficulty > 5.0)
                return mid + (max - mid) * DifficultyRangeValue(difficulty);

            if (difficulty < 5.0)
                return mid + (mid - min) * DifficultyRangeValue(difficulty);

            return mid;
        }

        public static double DifficultyRangeValue(double difficulty) => (difficulty - 5.0) / 5.0;

        public static double InverseDifficultyRange(double difficultyValue, double diff0, double diff5, double diff10)
        {
            if (RustMath.AlmostEq(Signum(difficultyValue - diff5), Signum(diff10 - diff5)))
                return (difficultyValue - diff5) / (diff10 - diff5) * 5.0 + 5.0;

            return (difficultyValue - diff5) / (diff5 - diff0) * 5.0 + 5.0;
        }

        /// <summary>Rust's f64::signum (±1.0 for zeros, NaN for NaN).</summary>
        private static double Signum(double x)
        {
            if (double.IsNaN(x)) return double.NaN;
            return double.IsNegative(x) ? -1.0 : 1.0;
        }

        public static double OsuGreatHitWindowToOd(double hitWindow) => (79.5 - hitWindow) / 6.0;
    }

    /// <summary>
    /// A beatmap's (osu!standard) attributes after applying mods. Clock rate is <b>not</b> considered in the values.
    /// </summary>
    public sealed class BeatmapAttributes
    {
        internal BeatmapDifficulty Difficulty;
        public double ClockRate { get; internal set; }

        internal BeatmapAttributes(BeatmapDifficulty difficulty, double clockRate)
        {
            Difficulty = difficulty;
            ClockRate = clockRate;
        }

        public float Ar
        {
            get
            {
                BeatmapAttribute ar = Difficulty.Ar;

                return ar.Kind switch
                {
                    AttributeKind.None => BeatmapAttribute.Default,
                    AttributeKind.Fixed => (float)GameModeHitWindows.ArWindow.InverseDifficultyRange(GameModeHitWindows.ArWindow.DifficultyRange(ar.Value) * ClockRate),
                    _ => ar.Value,
                };
            }
        }

        public float Od
        {
            get
            {
                BeatmapAttribute od = Difficulty.Od;

                return od.Kind switch
                {
                    AttributeKind.None => BeatmapAttribute.Default,
                    AttributeKind.Fixed => (float)GameModeHitWindows.OsuGreat.InverseDifficultyRange(GameModeHitWindows.OsuGreat.DifficultyRange(od.Value) * ClockRate),
                    _ => od.Value,
                };
            }
        }

        public float Cs => Difficulty.Cs.GetRaw();

        public float Hp => Difficulty.Hp.GetRaw();

        /// <summary>Calculate the AR and OD hit windows.</summary>
        public HitWindows GetHitWindows()
        {
            double clockRate = ClockRate;

            double ArWindow()
            {
                BeatmapAttribute a = Difficulty.Ar;
                float value;

                switch (a.Kind)
                {
                    case AttributeKind.None:
                        value = BeatmapAttribute.Default;
                        break;
                    case AttributeKind.Fixed:
                        return GameModeHitWindows.ArWindow.DifficultyRange(a.Value);
                    default:
                        value = a.Value;
                        break;
                }

                return GameModeHitWindows.ArWindow.DifficultyRange(value) / clockRate;
            }

            // See `OsuHitWindows.SetDifficulty`
            double SetDifficulty(GameModeHitWindows hitWindows)
            {
                BeatmapAttribute o = Difficulty.Od;
                float value;

                switch (o.Kind)
                {
                    case AttributeKind.None:
                        value = BeatmapAttribute.Default;
                        break;
                    case AttributeKind.Fixed:
                    {
                        //     Fixed           = f^-1(f(Value) / C)
                        // <=> f(Fixed) * C    = f(Value)
                        double fValue = hitWindows.DifficultyRange(o.Value) * clockRate;

                        return (Math.Floor(fValue) - 0.5) / clockRate;
                    }
                    default:
                        value = o.Value;
                        break;
                }

                return (Math.Floor(hitWindows.DifficultyRange(value)) - 0.5) / clockRate;
            }

            return new HitWindows(ArWindow(), SetDifficulty(GameModeHitWindows.OsuGreat), SetDifficulty(GameModeHitWindows.OsuOk),
                SetDifficulty(GameModeHitWindows.OsuMeh));
        }

        /// <summary>AR and OD after applying the clock rate.</summary>
        public (double Ar, double Od) ApplyClockRate()
        {
            double clockRate = ClockRate;

            double ar = Difficulty.Ar.MapOrElse(v => (double)v, v =>
            {
                double preempt = GameModeHitWindows.ArWindow.DifficultyRange(v);
                preempt /= clockRate;

                return GameModeHitWindows.ArWindow.InverseDifficultyRange(preempt);
            });

            double od = Difficulty.Od.MapOrElse(v => (double)v, v =>
            {
                double greatHitWindow = GameModeHitWindows.OsuGreat.DifficultyRange(v);
                greatHitWindow /= clockRate;

                return GameModeHitWindows.OsuGreat.InverseDifficultyRange(greatHitWindow);
            });

            return (ar, od);
        }

        /// <summary>Builds the attributes for an osu!standard map (port of <c>BeatmapAttributesBuilder</c>).</summary>
        internal static BeatmapAttributes Build(Beatmap? map, Difficulty? difficulty)
        {
            var diff = new BeatmapDifficulty();

            if (map != null)
            {
                // Clamping necessary to match lazer on maps like /b/4243836.
                diff.Ar = new BeatmapAttribute(AttributeKind.Value, RustMath.Clamp(map.Ar, 0f, 10f));
                diff.Od = new BeatmapAttribute(AttributeKind.Value, RustMath.Clamp(map.Od, 0f, 10f));
                diff.Cs = new BeatmapAttribute(AttributeKind.Value, map.Cs);
                diff.Hp = new BeatmapAttribute(AttributeKind.Value, map.Hp);
            }

            GameMods mods = GameMods.NoMod;
            double? clockRate = null;

            if (difficulty != null)
            {
                BeatmapDifficulty mapDiff = difficulty.MapDifficulty;

                diff.Ar = diff.Ar.Overwrite(mapDiff.Ar);
                diff.Cs = diff.Cs.Overwrite(mapDiff.Cs);
                diff.Hp = diff.Hp.Overwrite(mapDiff.Hp);
                diff.Od = diff.Od.Overwrite(mapDiff.Od);

                mods = difficulty.GetMods();
                clockRate = difficulty.GetClockRate();
            }

            diff.ApplyMods(mods);

            return new BeatmapAttributes(diff, clockRate ?? mods.ClockRate());
        }
    }
}
