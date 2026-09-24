using System;

namespace MosuPp.Util
{
    /// <summary>Port of rosu-pp's <c>util::difficulty</c>.</summary>
    internal static class DifficultyUtil
    {
        public static double BpmToMilliseconds(double bpm, int? delimiter = null) => 60_000.0 / (delimiter ?? 4) / bpm;

        public static double MillisecondsToBpm(double ms, int? delimiter = null) => 60_000.0 / (ms * (delimiter ?? 4));

        public static double Logistic(double x, double midpointOffset, double multiplier, double? maxValue = null)
            => (maxValue ?? 1.0) / (1.0 + Math.Exp(multiplier * (midpointOffset - x)));

        public static double SmoothstepBellCurve(double x, double mean, double width)
        {
            double newX = x;
            newX -= mean;
            newX = newX > 0.0 ? width - newX : width + newX;

            return Smoothstep(newX, 0.0, width);
        }

        public static double Smoothstep(double x, double start, double end)
        {
            x = ReverseLerp(x, start, end);

            return x * x * (3.0 - 2.0 * x);
        }

        public static double Smootherstep(double x, double start, double end)
        {
            x = ReverseLerp(x, start, end);

            return x * x * x * (x * (6.0 * x - 15.0) + 10.0);
        }

        public static double ReverseLerp(double x, double start, double end) => RustMath.Clamp((x - start) / (end - start), 0.0, 1.0);

        public static double Erf(double x)
        {
            if (RustMath.AlmostEq(x, 0.0))
                return 0.0;

            if (double.IsInfinity(x))
                return x > 0 ? 1.0 : -1.0;

            if (double.IsNaN(x))
                return double.NaN;

            // * Constants for approximation (Abramowitz and Stegun formula 7.1.26)
            double t = 1.0 / (1.0 + 0.3275911 * Math.Abs(x));

            double tau = t * (0.254829592 + t * (-0.284496736 + t * (1.421413741 + t * (-1.453152027 + t * 1.061405429))));

            double erf = 1.0 - tau * Math.Exp(-x * x);

            return x >= 0.0 ? erf : -erf;
        }

        public static double ErfInv(double x)
        {
            if (x <= -1.0)
                return double.NegativeInfinity;

            if (x >= 1.0)
                return double.PositiveInfinity;

            if (RustMath.AlmostEq(x, 0.0))
                return 0.0;

            const double a = 0.147;
            double sgn = Math.Sign(x); // x is finite and non-zero here
            x = Math.Abs(x);

            double ln = Math.Log(1.0 - x * x);
            double t1 = 2.0 / (Math.PI * a) + ln / 2.0;
            double t2 = ln / a;
            double baseApprox = Math.Sqrt(t1 * t1 - t2) - t1;

            // * Correction reduces max error from -0.005 to -0.00045.
            double c = x >= 0.85 ? Math.Pow((x - 0.85) / 0.293, 8.0) : 0.0;

            return sgn * (Math.Sqrt(baseApprox) + c);
        }
    }
}
