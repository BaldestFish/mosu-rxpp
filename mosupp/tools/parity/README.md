# Parity check against the original Rust rosu-pp

1. Put `cmp.rs` into `rosu-pp/examples/` (rosu-pp 4.0.1) and build: `cargo build --release --example cmp`.
2. Generate test cases: `python3 gen_cases.py full <folder-with-.osu-files> > cases.tsv`
   (columns: path, mods, clock, acc, n300, n100, n50, misses, combo, lazer, passed, large ticks,
   slider ends, small ticks, legacy score, ar, od, cs(fixed); `-` = not set;
   mods: `L:<legacy bits>` or `Z:RX,HD,DT=1.3,DA=ar:9.5;cs:4,CL=false,MR=2,...`).
3. Run Rust: `rosu-pp/target/release/examples/cmp cases.tsv > rust.txt`
4. Run C#: build `Harness.cs` as a console app referencing MosuPp, `Harness cases.tsv > cs.txt`
5. Compare: `python3 compare.py rust.txt cs.txt cases.tsv`

`fuzz.py` / `fuzz2.py` generate random (and deliberately malformed) maps for broader coverage.
