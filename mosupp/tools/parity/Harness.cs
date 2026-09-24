using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MosuPp;
using MosuPp.Model;
using MosuPp.Osu;

public static class Program
{
    static string F(double d) => double.IsNaN(d) ? "NaN" : d.ToString("R", CultureInfo.InvariantCulture);

    static T? Opt<T>(string s, Func<string, T> p) where T : struct => s == "-" ? null : p(s);

    static GameMods ParseMods(string s)
    {
        if (s.StartsWith("L:")) return GameMods.FromLegacy(uint.Parse(s.Substring(2)));
        var list = new List<LazerMod>();
        foreach (var tok in s.Substring(2).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = tok.IndexOf('=');
            if (eq < 0) { list.Add(new LazerMod(tok)); continue; }
            string ac = tok.Substring(0, eq), v = tok.Substring(eq + 1);
            var set = new Dictionary<string, object>();
            double D(string x) => double.Parse(x, CultureInfo.InvariantCulture);
            switch (ac)
            {
                case "DT": case "NC": case "HT": case "DC": set["speed_change"] = D(v); break;
                case "CL": set["no_slider_head_accuracy"] = bool.Parse(v); break;
                case "MR": set["reflection"] = v; break;
                case "MG": set["attraction_strength"] = D(v); break;
                case "DF": set["start_scale"] = D(v); break;
                case "HD": set["only_fade_approach_circles"] = bool.Parse(v); break;
                case "DA":
                    foreach (var kv in v.Split(';')) { var p = kv.Split(':'); set[p[0] switch { "cs" => "circle_size", "ar" => "approach_rate", "hp" => "drain_rate", _ => "overall_difficulty" }] = D(p[1]); }
                    break;
            }
            list.Add(new LazerMod(ac, set));
        }
        return GameMods.FromLazer(list);
    }

    public static void Main(string[] args)
    {

        var cache = new Dictionary<string, Beatmap>();
        var sb = new StringBuilder();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var line in File.ReadLines(args[0]))
        {
            if (line.Trim().Length == 0 || line.StartsWith("#")) continue;
            var c = line.Split('\t');
            if (!cache.TryGetValue(c[0], out var map)) cache[c[0]] = map = Beatmap.FromPath(c[0]);
            var diff = new Difficulty().Mods(ParseMods(c[1]));
            var inv = CultureInfo.InvariantCulture;
            if (c[2] != "-") diff.ClockRate(double.Parse(c[2], inv));
            if (c[10] != "-") diff.PassedObjects(uint.Parse(c[10]));
            if (c[9] != "-") diff.Lazer(bool.Parse(c[9]));
            if (c[15] != "-") diff.Ar(float.Parse(c[15], inv), false);
            if (c[16] != "-") diff.Od(float.Parse(c[16], inv), false);
            if (c[17] != "-") diff.Cs(float.Parse(c[17], inv), true);
            var a = diff.Calculate(map);
            var perf = new OsuPerformance(a.Clone()).Difficulty(diff);
            if (c[3] != "-") perf.Accuracy(double.Parse(c[3], inv));
            if (c[4] != "-") perf.N300(uint.Parse(c[4]));
            if (c[5] != "-") perf.N100(uint.Parse(c[5]));
            if (c[6] != "-") perf.N50(uint.Parse(c[6]));
            if (c[7] != "-") perf.Misses(uint.Parse(c[7]));
            if (c[8] != "-") perf.Combo(uint.Parse(c[8]));
            if (c[11] != "-") perf.LargeTickHits(uint.Parse(c[11]));
            if (c[12] != "-") perf.SliderEndHits(uint.Parse(c[12]));
            if (c[13] != "-") perf.SmallTickHits(uint.Parse(c[13]));
            if (c[14] != "-") perf.LegacyTotalScore(uint.Parse(c[14]));
            var r = perf.Calculate();
            var st = r.State;
            sb.Append(string.Join(" ", new[] { F(a.Stars), F(a.Aim), F(a.Speed), F(a.Flashlight), F(a.SliderFactor), F(a.SpeedNoteCount), F(a.AimDifficultSliderCount),
                F(a.AimDifficultStrainCount), F(a.SpeedDifficultStrainCount), F(a.AimTopWeightedSliderFactor), F(a.SpeedTopWeightedSliderFactor),
                F(a.Ar), F(a.GreatHitWindow), F(a.OkHitWindow), F(a.MehHitWindow), F(a.Hp), a.NCircles.ToString(), a.NSliders.ToString(), a.NLargeTicks.ToString(),
                a.NSpinners.ToString(), a.MaxCombo.ToString(), F(a.NestedScorePerObject), F(a.LegacyScoreBaseMultiplier), F(a.MaximumLegacyComboScore) }));
            sb.Append(" | ");
            sb.Append(string.Join(" ", new[] { F(r.Pp), F(r.PpAim), F(r.PpSpeed), F(r.PpAcc), F(r.PpFlashlight), F(r.EffectiveMissCount), F(r.SpeedDeviation ?? double.NaN),
                F(r.ComboBasedEstimatedMissCount), F(r.ScoreBasedEstimatedMissCount ?? double.NaN), F(r.AimEstimatedSliderBreaks), F(r.SpeedEstimatedSliderBreaks), "0" }));
            sb.Append(" | ");
            sb.Append($"{st.MaxCombo} {st.HitResults.N300} {st.HitResults.N100} {st.HitResults.N50} {st.HitResults.Misses} {st.HitResults.LargeTickHits} {st.HitResults.SliderEndHits} {st.HitResults.SmallTickHits}");
            sb.Append('\n');
        }
        Console.Write(sb.ToString());
        Console.Error.WriteLine($"elapsed {sw.ElapsedMilliseconds}ms");
    }
}
