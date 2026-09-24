using System;
using System.Globalization;

namespace MosuPp.Util
{
    /// <summary>
    /// Helpers that reproduce Rust's floating point / integer semantics exactly.
    /// rosu-pp relies on them (NaN-ignoring min/max, saturating casts, total ordering, ...),
    /// so the port must not use the slightly different .NET equivalents directly.
    /// </summary>
    internal static class RustMath
    {
        public const double F64Epsilon = 2.220446049250313e-16; // f64::EPSILON
        public const float F32Epsilon = 1.1920929e-7f; // f32::EPSILON

        // Rust's f64::max / f64::min ignore NaN (IEEE maxNum), Math.Max/Min propagate it.
        public static double Max(double a, double b)
        {
            if (double.IsNaN(a)) return b;
            if (double.IsNaN(b)) return a;
            return a > b ? a : (b > a ? b : (a == b && double.IsNegative(a) ? b : a));
        }

        public static double Min(double a, double b)
        {
            if (double.IsNaN(a)) return b;
            if (double.IsNaN(b)) return a;
            return a < b ? a : (b < a ? b : (a == b && double.IsNegative(b) ? b : a));
        }

        public static float Max(float a, float b)
        {
            if (float.IsNaN(a)) return b;
            if (float.IsNaN(b)) return a;
            return a > b ? a : (b > a ? b : a);
        }

        public static float Min(float a, float b)
        {
            if (float.IsNaN(a)) return b;
            if (float.IsNaN(b)) return a;
            return a < b ? a : (b < a ? b : a);
        }

        // Rust's clamp: NaN passes through.
        public static double Clamp(double x, double min, double max)
        {
            if (x < min) x = min;
            if (x > max) x = max;
            return x;
        }

        public static float Clamp(float x, float min, float max)
        {
            if (x < min) x = min;
            if (x > max) x = max;
            return x;
        }

        /// <summary>FloatExt::eq — |a - b| &lt;= EPSILON.</summary>
        /// <summary>
        /// Correctly rounded cube root, like Rust's <c>f64::cbrt</c>. .NET's <see cref="Math.Cbrt"/>
        /// (the C library's cbrt) may be off by one ulp which would make results differ from rosu-pp
        /// in the last bits.
        /// </summary>
        public static double Cbrt(double x)
        {
            if (x == 0.0 || double.IsNaN(x) || double.IsInfinity(x))
                return Math.Cbrt(x);

            bool negative = x < 0.0;
            double ax = Math.Abs(x);
            double y = Math.Cbrt(ax);

            // Math.Cbrt is within a few ulps; walk towards the correctly rounded result.
            for (int i = 0; i < 4; i++)
            {
                double lower = Math.BitDecrement(y);
                double upper = Math.BitIncrement(y);

                if (CompareMidpointCube(lower, y, ax) > 0)
                    y = lower;
                else if (CompareMidpointCube(y, upper, ax) < 0)
                    y = upper;
                else
                    break;
            }

            return negative ? -y : y;
        }

        /// <summary>Exactly compares <c>((a + b) / 2)^3</c> with <paramref name="x"/> (all positive, finite).</summary>
        private static int CompareMidpointCube(double a, double b, double x)
        {
            (System.Numerics.BigInteger ma, int ea) = Decompose(a);
            (System.Numerics.BigInteger mb, int eb) = Decompose(b);
            (System.Numerics.BigInteger mx, int ex) = Decompose(x);

            // mid = (ma*2^ea + mb*2^eb) / 2 = m * 2^e
            int e = Math.Min(ea, eb);
            System.Numerics.BigInteger m = (ma << (ea - e)) + (mb << (eb - e));
            e -= 1;

            // compare m^3 * 2^(3e) with mx * 2^ex
            System.Numerics.BigInteger cube = m * m * m;
            int ce = 3 * e;
            int common = Math.Min(ce, ex);

            return (cube << (ce - common)).CompareTo(mx << (ex - common));
        }

        private static (System.Numerics.BigInteger Mantissa, int Exponent) Decompose(double d)
        {
            long bits = BitConverter.DoubleToInt64Bits(d);
            int exp = (int)((bits >> 52) & 0x7FF);
            long frac = bits & 0xF_FFFF_FFFF_FFFFL;

            if (exp == 0)
                return (frac, -1074);

            return (frac | (1L << 52), exp - 1075);
        }

        /// <summary>
        /// <c>x.powf(2.0)</c> as compiled by Rust/LLVM: a plain multiplication (correctly rounded),
        /// whereas .NET's <see cref="Math.Pow"/> may differ in the last bit.
        /// </summary>
        public static double Pow2(double x) => x * x;

        /// <summary><c>x.powf(0.5)</c> as compiled by Rust/LLVM: <c>|sqrt(x)|</c> with <c>pow(-inf, 0.5) = +inf</c>.</summary>
        public static double PowHalf(double x) => double.IsNegativeInfinity(x) ? double.PositiveInfinity : Math.Abs(Math.Sqrt(x));

        public static bool AlmostEq(double a, double b) => Math.Abs(a - b) <= F64Epsilon;

        public static bool AlmostEq(double a, double b, double acceptableDifference) => Math.Abs(a - b) <= acceptableDifference;

        /// <summary>FloatExt::not_eq — |a - b| &gt;= EPSILON.</summary>
        public static bool NotEq(double a, double b) => Math.Abs(a - b) >= F64Epsilon;

        public static bool AlmostEq(float a, float b) => MathF.Abs(a - b) <= F32Epsilon;

        /// <summary>FloatExt::lerp (mirrors .NET's double.Lerp formula).</summary>
        public static double Lerp(double value1, double value2, double amount) => (value1 * (1.0 - amount)) + (value2 * amount);

        /// <summary>f64::total_cmp</summary>
        public static int TotalCmp(double a, double b)
        {
            long x = BitConverter.DoubleToInt64Bits(a);
            long y = BitConverter.DoubleToInt64Bits(b);
            x ^= (long)((ulong)(x >> 63) >> 1);
            y ^= (long)((ulong)(y >> 63) >> 1);
            return x.CompareTo(y);
        }

        /// <summary>f64::round_ties_even</summary>
        public static double RoundTiesEven(double x) => Math.Round(x, MidpointRounding.ToEven);

        /// <summary>f64::to_radians</summary>
        public static double ToRadians(double deg) => deg * (Math.PI / 180.0);

        // ---- saturating `as` casts ----

        public static int ToI32(double x)
        {
            if (double.IsNaN(x)) return 0;
            if (x >= 2147483647.0) return int.MaxValue;
            if (x <= -2147483648.0) return int.MinValue;
            return (int)x;
        }

        public static int ToI32(float x) => ToI32((double)x);

        public static uint ToU32(double x)
        {
            if (double.IsNaN(x) || x <= 0.0) return 0;
            if (x >= 4294967295.0) return uint.MaxValue;
            return (uint)x;
        }

        /// <summary>`as usize` clamped into the int range (sufficient for every use in this library).</summary>
        public static int ToUsizeClamped(double x)
        {
            if (double.IsNaN(x) || x <= 0.0) return 0;
            if (x >= int.MaxValue) return int.MaxValue;
            return (int)x;
        }

        public static uint SaturatingSub(uint a, uint b) => a > b ? a - b : 0;

        // ---- Rust-compatible number parsing ----

        /// <summary>Rust's `str::parse::&lt;f64&gt;` (no surrounding whitespace allowed).</summary>
        public static bool TryParseF64(string s, out double value)
        {
            value = 0;

            if (s.Length == 0)
                return false;

            int i = 0;
            bool negative = false;

            if (s[0] == '+' || s[0] == '-')
            {
                negative = s[0] == '-';
                i = 1;
            }

            string rest = s.Substring(i);

            if (string.Equals(rest, "inf", StringComparison.OrdinalIgnoreCase) || string.Equals(rest, "infinity", StringComparison.OrdinalIgnoreCase))
            {
                value = negative ? double.NegativeInfinity : double.PositiveInfinity;
                return true;
            }

            if (string.Equals(rest, "nan", StringComparison.OrdinalIgnoreCase))
            {
                value = double.NaN;
                return true;
            }

            // Validate the decimal grammar: digits* ('.' digits*)? (('e'|'E') sign? digits+)?
            int digits = 0;
            int j = i;

            while (j < s.Length && IsDigit(s[j])) { j++; digits++; }

            if (j < s.Length && s[j] == '.')
            {
                j++;
                while (j < s.Length && IsDigit(s[j])) { j++; digits++; }
            }

            if (digits == 0)
                return false;

            if (j < s.Length && (s[j] == 'e' || s[j] == 'E'))
            {
                j++;
                if (j < s.Length && (s[j] == '+' || s[j] == '-')) j++;
                int expDigits = 0;
                while (j < s.Length && IsDigit(s[j])) { j++; expDigits++; }
                if (expDigits == 0) return false;
            }

            if (j != s.Length)
                return false;

            return double.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Rust's `str::parse::&lt;f32&gt;`.</summary>
        public static bool TryParseF32(string s, out float value)
        {
            value = 0;

            // Validate with the f64 grammar, then parse directly as single precision
            // to get correct rounding (double rounding could differ).
            if (!TryParseF64(s, out double d))
                return false;

            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                value = (float)d;
                return true;
            }

            return float.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Rust's `str::parse::&lt;i32&gt;` (strict; no whitespace).</summary>
        public static bool TryParseI32(string s, out int value)
        {
            value = 0;

            if (s.Length == 0)
                return false;

            int i = (s[0] == '+' || s[0] == '-') ? 1 : 0;

            if (i == s.Length)
                return false;

            for (int j = i; j < s.Length; j++)
            {
                if (!IsDigit(s[j]))
                    return false;
            }

            return int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        /// <summary>
        /// Rust's `slice::binary_search_by` (the branchless variant used since Rust 1.82).
        /// The exact index returned for duplicate keys matters for curve interpolation.
        /// </summary>
        /// <returns>Non-negative index if found, otherwise <c>~insertionPoint</c>.</returns>
        public static int BinarySearchBy<T>(System.Collections.Generic.IReadOnlyList<T> list, Func<T, int> cmp, int count = -1)
        {
            int size = count < 0 ? list.Count : count;

            if (size == 0)
                return ~0;

            int @base = 0;

            while (size > 1)
            {
                int half = size / 2;
                int mid = @base + half;

                if (cmp(list[mid]) <= 0)
                    @base = mid;

                size -= half;
            }

            int c = cmp(list[@base]);

            if (c == 0)
                return @base;

            return ~(@base + (c < 0 ? 1 : 0));
        }
    }
}
