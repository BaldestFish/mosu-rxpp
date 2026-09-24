using System;
using System.Collections.Generic;
using MosuPp.Util;

namespace MosuPp.Model
{
    /// <summary>
    /// Slider curve (port of rosu-map's <c>Curve</c>): approximated path points and the
    /// cumulative length at each path point.
    /// </summary>
    public sealed class Curve
    {
        private const float BezierTolerance = 0.25f;
        private const int CatmullDetail = 50;
        private const float CircularArcTolerance = 0.1f;

        private readonly List<Pos> path;
        private readonly List<double> lengths;

        public IReadOnlyList<Pos> Path => path;
        public IReadOnlyList<double> Lengths => lengths;

        private Curve(List<Pos> path, List<double> lengths)
        {
            this.path = path;
            this.lengths = lengths;
        }

        /// <param name="isOsuMode">Only osu!standard optimizes catmull segments.</param>
        public static Curve Create(bool isOsuMode, IReadOnlyList<PathControlPoint> points, double? expectedLen)
        {
            var path = new List<Pos>();
            var lengths = new List<double>();
            double optimizedLen = 0.0;

            CalculatePath(isOsuMode, points, path, ref optimizedLen);
            CalculateLength(path, lengths, expectedLen, optimizedLen);

            return new Curve(path, lengths);
        }

        /// <summary>The total distance of the curve.</summary>
        public double Dist() => lengths.Count > 0 ? lengths[lengths.Count - 1] : 0.0;

        public double ProgressToDist(double progress) => RustMath.Clamp(progress, 0.0, 1.0) * Dist();

        public int IdxOfDist(double d)
        {
            // partial_cmp with NaN => Equal
            int res = RustMath.BinarySearchBy(lengths, len => len < d ? -1 : (len > d ? 1 : 0));
            return res >= 0 ? res : ~res;
        }

        /// <summary>The interpolated position at the given progress (relative to the slider head).</summary>
        public Pos PositionAt(double progress)
        {
            double d = ProgressToDist(progress);
            int i = IdxOfDist(d);

            return InterpolateVertices(i, d);
        }

        public Pos InterpolateVertices(int i, double d)
        {
            if (path.Count == 0)
                return default;

            if (i == 0)
                return path[0];

            if (i >= path.Count)
                return path[path.Count - 1];

            Pos p1 = path[i];
            Pos p0 = path[i - 1];

            double d0 = lengths[i - 1];
            double d1 = lengths[i];

            // * Avoid division by an almost-zero number in case
            // * two points are extremely close to each other
            if (Math.Abs(d0 - d1) <= RustMath.F64Epsilon)
                return p0;

            double w = (d - d0) / (d1 - d0);

            return p0 + (p1 - p0) * (float)w;
        }

        private static void CalculatePath(bool isOsuMode, IReadOnlyList<PathControlPoint> points, List<Pos> path, ref double optimizedLen)
        {
            if (points.Count == 0)
                return;

            path.Clear();
            optimizedLen = 0.0;

            var vertices = new Pos[points.Count];

            for (int k = 0; k < points.Count; k++)
                vertices[k] = points[k].Pos;

            var bufs = new BezierBuffers();
            int start = 0;

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].PathType == null && i < points.Count - 1)
                    continue;

                // * The current vertex ends the segment
                int segLen = i - start + 1;

                if (segLen == 1)
                {
                    // * No need to calculate path when there is only 1 vertex
                    path.Add(vertices[start]);
                }
                else
                {
                    var segment = new ArraySegment<Pos>(vertices, start, segLen);
                    SplineType segmentKind = points[start].PathType?.Kind ?? SplineType.Linear;

                    int pathLen = path.Count;

                    CalculateSubpath(isOsuMode, path, segment, segmentKind, ref optimizedLen, bufs);

                    // * Skip the first vertex if it is the same as the last vertex from the previous segment
                    bool skipFirst = pathLen >= 1 && pathLen < path.Count && path[pathLen - 1] == path[pathLen];

                    if (skipFirst)
                        path.RemoveAt(pathLen);
                }

                // * Start the new segment at the current vertex
                start = i;
            }
        }

        private static void CalculateLength(List<Pos> path, List<double> cumulativeLen, double? expectedLen, double optimizedLen)
        {
            cumulativeLen.Clear();
            double calculatedLen = optimizedLen;
            cumulativeLen.Add(0.0);

            for (int i = 0; i + 1 < path.Count; i++)
            {
                calculatedLen += (double)(path[i + 1] - path[i]).Length();
                cumulativeLen.Add(calculatedLen);
            }

            if (expectedLen is not double expected || Math.Abs(calculatedLen - expected) < RustMath.F64Epsilon)
                return;

            // * In osu-stable, if the last two path points of a slider are equal, extension is not performed
            if (path.Count >= 2 && path[path.Count - 2] == path[path.Count - 1] && expected > calculatedLen)
            {
                cumulativeLen.Add(calculatedLen);
                return;
            }

            // Shortcut when it's just (0,0) since there's nothing to do anyway
            if (cumulativeLen.Count == 1)
                return;

            // * The last length is always incorrect
            cumulativeLen.RemoveAt(cumulativeLen.Count - 1);

            int lastValid = 0;

            for (int k = cumulativeLen.Count - 1; k >= 0; k--)
            {
                if (cumulativeLen[k] < expected)
                {
                    lastValid = k + 1;
                    break;
                }
            }

            // * The path will be shortened further, in which case we should trim
            // * any more unnecessary lengths and their associated path segments
            if (lastValid < cumulativeLen.Count)
            {
                cumulativeLen.RemoveRange(lastValid, cumulativeLen.Count - lastValid);

                if (lastValid + 1 < path.Count)
                    path.RemoveRange(lastValid + 1, path.Count - (lastValid + 1));

                if (cumulativeLen.Count == 0)
                {
                    // * The expected distance is negative or zero
                    cumulativeLen.Add(0.0);
                    return;
                }
            }

            int endIdx = cumulativeLen.Count;
            int prevIdx = endIdx - 1;

            // * The direction of the segment to shorten or lengthen
            Pos dir = (path[endIdx] - path[prevIdx]).Normalize();

            path[endIdx] = path[prevIdx] + dir * (float)(expected - cumulativeLen[prevIdx]);
            cumulativeLen.Add(expected);
        }

        private static void CalculateSubpath(bool isOsuMode, List<Pos> path, ArraySegment<Pos> subPoints, SplineType pathType,
                                             ref double optimizedLen, BezierBuffers bufs)
        {
            switch (pathType)
            {
                case SplineType.Linear:
                    path.AddRange(subPoints);
                    break;

                case SplineType.PerfectCurve:
                    if (subPoints.Count == 3 && ApproximateCircularArc(path, subPoints[0], subPoints[1], subPoints[2]))
                        return;

                    ApproximateBezier(path, subPoints, bufs);
                    break;

                case SplineType.Catmull:
                {
                    int startLen = path.Count;
                    ApproximateCatmull(path, subPoints);

                    if (!isOsuMode)
                        return;

                    var subPath = path.GetRange(startLen, path.Count - startLen);
                    path.RemoveRange(startLen, path.Count - startLen);

                    Pos? lastStart = null;
                    double lenRemovedSinceStart = 0.0;

                    const int catmullSegmentLen = CatmullDetail * 2;

                    for (int i = 0; i < subPath.Count; i++)
                    {
                        Pos curr = subPath[i];

                        if (lastStart is not Pos ls)
                        {
                            path.Add(curr);
                            lastStart = curr;
                            continue;
                        }

                        double distFromStart = ls.Distance(curr);
                        lenRemovedSinceStart += subPath[i - 1].Distance(curr);

                        // * Either 6px from the start, the last vertex at every knot, or the end of the path.
                        if (distFromStart > 6.0 || (i + 1) % catmullSegmentLen == 0 || i == subPath.Count - 1)
                        {
                            path.Add(curr);
                            optimizedLen += lenRemovedSinceStart - distFromStart;

                            lastStart = null;
                            lenRemovedSinceStart = 0.0;
                        }
                    }

                    break;
                }

                case SplineType.BSpline:
                    ApproximateBezier(path, subPoints, bufs);
                    break;
            }
        }

        private sealed class BezierBuffers
        {
            public Pos[] Left = Array.Empty<Pos>();
            public Pos[] Right = Array.Empty<Pos>();
            public Pos[] Midpoints = Array.Empty<Pos>();
            public Pos[] LeftChild = Array.Empty<Pos>();

            public void ExtendExact(int len)
            {
                if (len <= Left.Length)
                    return;

                Array.Resize(ref Left, len);
                Array.Resize(ref Right, len);
                Array.Resize(ref Midpoints, len);
                Array.Resize(ref LeftChild, len);
            }
        }

        private static void ApproximateBezier(List<Pos> path, ArraySegment<Pos> points, BezierBuffers bufs)
        {
            bufs.ExtendExact(points.Count);
            ApproximateBSpline(path, points, bufs);
        }

        private static void ApproximateCatmull(List<Pos> path, ArraySegment<Pos> points)
        {
            if (points.Count == 1)
                return;

            // Handle first iteration distinctly because of v1
            Pos v1 = points[0];
            Pos v2 = points[0];
            Pos v3 = points.Count > 1 ? points[1] : v2;
            Pos v4 = points.Count > 2 ? points[2] : v3 * 2f - v2;

            CatmullSubpath(path, v1, v2, v3, v4);

            // Remaining iterations
            for (int i = 2; i < points.Count; i++)
            {
                v1 = points[i - 2];
                v2 = points[i - 1];
                v3 = i < points.Count ? points[i] : v2 * 2f - v1;
                v4 = i + 1 < points.Count ? points[i + 1] : v3 * 2f - v2;

                CatmullSubpath(path, v1, v2, v3, v4);
            }
        }

        private static bool ApproximateCircularArc(List<Pos> path, Pos a, Pos b, Pos c)
        {
            if (!CircularArcProperties(a, b, c, out double thetaStart, out double thetaRange, out double direction, out float radius, out Pos centre))
                return false;

            // * We select the amount of points for the approximation by requiring the discrete curvature
            // * to be smaller than the provided tolerance. The exact angle required to meet the tolerance
            // * is: 2 * Math.Acos(1 - TOLERANCE / r)
            // * The special case is required for extremely short sliders where the radius is smaller than
            // * the tolerance. This is a pathological rather than a realistic case.
            int subPoints;

            if (2f * radius <= CircularArcTolerance)
            {
                subPoints = 2;
            }
            else
            {
                float divisor = 2f * MathF.Acos(1f - (CircularArcTolerance / radius));

                // In C# it holds `(int)Infinity == -2147483648` whereas in Rust it's 2147483647
                // so rosu-pp works around this edge case, see map /b/2568364
                if (MathF.Abs(divisor) <= RustMath.F32Epsilon)
                    subPoints = 2;
                else
                    subPoints = Math.Max(RustMath.ToUsizeClamped(Math.Ceiling(thetaRange / (double)divisor)), 2);
            }

            // * 1000 subpoints requires an arc length of at least ~120 thousand to occur
            // * See here for calculations https://www.desmos.com/calculator/umj6jvmcz7
            if (subPoints >= 1000)
                return false;

            double div = subPoints - 1;
            double directedRange = direction * thetaRange;

            for (int i = 0; i < subPoints; i++)
            {
                double fract = i / div;
                double theta = thetaStart + fract * directedRange;
                double sin = Math.Sin(theta);
                double cos = Math.Cos(theta);

                var origin = new Pos((float)cos, (float)sin);

                path.Add(centre + origin * radius);
            }

            return true;
        }

        private static void ApproximateBSpline(List<Pos> path, ArraySegment<Pos> points, BezierBuffers bufs)
        {
            int p = points.Count;

            var toFlatten = new Stack<Pos[]>();
            var freeBufs = new Stack<Pos[]>();

            toFlatten.Push(points.ToArray());

            // * "toFlatten" contains all the curves which are not yet approximated well enough.
            // * We use a stack to emulate recursion without the risk of running into a stack overflow.
            while (toFlatten.Count > 0)
            {
                Pos[] parent = toFlatten.Pop();

                if (BezierIsFlatEnough(parent))
                {
                    // * If the control points we currently operate on are sufficiently "flat", we use
                    // * an extension to De Casteljau's algorithm to obtain a piecewise-linear approximation
                    // * of the bezier curve represented by our control points, consisting of the same amount
                    // * of points as there are control points.
                    BezierApproximate(parent, path, bufs.Left, bufs.Right, bufs.Midpoints);
                    freeBufs.Push(parent);
                    continue;
                }

                // * If we do not yet have a sufficiently "flat" (in other words, detailed) approximation we keep
                // * subdividing the curve we are currently operating on.
                Pos[] rightChild = freeBufs.Count > 0 ? freeBufs.Pop() : new Pos[p];

                BezierSubdivide(parent, bufs.LeftChild, rightChild, bufs.Midpoints);

                // * We re-use the buffer of the parent for one of the children, so that we save one allocation per iteration.
                Array.Copy(bufs.LeftChild, parent, p);

                toFlatten.Push(rightChild);
                toFlatten.Push(parent);
            }

            path.Add(points[p - 1]);
        }

        private static bool BezierIsFlatEnough(Pos[] points)
        {
            const float limit = BezierTolerance * BezierTolerance * 4f;

            for (int i = 1; i + 1 < points.Length; i++)
            {
                if ((points[i - 1] - points[i] * 2f + points[i + 1]).LengthSquared() > limit)
                    return false;
            }

            return true;
        }

        private static void BezierSubdivide(Pos[] points, Pos[] l, Pos[] r, Pos[] midpoints)
        {
            int count = points.Length;
            Array.Copy(points, midpoints, count);

            for (int i = count - 1; i >= 1; i--)
            {
                l[count - i - 1] = midpoints[0];
                r[i] = midpoints[i];

                for (int j = 0; j < i; j++)
                    midpoints[j] = (midpoints[j] + midpoints[j + 1]) / 2f;
            }

            l[count - 1] = midpoints[0];
            r[0] = midpoints[0];
        }

        // * https://en.wikipedia.org/wiki/De_Casteljau%27s_algorithm
        private static void BezierApproximate(Pos[] points, List<Pos> path, Pos[] l, Pos[] r, Pos[] midpoints)
        {
            int count = points.Length;

            BezierSubdivide(points, l, r, midpoints);
            path.Add(points[0]);

            // chain = l[..count] ++ r[1..count]
            Pos Chain(int idx) => idx < count ? l[idx] : r[idx - count + 1];

            int chainLen = 2 * count - 1;

            for (int k = 0; k + 3 < chainLen; k += 2)
            {
                Pos prev = Chain(k + 1);
                Pos curr = Chain(k + 2);
                Pos next = Chain(k + 3);

                path.Add((prev + curr * 2f + next) * 0.25f);
            }
        }

        private static void CatmullSubpath(List<Pos> path, Pos v1, Pos v2, Pos v3, Pos v4)
        {
            float x1 = 2f * v2.X;
            float x2 = -v1.X + v3.X;
            float x3 = 2f * v1.X - 5f * v2.X + 4f * v3.X - v4.X;
            float x4 = -v1.X + 3f * (v2.X - v3.X) + v4.X;

            float y1 = 2f * v2.Y;
            float y2 = -v1.Y + v3.Y;
            float y3 = 2f * v1.Y - 5f * v2.Y + 4f * v3.Y - v4.Y;
            float y4 = -v1.Y + 3f * (v2.Y - v3.Y) + v4.Y;

            const float catmullDetail = CatmullDetail;

            for (int ci = 0; ci < CatmullDetail; ci++)
            {
                float c = ci;
                float t1 = c / catmullDetail;
                float t2 = t1 * t1;
                float t3 = t2 * t1;

                var pos1 = new Pos(0.5f * (x1 + x2 * t1 + x3 * t2 + x4 * t3), 0.5f * (y1 + y2 * t1 + y3 * t2 + y4 * t3));

                t1 = (c + 1f) / catmullDetail;
                t2 = t1 * t1;
                t3 = t2 * t1;

                var pos2 = new Pos(0.5f * (x1 + x2 * t1 + x3 * t2 + x4 * t3), 0.5f * (y1 + y2 * t1 + y3 * t2 + y4 * t3));

                path.Add(pos1);
                path.Add(pos2);
            }
        }

        private static bool CircularArcProperties(Pos a, Pos b, Pos c, out double thetaStart, out double thetaRange, out double direction,
                                                  out float radius, out Pos centre)
        {
            thetaStart = thetaRange = direction = 0;
            radius = 0;
            centre = default;

            // * If we have a degenerate triangle where a side-length is almost zero,
            // * then give up and fallback to a more numerically stable method.
            if (MathF.Abs((b.Y - a.Y) * (c.X - a.X) - (b.X - a.X) * (c.Y - a.Y)) <= RustMath.F32Epsilon)
                return false;

            // * See: https://en.wikipedia.org/wiki/Circumscribed_circle#Cartesian_coordinates_2
            float d = 2f * (a.X * (b - c).Y + b.X * (c - a).Y + c.X * (a - b).Y);
            float aSq = a.LengthSquared();
            float bSq = b.LengthSquared();
            float cSq = c.LengthSquared();

            centre = new Pos(
                (aSq * (b - c).Y + bSq * (c - a).Y + cSq * (a - b).Y) / d,
                (aSq * (c - b).X + bSq * (a - c).X + cSq * (b - a).X) / d);

            Pos dA = a - centre;
            Pos dC = c - centre;

            radius = dA.Length();

            thetaStart = Math.Atan2(dA.Y, dA.X);
            double thetaEnd = Math.Atan2(dC.Y, dC.X);

            while (thetaEnd < thetaStart)
                thetaEnd += 2.0 * Math.PI;

            direction = 1.0;
            thetaRange = thetaEnd - thetaStart;

            // * Decide in which direction to draw the circle,
            // * depending on which side of AC B lies.
            Pos orthoAToC = c - a;
            orthoAToC = new Pos(orthoAToC.Y, -orthoAToC.X);

            if (orthoAToC.Dot(b - a) < 0f)
            {
                direction = -direction;
                thetaRange = 2.0 * Math.PI - thetaRange;
            }

            return true;
        }
    }
}
