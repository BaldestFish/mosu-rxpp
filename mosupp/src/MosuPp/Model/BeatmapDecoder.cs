using System;
using System.Collections.Generic;
using System.Text;
using MosuPp.Util;

namespace MosuPp.Model
{
    /// <summary>
    /// Port of rosu-map's decoding framework combined with rosu-pp's <c>DecodeBeatmap</c> implementation.
    /// Invalid lines are skipped silently, exactly like rosu-pp does.
    /// </summary>
    internal sealed class BeatmapDecoder
    {
        private const int MaxCoordinateValue = 131_072;
        private const double MaxParseValue = int.MaxValue;

        private enum Section
        {
            General,
            Editor,
            Metadata,
            Difficulty,
            Events,
            TimingPoints,
            Colors,
            HitObjects,
            Variables,
            CatchTheBeat,
            Mania,
        }

        /// <summary>Thrown internally to abort processing of the current line (rosu's `?`).</summary>
        private sealed class LineError : Exception
        {
            public LineError(string message) : base(message)
            {
            }
        }

        // ---- state ----
        private readonly int version;
        private float stackLeniency = Beatmap.DefaultStackLeniency;
        private GameMode mode = GameMode.Osu;
        private bool hasApproachRate;

        private float hpDrainRate = 5f;
        private float circleSize = 5f;
        private float overallDifficulty = 5f;
        private float approachRate = 5f;
        private double sliderMultiplier = 1.4;
        private double sliderTickRate = 1.0;

        private readonly List<BreakPeriod> breaks = new List<BreakPeriod>();
        private readonly List<TimingPoint> timingPoints = new List<TimingPoint>();
        private readonly List<DifficultyPoint> difficultyPoints = new List<DifficultyPoint>();
        private readonly List<EffectPoint> effectPoints = new List<EffectPoint>();
        private readonly List<HitObject> hitObjects = new List<HitObject>();
        private readonly List<byte> hitSounds = new List<byte>();

        private double pendingControlPointsTime;
        private TimingPoint? pendingTimingPoint;
        private DifficultyPoint? pendingDifficultyPoint;
        private EffectPoint? pendingEffectPoint;

        // Persisted across sliders on purpose: rosu-map keeps (stale) points in here if a slider line fails midway.
        private readonly List<PathControlPoint> curvePoints = new List<PathControlPoint>();
        private readonly List<PathControlPoint> vertices = new List<PathControlPoint>();

        private BeatmapDecoder(int version)
        {
            this.version = version;
        }

        public static Beatmap Decode(byte[] bytes) => DecodeString(DecodeText(bytes));

        public static Beatmap DecodeString(string content)
        {
            string[] lines = content.Split('\n');
            int idx = 0;

            string? ReadLine()
            {
                if (idx >= lines.Length)
                    return null;

                // The last element after a trailing '\n' is not a line in rosu-map.
                if (idx == lines.Length - 1 && lines[idx].Length == 0)
                {
                    idx++;
                    return null;
                }

                return lines[idx++].TrimEnd();
            }

            // ---- format version ----
            int? parsedVersion = null;
            bool useCurrLine = false;
            string? currLine = null;

            while (true)
            {
                string? line = ReadLine();

                if (line == null)
                    break;

                currLine = line;

                if (!line.StartsWith("osu file format v", StringComparison.Ordinal))
                {
                    if (line.Length == 0)
                        continue;

                    useCurrLine = true; // unknown file format
                    break;
                }

                int lastV = line.LastIndexOf('v');
                string num = line.Substring(lastV + 1);

                if (TryParseI32Num(num, out int v))
                    parsedVersion = v;
                else
                    useCurrLine = true;

                break;
            }

            var state = new BeatmapDecoder(parsedVersion ?? Beatmap.LatestFormatVersion);

            // ---- first section ----
            Section? section = null;

            if (useCurrLine && currLine != null)
                section = TryParseSection(currLine);

            while (section == null)
            {
                string? line = ReadLine();

                if (line == null)
                    return state.Finish();

                section = TryParseSection(line);
            }

            // ---- sections ----
            while (true)
            {
                string? line = ReadLine();

                if (line == null)
                    break;

                if (line.Length == 0 || line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                    continue;

                Section? next = TryParseSection(line);

                if (next != null)
                {
                    section = next;
                    continue;
                }

                try
                {
                    switch (section.Value)
                    {
                        case Section.General:
                            state.ParseGeneral(line);
                            break;
                        case Section.Difficulty:
                            state.ParseDifficulty(line);
                            break;
                        case Section.Events:
                            state.ParseEvents(line);
                            break;
                        case Section.TimingPoints:
                            state.ParseTimingPoints(line);
                            break;
                        case Section.HitObjects:
                            state.ParseHitObjects(line);
                            break;
                    }
                }
                catch (LineError)
                {
                    // Same as rosu-map without the `tracing` feature: the line is ignored.
                }
            }

            return state.Finish();
        }

        private static string DecodeText(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return new UTF8Encoding(false, false).GetString(bytes, 3, bytes.Length - 3);

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

            // Invalid UTF-8 is replaced with U+FFFD
            return new UTF8Encoding(false, false).GetString(bytes);
        }

        private static Section? TryParseSection(string line)
        {
            if (line.Length < 2 || line[0] != '[' || line[line.Length - 1] != ']')
                return null;

            switch (line.Substring(1, line.Length - 2))
            {
                case "General": return Section.General;
                case "Editor": return Section.Editor;
                case "Metadata": return Section.Metadata;
                case "Difficulty": return Section.Difficulty;
                case "Events": return Section.Events;
                case "TimingPoints": return Section.TimingPoints;
                case "Colours": return Section.Colors;
                case "HitObjects": return Section.HitObjects;
                case "Variables": return Section.Variables;
                case "CatchTheBeat": return Section.CatchTheBeat;
                case "Mania": return Section.Mania;
                default: return null;
            }
        }

        // ---- rosu-map util ----

        private static string TrimComment(string s)
        {
            int i = s.IndexOf("//", StringComparison.Ordinal);
            return (i >= 0 ? s.Substring(0, i) : s).TrimEnd();
        }

        private static (string Key, string Value) KeyValue(string s)
        {
            string[] split = s.Split(':');
            return (split[0].Trim(), split.Length > 1 ? split[1].Trim() : string.Empty);
        }

        private static bool TryParseI32Num(string s, out int value)
        {
            // ParseNumber for i32: trimmed, limit i32::MAX (always satisfied)
            return RustMath.TryParseI32(s.Trim(), out value);
        }

        private static int ParseI32(string s)
        {
            if (!TryParseI32Num(s, out int v))
                throw new LineError("invalid integer");

            return v;
        }

        private static float ParseF32(string s, float limit = (float)MaxParseValue)
        {
            if (!RustMath.TryParseF32(s.Trim(), out float n))
                throw new LineError("invalid float");

            if (n < -limit || n > limit || float.IsNaN(n))
                throw new LineError("invalid number");

            return n;
        }

        private static double ParseF64(string s, double limit = MaxParseValue)
        {
            if (!RustMath.TryParseF64(s.Trim(), out double n))
                throw new LineError("invalid float");

            if (n < -limit || n > limit || double.IsNaN(n))
                throw new LineError("invalid number");

            return n;
        }

        /// <summary>Strict `str::parse::&lt;i32&gt;` (used by HitObjectType, HitSoundType, EffectFlags).</summary>
        private static int ParseI32Strict(string s)
        {
            if (!RustMath.TryParseI32(s, out int v))
                throw new LineError("invalid integer");

            return v;
        }

        private static byte ParseHitSound(string s) => (byte)(ParseI32Strict(s) & 0xFF);

        // ---- sections ----

        private void ParseGeneral(string line)
        {
            (string key, string value) = KeyValue(TrimComment(line));

            switch (key)
            {
                case "StackLeniency":
                    stackLeniency = ParseF32(value);
                    break;
                case "Mode":
                    mode = value switch
                    {
                        "0" => GameMode.Osu,
                        "1" => GameMode.Taiko,
                        "2" => GameMode.Catch,
                        "3" => GameMode.Mania,
                        _ => throw new LineError("invalid mode"),
                    };
                    break;
            }
        }

        private void ParseDifficulty(string line)
        {
            (string key, string value) = KeyValue(TrimComment(line));

            switch (key)
            {
                case "HPDrainRate":
                    hpDrainRate = ParseF32(value);
                    break;
                case "CircleSize":
                    circleSize = ParseF32(value);
                    break;
                case "OverallDifficulty":
                    overallDifficulty = ParseF32(value);

                    if (!hasApproachRate)
                        approachRate = overallDifficulty;

                    break;
                case "ApproachRate":
                    approachRate = ParseF32(value);
                    hasApproachRate = true;
                    break;
                case "SliderMultiplier":
                    sliderMultiplier = ParseF64(value);
                    break;
                case "SliderTickRate":
                    sliderTickRate = ParseF64(value);
                    break;
            }
        }

        private void ParseEvents(string line)
        {
            string[] split = TrimComment(line).Split(',');

            bool isBreak = split[0] switch
            {
                "0" or "Background" or "1" or "Video" or "3" or "Colour" or "4" or "Sprite" or "5" or "Sample" or "6" or "Animation" => false,
                "2" or "Break" => true,
                _ => throw new LineError("invalid event type"),
            };

            if (!isBreak)
                return;

            if (split.Length < 3)
                throw new LineError("invalid event line");

            double startTime = ParseF64(split[1]);
            double endTime = RustMath.Max(startTime, ParseF64(split[2]));

            breaks.Add(new BreakPeriod(startTime, endTime));
        }

        private void ParseTimingPoints(string line)
        {
            string[] split = TrimComment(line).Split(',');

            if (split.Length < 2)
                throw new LineError("invalid timing point line");

            double time = ParseF64(split[0]);

            // Manual parse so that NaN does not cause an error
            if (!RustMath.TryParseF64(split[1].Trim(), out double beatLen))
                throw new LineError("invalid float");

            if (beatLen < -MaxParseValue || beatLen > MaxParseValue)
                throw new LineError("number out of range");

            double speedMultiplier = beatLen < 0.0 ? 100.0 / -beatLen : 1.0;

            if (split.Length > 2 && ParseI32(split[2]) < 1)
                throw new LineError("invalid time signature");

            // [3] sample set, [4] custom sample bank, [5] sample volume
            bool timingChange = split.Length <= 6 || (split[6].Length > 0 && split[6][0] == '1');

            bool kiai = false;

            if (split.Length > 7)
                kiai = (ParseI32Strict(split[7]) & 1) != 0;

            if (timingChange)
            {
                if (double.IsNaN(beatLen))
                    throw new LineError("beat length cannot be NaN in a timing control point");

                AddPendingTiming(time, new TimingPoint(time, beatLen), timingChange);
            }

            AddPendingDifficulty(time, new DifficultyPoint(time, beatLen, speedMultiplier), timingChange);

            var effect = new EffectPoint(time, kiai);

            if (mode == GameMode.Taiko || mode == GameMode.Mania)
                effect.ScrollSpeed = RustMath.Clamp(speedMultiplier, 0.01, 10.0);

            AddPendingEffect(time, effect, timingChange);

            pendingControlPointsTime = time;
        }

        private void ParseHitObjects(string line)
        {
            string[] split = TrimComment(line).Split(',');

            if (split.Length < 5)
                throw new LineError("invalid hit object line");

            var pos = new Pos(
                RustMath.ToI32(ParseF32(split[0], MaxCoordinateValue)),
                RustMath.ToI32(ParseF32(split[1], MaxCoordinateValue)));

            double startTime = ParseF64(split[2]);
            int hitObjectType = ParseI32Strict(split[3]);
            byte sound = ParseHitSound(split[4]);

            int next = 5;
            string? Next() => next < split.Length ? split[next++] : null;

            void ParseCustomSound(string? bankInfo)
            {
                if (string.IsNullOrEmpty(bankInfo))
                    return;

                string[] parts = bankInfo.Split(':');

                // normal bank, additional bank, custom sample bank, volume
                for (int i = 0; i < 4 && i < parts.Length; i++)
                    ParseI32(parts[i]);

                // filename
                // Relevant maps:
                //   - /b/244784 at 43374
                if (parts.Length > 4 && parts[4].Length > 0)
                    sound = (byte)(sound & ~1);
            }

            var h = new HitObject { Pos = pos, StartTime = startTime };

            if ((hitObjectType & 1) != 0)
            {
                ParseCustomSound(Next());
                h.Kind = HitObjectKind.Circle;
            }
            else if ((hitObjectType & 2) != 0)
            {
                string? pointStr = Next();
                string? repeatCount = Next();

                if (pointStr == null || repeatCount == null)
                    throw new LineError("invalid hit object line");

                double? len = null;

                int repeats = ParseI32(repeatCount);

                if (repeats > 9000)
                    throw new LineError("repeat count is way too high");

                repeats = Math.Max(0, repeats - 1);

                string? lenStr = Next();

                if (lenStr != null)
                {
                    double newLen = RustMath.Max(ParseF64(lenStr, MaxCoordinateValue), 0.0);

                    if (RustMath.NotEq(newLen, 0.0))
                        len = newLen;
                }

                string? nodeSoundsStr = Next();

                Next(); // node banks
                ParseCustomSound(Next());

                var nodeSounds = new byte[repeats + 2];

                for (int i = 0; i < nodeSounds.Length; i++)
                    nodeSounds[i] = sound;

                if (nodeSoundsStr != null)
                {
                    string[] soundParts = nodeSoundsStr.Split('|');

                    for (int i = 0; i < soundParts.Length && i < nodeSounds.Length; i++)
                        nodeSounds[i] = RustMath.TryParseI32(soundParts[i], out int s) ? (byte)(s & 0xFF) : (byte)0;
                }

                ConvertPathStr(pointStr, pos);

                h.Kind = HitObjectKind.Slider;
                h.Slider = new SliderData
                {
                    ExpectedDist = len,
                    Repeats = repeats,
                    ControlPoints = curvePoints.ToArray(),
                    NodeSounds = nodeSounds,
                };

                curvePoints.Clear();
            }
            else if ((hitObjectType & 8) != 0)
            {
                string endTimeStr = Next() ?? throw new LineError("invalid hit object line");
                double endTime = ParseF64(endTimeStr);

                ParseCustomSound(Next());

                h.Kind = HitObjectKind.Spinner;
                h.Duration = RustMath.Max(endTime - startTime, 0.0);
            }
            else if ((hitObjectType & 128) != 0)
            {
                double endTime;
                string? s = Next();

                if (!string.IsNullOrEmpty(s))
                {
                    int colon = s.IndexOf(':');

                    if (colon < 0)
                        throw new LineError("invalid hit object line");

                    ParseCustomSound(s.Substring(colon + 1));
                    endTime = RustMath.Max(ParseF64(s.Substring(0, colon)), startTime);
                }
                else
                {
                    endTime = startTime;
                }

                h.Kind = HitObjectKind.Hold;
                h.Duration = endTime - startTime;
            }
            else
            {
                throw new LineError("unknown hit object type");
            }

            hitObjects.Add(h);
            hitSounds.Add(sound);
        }

        // ---- slider path parsing ----

        private void ConvertPathStr(string pointStr, Pos offset)
        {
            string[] pointSplit = pointStr.Split('|');

            int startIdx = 0;
            int endIdx = 0;
            bool first = true;

            while (++endIdx < pointSplit.Length)
            {
                string piece = pointSplit[endIdx];

                if (piece.Length == 0)
                    throw new LineError("invalid hit object line");

                char c = piece[0];
                bool isLetter = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

                if (!isLetter)
                    continue;

                string? endPoint = endIdx + 1 < pointSplit.Length ? pointSplit[endIdx + 1] : null;
                ConvertPoints(pointSplit, startIdx, endIdx, endPoint, first, offset);

                startIdx = endIdx;
                first = false;
            }

            if (endIdx > startIdx)
                ConvertPoints(pointSplit, startIdx, endIdx, null, first, offset);
        }

        private static PathControlPoint ReadPoint(string value, Pos startPos)
        {
            string[] v = value.Split(':');

            if (v.Length < 2)
                throw new LineError("invalid hit object line");

            double x = ParseF64(v[0], MaxCoordinateValue);
            double y = ParseF64(v[1], MaxCoordinateValue);

            var pos = new Pos(RustMath.ToI32(x), RustMath.ToI32(y));

            return new PathControlPoint(pos - startPos);
        }

        private static bool IsLinear(Pos p0, Pos p1, Pos p2)
            => RustMath.AlmostEq((p1.Y - p0.Y) * (p2.X - p0.X), (p1.X - p0.X) * (p2.Y - p0.Y));

        /// <param name="pieces">All pieces of the path string; the points are <c>pieces[from..to]</c>.</param>
        private void ConvertPoints(string[] pieces, int from, int to, string? endPoint, bool first, Pos offset)
        {
            int count = to - from;

            if (count == 0)
                throw new LineError("invalid hit object line");

            PathType pathType = PathType.FromStr(pieces[from]);

            int endPointLen = endPoint != null ? 1 : 0;

            vertices.Clear();

            if (first)
                vertices.Add(default);

            for (int i = from + 1; i < to; i++)
                vertices.Add(ReadPoint(pieces[i], offset));

            if (endPoint != null)
                vertices.Add(ReadPoint(endPoint, offset));

            if (pathType == PathType.PerfectCurve)
            {
                if (vertices.Count == 3)
                {
                    if (IsLinear(vertices[0].Pos, vertices[1].Pos, vertices[2].Pos))
                        pathType = PathType.Linear;
                }
                else
                {
                    pathType = PathType.Bezier;
                }
            }

            if (vertices.Count == 0)
                throw new LineError("invalid hit object line");

            PathControlPoint firstVertex = vertices[0];
            firstVertex.PathType = pathType;
            vertices[0] = firstVertex;

            int startIdx = 0;
            int endIdx = 0;

            while (++endIdx < vertices.Count - endPointLen)
            {
                if (vertices[endIdx].Pos != vertices[endIdx - 1].Pos)
                    continue;

                if (pathType == PathType.Catmull && endIdx > 1)
                    continue;

                if (endIdx == vertices.Count - endPointLen - 1)
                    continue;

                PathControlPoint prev = vertices[endIdx - 1];
                prev.PathType = pathType;
                vertices[endIdx - 1] = prev;

                for (int i = startIdx; i < endIdx; i++)
                    curvePoints.Add(vertices[i]);

                startIdx = endIdx + 1;
            }

            if (endIdx > startIdx)
            {
                for (int i = startIdx; i < endIdx; i++)
                    curvePoints.Add(vertices[i]);
            }
        }

        // ---- control points ----

        private void AddPendingTiming(double time, TimingPoint point, bool timingChange)
        {
            if (RustMath.NotEq(time, pendingControlPointsTime))
                FlushPendingPoints();

            if (timingChange)
            {
                if (pendingTimingPoint == null)
                    pendingTimingPoint = point;
            }
            else
            {
                pendingTimingPoint = point;
            }

            pendingControlPointsTime = time;
        }

        private void AddPendingDifficulty(double time, DifficultyPoint point, bool timingChange)
        {
            if (RustMath.NotEq(time, pendingControlPointsTime))
                FlushPendingPoints();

            if (timingChange)
            {
                if (pendingDifficultyPoint == null)
                    pendingDifficultyPoint = point;
            }
            else
            {
                pendingDifficultyPoint = point;
            }

            pendingControlPointsTime = time;
        }

        private void AddPendingEffect(double time, EffectPoint point, bool timingChange)
        {
            if (RustMath.NotEq(time, pendingControlPointsTime))
                FlushPendingPoints();

            if (timingChange)
            {
                if (pendingEffectPoint == null)
                    pendingEffectPoint = point;
            }
            else
            {
                pendingEffectPoint = point;
            }

            pendingControlPointsTime = time;
        }

        private void FlushPendingPoints()
        {
            if (pendingTimingPoint is TimingPoint timing)
            {
                pendingTimingPoint = null;

                // Timing points are never redundant
                int res = ControlPointLookup.Search(timingPoints, timing.Time);
                if (res >= 0) timingPoints[res] = timing;
                else timingPoints.Insert(~res, timing);
            }

            if (pendingDifficultyPoint is DifficultyPoint difficulty)
            {
                pendingDifficultyPoint = null;

                DifficultyPoint existing = ControlPointLookup.DifficultyPointAt(difficultyPoints, difficulty.Time) ?? DifficultyPoint.Default;

                if (!difficulty.IsRedundant(existing))
                {
                    int res = ControlPointLookup.Search(difficultyPoints, difficulty.Time);
                    if (res >= 0) difficultyPoints[res] = difficulty;
                    else difficultyPoints.Insert(~res, difficulty);
                }
            }

            if (pendingEffectPoint is EffectPoint effect)
            {
                pendingEffectPoint = null;

                EffectPoint existing = ControlPointLookup.EffectPointAt(effectPoints, effect.Time) ?? EffectPoint.Default;

                if (!effect.IsRedundant(existing))
                {
                    int res = ControlPointLookup.Search(effectPoints, effect.Time);
                    if (res >= 0) effectPoints[res] = effect;
                    else effectPoints.Insert(~res, effect);
                }
            }
        }

        private Beatmap Finish()
        {
            FlushPendingPoints();

            float hp = RustMath.Clamp(hpDrainRate, 0f, 10f);

            // * mania uses "circle size" for key count, thus different allowable range
            float cs = mode == GameMode.Mania ? RustMath.Clamp(circleSize, 1f, 18f) : RustMath.Clamp(circleSize, 0f, 10f);

            float od = RustMath.Clamp(overallDifficulty, 0f, 10f);
            float ar = RustMath.Clamp(approachRate, 0f, 10f);

            double sm = RustMath.Clamp(sliderMultiplier, 0.4, 3.6);
            double tr = RustMath.Clamp(sliderTickRate, 0.5, 8.0);

            // Stable sort by start time (total order)
            var indices = new int[hitObjects.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;

            Array.Sort(indices, (a, b) =>
            {
                int c = RustMath.TotalCmp(hitObjects[a].StartTime, hitObjects[b].StartTime);
                return c != 0 ? c : a.CompareTo(b);
            });

            var sortedObjects = new List<HitObject>(hitObjects.Count);
            var sortedSounds = new List<byte>(hitSounds.Count);

            foreach (int i in indices)
            {
                sortedObjects.Add(hitObjects[i]);
                sortedSounds.Add(hitSounds[i]);
            }

            return new Beatmap
            {
                Version = version,
                StackLeniency = stackLeniency,
                Mode = mode,
                Ar = ar,
                Cs = cs,
                Hp = hp,
                Od = od,
                SliderMultiplier = sm,
                SliderTickRate = tr,
                Breaks = breaks,
                TimingPoints = timingPoints,
                DifficultyPoints = difficultyPoints,
                EffectPoints = effectPoints,
                HitObjects = sortedObjects,
                HitSounds = sortedSounds,
            };
        }
    }
}
