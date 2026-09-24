using System;

namespace MosuPp.Model
{
    /// <summary>Simple single-precision <c>(x, y)</c> vector (rosu-map's <c>Pos</c>).</summary>
    public readonly struct Pos : IEquatable<Pos>
    {
        public readonly float X;
        public readonly float Y;

        public Pos(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Pos Zero => default;

        public float LengthSquared() => Dot(this);

        // Note: the sum is computed in single precision, the square root in double precision.
        public float Length()
        {
            float sq = X * X + Y * Y;
            return (float)Math.Sqrt(sq);
        }

        public float Dot(Pos other) => (X * other.X) + (Y * other.Y);

        public float Distance(Pos other) => (this - other).Length();

        public Pos Normalize()
        {
            float scale = 1f / Length();
            return new Pos(X * scale, Y * scale);
        }

        public static Pos operator +(Pos a, Pos b) => new Pos(a.X + b.X, a.Y + b.Y);

        public static Pos operator -(Pos a, Pos b) => new Pos(a.X - b.X, a.Y - b.Y);

        public static Pos operator *(Pos a, float s) => new Pos(a.X * s, a.Y * s);

        public static Pos operator /(Pos a, float s) => new Pos(a.X / s, a.Y / s);

        // PartialEq semantics of Rust (NaN != NaN).
        public static bool operator ==(Pos a, Pos b) => a.X == b.X && a.Y == b.Y;

        public static bool operator !=(Pos a, Pos b) => !(a == b);

        public bool Equals(Pos other) => this == other;

        public override bool Equals(object? obj) => obj is Pos p && this == p;

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"({X}, {Y})";
    }
}
