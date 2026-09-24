using System.Collections.Generic;

namespace MosuPp.Model
{
    public enum SplineType
    {
        Catmull,
        BSpline,
        Linear,
        PerfectCurve,
    }

    /// <summary>The type of a slider path segment.</summary>
    public readonly struct PathType : System.IEquatable<PathType>
    {
        public readonly SplineType Kind;

        /// <summary>B-spline degree; <c>null</c> for plain bezier and all other kinds.</summary>
        public readonly int? Degree;

        public PathType(SplineType kind, int? degree = null)
        {
            Kind = kind;
            Degree = degree;
        }

        public static readonly PathType Catmull = new PathType(SplineType.Catmull);
        public static readonly PathType Bezier = new PathType(SplineType.BSpline);
        public static readonly PathType Linear = new PathType(SplineType.Linear);
        public static readonly PathType PerfectCurve = new PathType(SplineType.PerfectCurve);

        /// <summary>Parses <c>"B&lt;degree?&gt;" | "L" | "P"</c>; anything else is catmull.</summary>
        public static PathType FromStr(string input)
        {
            if (input.Length == 0)
                return Catmull;

            switch (input[0])
            {
                case 'B':
                    if (Util.RustMath.TryParseI32(input.Substring(1), out int degree) && degree > 0)
                        return new PathType(SplineType.BSpline, degree);

                    return Bezier;
                case 'L':
                    return Linear;
                case 'P':
                    return PerfectCurve;
                default:
                    return Catmull;
            }
        }

        public bool Equals(PathType other) => Kind == other.Kind && Degree == other.Degree;

        public override bool Equals(object? obj) => obj is PathType p && Equals(p);

        public override int GetHashCode() => System.HashCode.Combine(Kind, Degree);

        public static bool operator ==(PathType a, PathType b) => a.Equals(b);

        public static bool operator !=(PathType a, PathType b) => !a.Equals(b);
    }

    /// <summary>A positional control point of a slider curve.</summary>
    public struct PathControlPoint
    {
        public Pos Pos;
        public PathType? PathType;

        public PathControlPoint(Pos pos, PathType? pathType = null)
        {
            Pos = pos;
            PathType = pathType;
        }
    }

    public enum HitObjectKind
    {
        Circle,
        Slider,
        Spinner,
        Hold,
    }

    /// <summary>Slider-specific data of a <see cref="HitObject"/>.</summary>
    public sealed class SliderData
    {
        public double? ExpectedDist;
        public int Repeats;
        public PathControlPoint[] ControlPoints = System.Array.Empty<PathControlPoint>();
        public byte[] NodeSounds = System.Array.Empty<byte>();

        public int SpanCount => Repeats + 1;
    }

    /// <summary>All hit object data relevant for difficulty and performance calculation.</summary>
    public sealed class HitObject
    {
        public Pos Pos;
        public double StartTime;
        public HitObjectKind Kind;

        /// <summary>Set for sliders only.</summary>
        public SliderData? Slider;

        /// <summary>Duration for spinners and hold notes.</summary>
        public double Duration;

        public bool IsCircle => Kind == HitObjectKind.Circle;
        public bool IsSlider => Kind == HitObjectKind.Slider;
        public bool IsSpinner => Kind == HitObjectKind.Spinner;
        public bool IsHoldNote => Kind == HitObjectKind.Hold;

        /// <summary>End time of the object. Not correct for sliders (returns the start time).</summary>
        internal double EndTime => Kind == HitObjectKind.Spinner || Kind == HitObjectKind.Hold ? StartTime + Duration : StartTime;
    }

    /// <summary>A break section during a beatmap.</summary>
    public readonly struct BreakPeriod
    {
        public readonly double StartTime;
        public readonly double EndTime;

        public BreakPeriod(double startTime, double endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
        }

        public double Duration => EndTime - StartTime;
    }
}
