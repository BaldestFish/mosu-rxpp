using System.Collections.Generic;
using MosuPp.Util;

namespace MosuPp.Model
{
    public struct TimingPoint
    {
        public const double DefaultBeatLen = 60_000.0 / 60.0;

        public double Time;
        public double BeatLen;

        public TimingPoint(double time, double beatLen)
        {
            Time = time;
            BeatLen = RustMath.Clamp(beatLen, 6.0, 60_000.0);
        }
    }

    public struct DifficultyPoint
    {
        public const double DefaultSliderVelocity = 1.0;
        public const double DefaultBpmMultiplier = 1.0;
        public const bool DefaultGenerateTicks = true;

        public double Time;
        public double SliderVelocity;
        public double BpmMultiplier;
        public bool GenerateTicks;

        public DifficultyPoint(double time, double beatLen, double speedMultiplier)
        {
            Time = time;
            SliderVelocity = RustMath.Clamp(speedMultiplier, 0.1, 10.0);
            BpmMultiplier = beatLen < 0.0 ? RustMath.Clamp((double)(float)(-beatLen), 10.0, 10_000.0) / 100.0 : 1.0;
            GenerateTicks = !double.IsNaN(beatLen);
        }

        public static DifficultyPoint Default => new DifficultyPoint
        {
            Time = 0.0,
            SliderVelocity = DefaultSliderVelocity,
            BpmMultiplier = DefaultBpmMultiplier,
            GenerateTicks = DefaultGenerateTicks,
        };

        internal bool IsRedundant(in DifficultyPoint existing)
            => GenerateTicks == existing.GenerateTicks && RustMath.AlmostEq(SliderVelocity, existing.SliderVelocity);
    }

    public struct EffectPoint
    {
        public const bool DefaultKiai = false;
        public const double DefaultScrollSpeed = 1.0;

        public double Time;
        public bool Kiai;
        public double ScrollSpeed;

        public EffectPoint(double time, bool kiai)
        {
            Time = time;
            Kiai = kiai;
            ScrollSpeed = DefaultScrollSpeed;
        }

        public static EffectPoint Default => new EffectPoint(0.0, DefaultKiai);

        internal bool IsRedundant(in EffectPoint existing)
            => Kiai == existing.Kiai && RustMath.AlmostEq(ScrollSpeed, existing.ScrollSpeed);
    }

    internal static class ControlPointLookup
    {
        public static int Search(List<TimingPoint> points, double time)
            => RustMath.BinarySearchBy(points, p => RustMath.TotalCmp(p.Time, time));

        public static int Search(List<DifficultyPoint> points, double time)
            => RustMath.BinarySearchBy(points, p => RustMath.TotalCmp(p.Time, time));

        public static int Search(List<EffectPoint> points, double time)
            => RustMath.BinarySearchBy(points, p => RustMath.TotalCmp(p.Time, time));

        /// <summary>Note: if <paramref name="time"/> lies before the first point, the first point is returned.</summary>
        public static TimingPoint? TimingPointAt(List<TimingPoint> points, double time)
        {
            int res = Search(points, time);
            int i = res >= 0 ? res : System.Math.Max(~res - 1, 0);

            return i < points.Count ? points[i] : (TimingPoint?)null;
        }

        public static DifficultyPoint? DifficultyPointAt(List<DifficultyPoint> points, double time)
        {
            int res = Search(points, time);
            int i = res >= 0 ? res : ~res - 1;

            return i >= 0 ? points[i] : (DifficultyPoint?)null;
        }

        public static EffectPoint? EffectPointAt(List<EffectPoint> points, double time)
        {
            int res = Search(points, time);
            int i = res >= 0 ? res : ~res - 1;

            return i >= 0 ? points[i] : (EffectPoint?)null;
        }
    }
}
