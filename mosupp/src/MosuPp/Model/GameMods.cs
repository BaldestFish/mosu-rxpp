using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace MosuPp.Model
{
    /// <summary>Legacy (osu!stable) mod bitflags. See https://github.com/ppy/osu-api/wiki#mods</summary>
    [Flags]
    public enum LegacyMods : uint
    {
        None = 0,
        NoFail = 1 << 0,
        Easy = 1 << 1,
        TouchDevice = 1 << 2,
        Hidden = 1 << 3,
        HardRock = 1 << 4,
        SuddenDeath = 1 << 5,
        DoubleTime = 1 << 6,
        Relax = 1 << 7,
        HalfTime = 1 << 8,

        /// <summary>Note: as in osu!stable, Nightcore also contains the DoubleTime bit.</summary>
        Nightcore = (1 << 9) | DoubleTime,
        Flashlight = 1 << 10,
        Autoplay = 1 << 11,
        SpunOut = 1 << 12,
        Autopilot = 1 << 13,
        Perfect = (1 << 14) | SuddenDeath,
        FadeIn = 1 << 20,
        Random = 1 << 21,
        Cinema = 1 << 22,
        Target = 1 << 23,
        ScoreV2 = 1 << 29,
        Mirror = 1u << 30,
    }

    /// <summary>A single osu!lazer mod with its (optional) settings, e.g. <c>DT</c> with <c>speed_change = 1.3</c>.</summary>
    public sealed class LazerMod
    {
        public string Acronym { get; }

        /// <summary>Setting values are <see cref="double"/>, <see cref="bool"/> or <see cref="string"/>.</summary>
        public IReadOnlyDictionary<string, object> Settings { get; }

        public LazerMod(string acronym, IReadOnlyDictionary<string, object>? settings = null)
        {
            Acronym = acronym.ToUpperInvariant();
            Settings = settings ?? new Dictionary<string, object>();
        }

        public double? GetDouble(string key)
        {
            if (!Settings.TryGetValue(key, out object? v))
                return null;

            return v switch
            {
                double d => d,
                float f => f,
                int i => i,
                long l => l,
                string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) => parsed,
                _ => null,
            };
        }

        public bool? GetBool(string key)
        {
            if (!Settings.TryGetValue(key, out object? v))
                return null;

            return v switch
            {
                bool b => b,
                string s when bool.TryParse(s, out bool parsed) => parsed,
                _ => null,
            };
        }

        public string? GetString(string key)
        {
            if (!Settings.TryGetValue(key, out object? v))
                return null;

            return v switch
            {
                string s => s,
                double d => d.ToString(CultureInfo.InvariantCulture),
                int i => i.ToString(CultureInfo.InvariantCulture),
                long l => l.ToString(CultureInfo.InvariantCulture),
                bool b => b ? "true" : "false",
                _ => v.ToString(),
            };
        }

        public override string ToString() => Settings.Count == 0 ? Acronym : $"{Acronym}({string.Join(", ", Settings.Select(kv => $"{kv.Key}={kv.Value}"))})";
    }

    internal enum Reflection
    {
        None,
        Vertical,
        Horizontal,
        Both,
    }

    /// <summary>
    /// Collection of mods, either legacy bitflags or osu!lazer mods (with settings).
    /// Mirrors rosu-pp's <c>GameMods</c> enum (the <c>Intermode</c> variant is represented as lazer mods without settings).
    /// </summary>
    public sealed class GameMods
    {
        private readonly LegacyMods legacyBits;
        private readonly List<LazerMod>? lazerMods;

        private GameMods(LegacyMods bits)
        {
            legacyBits = bits;
        }

        private GameMods(List<LazerMod> mods)
        {
            lazerMods = mods;
        }

        public static GameMods NoMod => new GameMods(LegacyMods.None);

        /// <summary>True if these are osu!lazer mods, false for legacy bitflags.</summary>
        public bool IsLazer => lazerMods != null;

        public LegacyMods LegacyBits => legacyBits;

        public IReadOnlyList<LazerMod> Lazer => lazerMods ?? (IReadOnlyList<LazerMod>)Array.Empty<LazerMod>();

        /// <summary>Legacy mods from bitflags, e.g. <c>FromLegacy(128 | 8)</c> for RXHD.</summary>
        public static GameMods FromLegacy(uint bits) => new GameMods((LegacyMods)bits);

        public static GameMods FromLegacy(LegacyMods bits) => new GameMods(bits);

        /// <summary>osu!lazer mods.</summary>
        public static GameMods FromLazer(IEnumerable<LazerMod> mods) => new GameMods(mods.ToList());

        /// <summary>
        /// osu!lazer mods from acronyms without settings, e.g. <c>"RXHDDT"</c> or <c>"RX,HD,DT"</c>.
        /// </summary>
        public static GameMods FromAcronyms(string acronyms)
        {
            var mods = new List<LazerMod>();
            string cleaned = new string(acronyms.Where(c => !char.IsWhiteSpace(c)).ToArray());

            foreach (string part in cleaned.Split(new[] { ',', '+', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string upper = part.ToUpperInvariant();

                // All osu! acronyms are 2 chars except e.g. "SV2" and mania key mods ("4K")
                int i = 0;

                while (i < upper.Length)
                {
                    int len = i + 3 <= upper.Length && upper.Substring(i, 3) == "SV2" ? 3 : 2;

                    if (i + len > upper.Length)
                        throw new FormatException($"Invalid mod acronyms: '{acronyms}'");

                    string acronym = upper.Substring(i, len);

                    if (acronym != "NM")
                        mods.Add(new LazerMod(acronym));

                    i += len;
                }
            }

            return new GameMods(mods);
        }

        /// <summary>
        /// osu!lazer mods from the API/score JSON format:
        /// <c>[{"acronym":"DT","settings":{"speed_change":1.3}},{"acronym":"RX"}]</c>.
        /// </summary>
        public static GameMods FromJson(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var mods = new List<LazerMod>();

            foreach (JsonElement el in doc.RootElement.EnumerateArray())
            {
                string acronym = el.GetProperty("acronym").GetString() ?? throw new FormatException("missing acronym");
                var settings = new Dictionary<string, object>();

                if (el.TryGetProperty("settings", out JsonElement s) && s.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty prop in s.EnumerateObject())
                    {
                        object? value = prop.Value.ValueKind switch
                        {
                            JsonValueKind.Number => prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.String => prop.Value.GetString(),
                            _ => null,
                        };

                        if (value != null)
                            settings[prop.Name] = value;
                    }
                }

                mods.Add(new LazerMod(acronym, settings));
            }

            return new GameMods(mods);
        }

        public static implicit operator GameMods(LegacyMods bits) => FromLegacy(bits);

        private LazerMod? Find(string acronym)
        {
            if (lazerMods == null)
                return null;

            foreach (LazerMod m in lazerMods)
            {
                if (m.Acronym == acronym)
                    return m;
            }

            return null;
        }

        private bool HasLazer(string acronym) => Find(acronym) != null;

        private bool HasLegacy(LegacyMods flag) => (legacyBits & flag) == flag;

        private bool Has(string acronym, LegacyMods? legacyFlag) => lazerMods != null ? HasLazer(acronym) : legacyFlag.HasValue && HasLegacy(legacyFlag.Value);

        internal bool Nf => Has("NF", LegacyMods.NoFail);
        internal bool Ez => Has("EZ", LegacyMods.Easy);
        internal bool Td => Has("TD", LegacyMods.TouchDevice);
        internal bool Hd => Has("HD", LegacyMods.Hidden);
        internal bool Hr => Has("HR", LegacyMods.HardRock);
        internal bool Rx => Has("RX", LegacyMods.Relax);
        internal bool Fl => Has("FL", LegacyMods.Flashlight);
        internal bool So => Has("SO", LegacyMods.SpunOut);
        internal bool Ap => Has("AP", LegacyMods.Autopilot);
        internal bool Sv2 => Has("SV2", LegacyMods.ScoreV2);
        internal bool Bl => Has("BL", null);
        internal bool Cl => Has("CL", null);
        internal bool Tc => Has("TC", null);

        /// <summary>Whether Relax is enabled.</summary>
        public bool IsRelax => Rx;

        /// <summary>
        /// The mods' clock rate. Legacy: 1.5 for DT (bit 64, which NC includes), 0.75 for HT.
        /// Lazer: DT/NC/HT/DC with their <c>speed_change</c> setting.
        /// </summary>
        public double ClockRate()
        {
            if (lazerMods == null)
            {
                if (HasLegacy(LegacyMods.DoubleTime))
                    return 1.5;

                if (HasLegacy(LegacyMods.HalfTime))
                    return 0.75;

                return 1.0;
            }

            foreach (LazerMod m in lazerMods)
            {
                switch (m.Acronym)
                {
                    case "DT":
                        return m.GetDouble("speed_change") ?? 1.5;
                    case "HT":
                        return m.GetDouble("speed_change") ?? 0.75;
                    case "NC":
                    {
                        const double def = 1.5;
                        return def * ((m.GetDouble("speed_change") ?? 1.5) / def);
                    }
                    case "DC":
                    {
                        const double def = 0.75;
                        return def * ((m.GetDouble("speed_change") ?? 0.75) / def);
                    }
                }
            }

            return 1.0;
        }

        internal bool NoSliderHeadAcc(bool lazer)
        {
            if (lazerMods == null)
                return !lazer;

            LazerMod? cl = Find("CL");

            if (cl != null)
                return cl.GetBool("no_slider_head_accuracy") ?? true;

            return !lazer;
        }

        internal Reflection Reflection()
        {
            if (lazerMods == null)
                return HasLegacy(LegacyMods.HardRock) ? Model.Reflection.Vertical : Model.Reflection.None;

            foreach (LazerMod m in lazerMods)
            {
                if (m.Acronym == "HR")
                    return Model.Reflection.Vertical;

                if (m.Acronym == "MR")
                {
                    // Same mapping as rosu-pp.
                    return m.GetString("reflection") switch
                    {
                        null => Model.Reflection.Horizontal,
                        "1" => Model.Reflection.Vertical,
                        "2" => Model.Reflection.Both,
                        _ => Model.Reflection.None,
                    };
                }
            }

            return Model.Reflection.None;
        }

        internal double? AttractionStrength() => Find("MG")?.GetDouble("attraction_strength");

        internal double? DeflateStartScale() => Find("DF")?.GetDouble("start_scale");

        internal bool? HdOnlyFadeApproachCircles() => Find("HD")?.GetBool("only_fade_approach_circles");

        /// <summary>DifficultyAdjust overrides (lazer only): (cs, ar, hp, od).</summary>
        internal LazerMod? DifficultyAdjust() => Find("DA");

        public override string ToString()
        {
            if (lazerMods != null)
                return lazerMods.Count == 0 ? "NM" : string.Join("", lazerMods.Select(m => m.ToString()));

            return legacyBits == LegacyMods.None ? "NM" : legacyBits.ToString();
        }
    }
}
