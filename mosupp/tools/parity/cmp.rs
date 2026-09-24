use std::io::{BufRead, Write};
use rosu_pp::{Beatmap, Difficulty, GameMods, osu::{OsuPerformance, OsuDifficultyAttributes}};
use rosu_pp::model::mods::rosu_mods::{GameMod, GameMode, GameMods as LazerMods, generated_mods::*};

fn opt<T: std::str::FromStr>(s: &str) -> Option<T> where T::Err: std::fmt::Debug { if s == "-" { None } else { Some(s.parse().unwrap()) } }

fn parse_mods(s: &str) -> GameMods {
    if let Some(bits) = s.strip_prefix("L:") { return GameMods::from(bits.parse::<u32>().unwrap()); }
    let spec = s.strip_prefix("Z:").unwrap();
    let mut mods = LazerMods::new();
    for tok in spec.split(',').filter(|t| !t.is_empty()) {
        let (ac, val) = match tok.split_once('=') { Some((a, v)) => (a, Some(v)), None => (tok, None) };
        let m = match (ac, val) {
            ("DT", Some(v)) => GameMod::DoubleTimeOsu(DoubleTimeOsu { speed_change: Some(v.parse().unwrap()), ..Default::default() }),
            ("NC", Some(v)) => GameMod::NightcoreOsu(NightcoreOsu { speed_change: Some(v.parse().unwrap()) }),
            ("HT", Some(v)) => GameMod::HalfTimeOsu(HalfTimeOsu { speed_change: Some(v.parse().unwrap()), ..Default::default() }),
            ("DC", Some(v)) => GameMod::DaycoreOsu(DaycoreOsu { speed_change: Some(v.parse().unwrap()) }),
            ("CL", Some(v)) => GameMod::ClassicOsu(ClassicOsu { no_slider_head_accuracy: Some(v.parse().unwrap()), ..Default::default() }),
            ("MR", Some(v)) => GameMod::MirrorOsu(MirrorOsu { reflection: Some(v.to_owned()) }),
            ("MG", Some(v)) => GameMod::MagnetisedOsu(MagnetisedOsu { attraction_strength: Some(v.parse().unwrap()) }),
            ("DF", Some(v)) => GameMod::DeflateOsu(DeflateOsu { start_scale: Some(v.parse().unwrap()) }),
            ("HD", Some(v)) => GameMod::HiddenOsu(HiddenOsu { only_fade_approach_circles: Some(v.parse().unwrap()) }),
            ("DA", Some(v)) => {
                let mut da = DifficultyAdjustOsu::default();
                for kv in v.split(';') { let (k, x) = kv.split_once(':').unwrap(); let x: f64 = x.parse().unwrap();
                    match k { "cs" => da.circle_size = Some(x), "ar" => da.approach_rate = Some(x), "hp" => da.drain_rate = Some(x), "od" => da.overall_difficulty = Some(x), _ => panic!() } }
                GameMod::DifficultyAdjustOsu(da)
            }
            (a, None) => GameMod::new(a, GameMode::Osu),
            _ => panic!("bad mod {tok}"),
        };
        mods.insert(m);
    }
    GameMods::from(mods)
}

fn attrs_line(a: &OsuDifficultyAttributes) -> String {
    format!("{:?} {:?} {:?} {:?} {:?} {:?} {:?} {:?} {:?} {:?} {:?} {:?} {:?} {:?} {:?} {:?} {} {} {} {} {} {:?} {:?} {:?}",
        a.stars, a.aim, a.speed, a.flashlight, a.slider_factor, a.speed_note_count, a.aim_difficult_slider_count,
        a.aim_difficult_strain_count, a.speed_difficult_strain_count, a.aim_top_weighted_slider_factor, a.speed_top_weighted_slider_factor,
        a.ar, a.great_hit_window, a.ok_hit_window, a.meh_hit_window, a.hp, a.n_circles, a.n_sliders, a.n_large_ticks, a.n_spinners, a.max_combo,
        a.nested_score_per_object, a.legacy_score_base_multiplier, a.maximum_legacy_combo_score)
}

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let f = std::fs::File::open(&args[1]).unwrap();
    let out = std::io::stdout();
    let mut out = out.lock();
    let mut cache: std::collections::HashMap<String, Beatmap> = Default::default();
    for line in std::io::BufReader::new(f).lines() {
        let line = line.unwrap();
        if line.trim().is_empty() || line.starts_with('#') { continue; }
        let c: Vec<&str> = line.split('\t').collect();
        let map = cache.entry(c[0].to_owned()).or_insert_with(|| Beatmap::from_path(c[0]).unwrap());
        let mods = parse_mods(c[1]);
        let mut diff = Difficulty::new().mods(mods);
        if let Some(x) = opt::<f64>(c[2]) { diff = diff.clock_rate(x); }
        if let Some(x) = opt::<u32>(c[10]) { diff = diff.passed_objects(x); }
        if let Some(x) = opt::<bool>(c[9]) { diff = diff.lazer(x); }
        if let Some(x) = opt::<f32>(c[15]) { diff = diff.ar(x, false); }
        if let Some(x) = opt::<f32>(c[16]) { diff = diff.od(x, false); }
        if let Some(x) = opt::<f32>(c[17]) { diff = diff.cs(x, true); }
        let attrs = diff.calculate(map);
        let rosu_pp::any::DifficultyAttributes::Osu(attrs) = attrs else { panic!() };
        let mut perf = OsuPerformance::from(attrs.clone()).difficulty(diff.clone());
        if let Some(x) = opt::<f64>(c[3]) { perf = perf.accuracy(x); }
        if let Some(x) = opt::<u32>(c[4]) { perf = perf.n300(x); }
        if let Some(x) = opt::<u32>(c[5]) { perf = perf.n100(x); }
        if let Some(x) = opt::<u32>(c[6]) { perf = perf.n50(x); }
        if let Some(x) = opt::<u32>(c[7]) { perf = perf.misses(x); }
        if let Some(x) = opt::<u32>(c[8]) { perf = perf.combo(x); }
        if let Some(x) = opt::<u32>(c[11]) { perf = perf.large_tick_hits(x); }
        if let Some(x) = opt::<u32>(c[12]) { perf = perf.slider_end_hits(x); }
        if let Some(x) = opt::<u32>(c[13]) { perf = perf.small_tick_hits(x); }
        if let Some(x) = opt::<u32>(c[14]) { perf = perf.legacy_total_score(x); }
        let mut p2 = perf.clone();
        let st = p2.generate_state().unwrap();
        let r = perf.calculate().unwrap();
        writeln!(out, "{} | {:?} {:?} {:?} {:?} {:?} {:?} {:?} {:?} {:?} {:?} {:?} {:?} | {} {} {} {} {} {} {} {}",
            attrs_line(&attrs), r.pp, r.pp_aim, r.pp_speed, r.pp_acc, r.pp_flashlight, r.effective_miss_count,
            r.speed_deviation.unwrap_or(f64::NAN), r.combo_based_estimated_miss_count, r.score_based_estimated_miss_count.unwrap_or(f64::NAN),
            r.aim_estimated_slider_breaks, r.speed_estimated_slider_breaks, 0.0,
            st.max_combo, st.hitresults.n300, st.hitresults.n100, st.hitresults.n50, st.hitresults.misses,
            st.hitresults.large_tick_hits, st.hitresults.slider_end_hits, st.hitresults.small_tick_hits).unwrap();
    }
}
