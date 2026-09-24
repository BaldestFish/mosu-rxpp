using System.Collections.Generic;
using MosuPp.Model;
using MosuPp.Util;

namespace MosuPp.Osu
{
    internal enum OsuObjectKind
    {
        Circle,
        Slider,
        Spinner,
    }

    internal enum NestedSliderObjectKind
    {
        Repeat,
        Tail,
        Tick,
    }

    internal sealed class NestedSliderObject
    {
        public Pos Pos;
        public double StartTime;
        public NestedSliderObjectKind Kind;

        public bool IsRepeat => Kind == NestedSliderObjectKind.Repeat;
        public bool IsTick => Kind == NestedSliderObjectKind.Tick;
    }

    internal sealed class OsuSlider
    {
        public double EndTime;
        public double SpanCount;
        public Curve Path = null!;
        public List<NestedSliderObject> NestedObjects = null!;

        private const float BaseScoringDist = 100f;

        public static OsuSlider Create(HitObject h, SliderData slider, Beatmap map, Reflection reflection)
        {
            double startTime = h.StartTime;
            double sliderMultiplier = map.SliderMultiplier;
            double sliderTickRate = map.SliderTickRate;

            double beatLen = map.TimingPointAt(startTime)?.BeatLen ?? TimingPoint.DefaultBeatLen;

            DifficultyPoint? dp = map.DifficultyPointAt(startTime);
            double sliderVelocity = dp?.SliderVelocity ?? DifficultyPoint.DefaultSliderVelocity;
            bool generateTicks = dp?.GenerateTicks ?? DifficultyPoint.DefaultGenerateTicks;

            Curve path = CreateCurve(slider, reflection);

            double spanCount = slider.SpanCount;

            double velocity = (double)BaseScoringDist * sliderMultiplier / GetPrecisionAdjustedBeatLen(sliderVelocity, beatLen);
            double scoringDist = velocity * beatLen;

            double endTime = startTime + spanCount * path.Dist() / velocity;

            double duration = endTime - startTime;
            double spanDuration = duration / spanCount;

            double tickDistMultiplier = map.Version < 8 ? 1.0 / sliderVelocity : 1.0;

            double tickDist = generateTicks ? scoringDist / sliderTickRate * tickDistMultiplier : double.PositiveInfinity;

            List<SliderEvent> events = SliderEvents.Generate(startTime, spanDuration, velocity, tickDist, path.Dist(), slider.SpanCount);

            int SpanAt(double progress) => RustMath.ToI32(progress * spanCount);

            double ObjProgressAt(double progress)
            {
                double p = progress * spanCount % 1.0;
                return SpanAt(progress) % 2 == 1 ? 1.0 - p : p;
            }

            Pos endPathPos = path.PositionAt(ObjProgressAt(1.0));

            var nested = new List<NestedSliderObject>(events.Count);

            foreach (SliderEvent e in events)
            {
                switch (e.Kind)
                {
                    case SliderEventType.Tick:
                        nested.Add(new NestedSliderObject { Pos = path.PositionAt(e.PathProgress), StartTime = e.Time, Kind = NestedSliderObjectKind.Tick });
                        break;
                    case SliderEventType.Repeat:
                        nested.Add(new NestedSliderObject
                        {
                            Pos = path.PositionAt(e.PathProgress),
                            StartTime = startTime + (e.SpanIdx + 1) * spanDuration,
                            Kind = NestedSliderObjectKind.Repeat,
                        });
                        break;
                    case SliderEventType.Tail:
                        // no `h.pos` yet to keep order of float operations
                        nested.Add(new NestedSliderObject { Pos = endPathPos, StartTime = e.Time, Kind = NestedSliderObjectKind.Tail });
                        break;
                }
            }

            CSharpSort.Sort(nested, (a, b) => RustMath.TotalCmp(a.StartTime, b.StartTime));

            return new OsuSlider
            {
                EndTime = endTime,
                SpanCount = spanCount,
                Path = path,
                NestedObjects = nested,
            };
        }

        private static Curve CreateCurve(SliderData slider, Reflection reflection)
        {
            PathControlPoint[] points = slider.ControlPoints;

            if (reflection != Reflection.None)
            {
                points = (PathControlPoint[])points.Clone();

                for (int i = 0; i < points.Length; i++)
                {
                    Pos p = points[i].Pos;

                    points[i].Pos = reflection switch
                    {
                        Reflection.Vertical => new Pos(p.X, -p.Y),
                        Reflection.Horizontal => new Pos(-p.X, p.Y),
                        _ => new Pos(-p.X, -p.Y),
                    };
                }
            }

            return Curve.Create(true, points, slider.ExpectedDist);
        }

        internal static double GetPrecisionAdjustedBeatLen(double sliderVelocityMultiplier, double beatLen)
        {
            double sliderVelocityAsBeatLen = -100.0 / sliderVelocityMultiplier;

            double bpmMultiplier = sliderVelocityAsBeatLen < 0.0
                ? RustMath.Clamp((double)(float)-sliderVelocityAsBeatLen, 10.0, 10_000.0) / 100.0
                : 1.0;

            return beatLen * bpmMultiplier;
        }

        public int RepeatCount()
        {
            int count = 0;
            foreach (NestedSliderObject n in NestedObjects)
                if (n.Kind == NestedSliderObjectKind.Repeat) count++;
            return count;
        }

        public int TickCount()
        {
            int count = 0;
            foreach (NestedSliderObject n in NestedObjects)
                if (n.Kind == NestedSliderObjectKind.Tick) count++;
            return count;
        }

        /// <summary>Counts both ticks and repeats.</summary>
        public int LargeTickCount()
        {
            int count = 0;
            foreach (NestedSliderObject n in NestedObjects)
                if (n.Kind == NestedSliderObjectKind.Tick || n.Kind == NestedSliderObjectKind.Repeat) count++;
            return count;
        }

        /// <summary>
        /// The tail is not necessarily the last nested object, e.g. on very
        /// short and fast buzz sliders (/b/1001757).
        /// </summary>
        public NestedSliderObject? Tail()
        {
            for (int i = NestedObjects.Count - 1; i >= 0; i--)
            {
                if (NestedObjects[i].Kind == NestedSliderObjectKind.Tail)
                    return NestedObjects[i];
            }

            return null;
        }
    }

    internal sealed class OsuObject
    {
        public const float ObjectRadius = 64f;
        public const double PreemptMin = 450.0;

        public Pos Pos;
        public double StartTime;
        public int StackHeight;
        public Pos StackOffset;
        public OsuObjectKind Kind;
        public OsuSlider? Slider;
        public double SpinnerDuration;

        public static OsuObject Create(HitObject h, Beatmap map, Reflection reflection)
        {
            var obj = new OsuObject { Pos = h.Pos, StartTime = h.StartTime };

            switch (h.Kind)
            {
                case HitObjectKind.Circle:
                    obj.Kind = OsuObjectKind.Circle;
                    break;
                case HitObjectKind.Slider:
                    obj.Kind = OsuObjectKind.Slider;
                    obj.Slider = OsuSlider.Create(h, h.Slider!, map, reflection);
                    break;
                default: // spinner or hold note
                    obj.Kind = OsuObjectKind.Spinner;
                    obj.SpinnerDuration = h.Duration;
                    break;
            }

            return obj;
        }

        public void ReflectVertically()
        {
            Pos = new Pos(Pos.X, OsuConstants.PlayfieldBaseSize.Y - Pos.Y);
            FinalizeNested();
        }

        public void ReflectHorizontally()
        {
            Pos = new Pos(OsuConstants.PlayfieldBaseSize.X - Pos.X, Pos.Y);
            FinalizeNested();
        }

        public void ReflectBothAxes()
        {
            Pos = new Pos(OsuConstants.PlayfieldBaseSize.X - Pos.X, OsuConstants.PlayfieldBaseSize.Y - Pos.Y);
            FinalizeNested();
        }

        public void FinalizeNested()
        {
            if (Slider == null)
                return;

            foreach (NestedSliderObject nested in Slider.NestedObjects)
                nested.Pos = Pos + nested.Pos;
        }

        public double EndTime => Kind switch
        {
            OsuObjectKind.Circle => StartTime,
            OsuObjectKind.Slider => Slider!.EndTime,
            _ => StartTime + SpinnerDuration,
        };

        public Pos StackedPos => new Pos(Pos.X + StackOffset.X, Pos.Y + StackOffset.Y);

        public Pos EndPos => Kind == OsuObjectKind.Slider ? (Slider!.Tail()?.Pos ?? default) : Pos;

        public Pos StackedEndPos => EndPos + StackOffset;

        public bool IsCircle => Kind == OsuObjectKind.Circle;
        public bool IsSlider => Kind == OsuObjectKind.Slider;
        public bool IsSpinner => Kind == OsuObjectKind.Spinner;
    }

    internal static class OsuConstants
    {
        public static readonly Pos PlayfieldBaseSize = new Pos(512f, 384f);
    }
}
