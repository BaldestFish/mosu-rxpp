# MosuPp changelog (Relax / RX only)

> MosuPp ships its own calculator (osu.Game.Rulesets.Osu/Difficulty/Relax/MosuPpRelax): an exact copy of the MosuPp
> development calculator (Realistik core + all rules below), so MosuPp PP is identical to the development build.
> The "Mosu" and "Lazer (vanilla)" systems are unchanged and do not use it.

> Score (osu!, all PP systems) — shipped in the same patch:
> - Relax (RX) has no score penalty any more: score multiplier 0.1x → 1.0x (OsuScoreMultiplierCalculatorV2 only; V1 keeps
>   0.1x because it describes how old scores were stored and is used to migrate them).
> - Difficulty Adjust (DA) score multiplier, per 0.1 of change: CS +0.004x raised / −0.03x lowered, OD +0.001x raised /
>   −0.02x lowered, AR −0.005x either way, HP no effect; each part and the product at least 0.1x.

MosuPp = Mosu/Realistik RX calculator + the rules below. Vanilla PP and star rating are not changed.
Version names: **MosuPp X.Y.Z** — X = full rework, Y = a rule added or removed, Z = numbers changed in existing rules.
The "cache" number is `ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION`; it goes up by 1 with every change so cached PP is recalculated.
Examples are SS scores, RX, with all rules of that version.

## MosuPp 1.13.0 (cache 25)

- **Simple Stream Nerf (RX)**: **removed**.
- **Stream Map Nerf (RX)** (new): every stream map, whatever its stream shapes, total × (1 − 0.50 · smoothstep(SS speed/aim,
  1.0, 1.3) · smoothstep(stream share, 30%, 60%)).
- **Global PP Scale (RX)**: **removed** (the +10% of 1.11.0).
  Examples (SS, NM / DT, 1.12.3 → 1.13.0): Expurget (/b/4565459, 86% streams) 745 / 1999 → 339 / 909;
  A Tale Of Salt And Light (/b/5571670, 70%) 824 / 2493 → 542 / 1133; Worms of Soul (/b/5245120) 375 / 1186 → 341 / 1079;
  heat (/b/4303461) 423 / 1135 → 379 / 1013; drivers license [full] (/b/4566445, jump map with streams) 1601 / 4924 → 1455 / 4477 (only −10% scale);
  all other maps −9% (the removed +10%): glass beach (/b/5870318) 2832 / 8768 → 2575 / 7971, Attack (/b/5637274) 3401 / 8658 → 3092 / 7871.

## MosuPp 1.12.3 (cache 24)

- **Simple Stream Nerf (RX)**: max nerf raised from −35% to **−50%**.
  Worms of Soul (/b/5245120) SS NM / DT 487 / 1542 → 375 / 1186. A Tale Of Salt And Light (/b/5571670) 824 / 2493,
  heat (/b/4303461), Novae Ruptis (/b/3802844), drivers license (/b/4566445) and jump maps ±0% (not simple streams).

## MosuPp 1.12.2 (cache 23)

- **Simple Stream Nerf (RX)**: stream-map gate moved from speed/aim 1.3–1.65 to **1.1–1.35**, so NM stream maps get the
  full nerf too. Worms of Soul (/b/5245120) SS NM / DT 707 / 1542 → 487 / 1542. All other test maps ±0%
  (drivers license, A Tale Of Salt And Light, Novae Ruptis keep ±0% because their streams are not simple).

## MosuPp 1.12.1 (cache 22)

- **Simple Stream Nerf (RX)**: max nerf raised from −20% to **−35%**.
- **Spike Nerf (RX)**: max nerf lowered from −30% to **−20%** (maps with one hard part and filler).
  Examples (SS, NM / DT, 1.12.0 → 1.12.1): Attack (/b/5637274) 2976 / 7577 → 3401 / 8658 (+14%);
  Lament (/b/5826020) 2263 / 6597 → 2452 / 7278 (+8% / +10%); mcr (/b/5326952) +3%; Sky of Twilight (/b/5208003), Juliet (/b/4543721) +1%;
  Worms of Soul (/b/5245120) 623 / 1787 → 707 / 1542 (NM +13%: speed/aim 1.39 only half-opens the stream-map gate and the
  weaker Spike Nerf helps it; DT −14%). Other test maps ±0%.

## MosuPp 1.12.0 (cache 21)

- **Simple Stream Nerf (RX)**: max nerf lowered from −30% to **−20%**.
- **Low CS Nerf (RX)**: **removed**.
- **CS PP Buff (RX)**: **restored with half the bonus**: CS ≤ 3.5 +0%, 4 +5%, 5 +10%, 6 +15%, 7 +25%, 8 +50%, 9 +100%, 10 +200%
  (same smooth PCHIP curve, CS incl. HR/EZ/DA).
  Examples (SS, NM / DT, 1.11.0 → 1.12.0): Worms of Soul (/b/5245120, CS 4.4) 542 / 1457 → 623 / 1787;
  Attack (/b/5637274, CS 5) 2705 / 6888 → 2976 / 7577; Spider-Man (/b/4784320, CS 6.1) 935 / 2766 → 1081 / 3200;
  glass beach (/b/5870318, CS 4) 2697 / 8350 → 2832 / 8768; Sky of Twilight (/b/5208003, CS 3.6) 1298 / 4050 → 1366 / 4263;
  WE ♥ YURI (/b/5377716, CS 3.5) 1995 / 6424 → 2100 / 6762; drivers license [full] (/b/4566445, CS 2.6) 1313 / 4038 → 1601 / 4924.

## MosuPp 1.11.0 (cache 20)

- **Point Variety Nerf (RX)**: **restored** (same as 1.9.0). Kami no Kotoba small CS (/b/2589130) 971 / 2265 → 218 / 546.
- **Heavy Miss Penalty (RX)** (new): total × e^(−0.11 · max(0, misses − 15)) on top of the base miss penalty
  (replaces the removed Low Accuracy Nerf as the main punishment for bad plays). Sky of Twilight (/b/5208003), share of
  the SS PP kept: 15 misses 41% (unchanged), 20 → 20%, 25 → 10%, 30 → 5%.
- **Low CS Nerf (RX)** (new): total × CS 4+ 1.00, 3.5 0.95, 3 0.90, 2.5 0.80, ≤ 2 0.70 (linear in between, CS incl. HR/EZ).
- **Global PP Scale (RX)** (new): every MosuPp score ×1.10.
  Examples (SS, NM / DT, 1.10.0 → 1.11.0): glass beach (/b/5870318, CS 4) 2452 / 7591 → 2697 / 8350 (+10%);
  jump pack 3 (/b/2303920) 2222 / 7272 → 2445 / 7999; Attack (/b/5637274) 2459 / 6262 → 2705 / 6888;
  Sky of Twilight (/b/5208003, CS 3.6) 1229 / 3835 → 1298 / 4050 (+6%); WE ♥ YURI (/b/5377716, CS 3.5) 1909 / 6148 → 1995 / 6424;
  Crazy banger (/b/2562794, CS 3) 1263 / 3285 → 1251 / 3252 (−1%); drivers license [full] (/b/4566445, CS 2.6)
  1455 / 4477 → 1313 / 4038 (−10%).

## MosuPp 1.10.0 (cache 19)

- **CS PP Buff (RX)**: **removed** (CS no longer multiplies the PP; high-CS maps rely on the base calculator only).
- **Low Accuracy Nerf (RX)**: **removed** (scores below 75% accuracy are no longer cut extra).
- **Point Variety Nerf (RX)**: **removed**.
  Examples (SS, NM / DT, 1.9.0 → 1.10.0): Attack (/b/5637274, CS 5) 2951 / 7515 → 2459 / 6262;
  Spider-Man (/b/4784320, CS 6.1) 1117 / 3304 → 850 / 2514; glass beach (/b/5870318) 2697 / 8350 → 2452 / 7591;
  jump pack 3 (/b/2303920) 2445 / 7999 → 2222 / 7272; Novae Ruptis (/b/3802844) 1932 / 5891 → 1756 / 5355;
  Kami no Kotoba small CS (/b/2589130) 273 / 686 → 971 / 2265 (was held down by Point Variety Nerf);
  WE ♥ YURI (/b/5377716), drivers license (/b/4566443, /b/4566445), Crazy banger (/b/2562794) ±0% (CS ≤ 3.5).

## MosuPp 1.9.0 (cache 18)

- **Short High CS Nerf (RX)** (new): total × (1 − 0.15 · smoothstep(CS, 5, 6) · (1 − smoothstep(objects, 300, 700))).
  Smooth in both CS and length (no cliff at CS 6): CS 6+ → −15% at ≤ 300 objects, −7.5% at 500, ±0% from 700;
  CS 5.5 → half; CS ≤ 5 → ±0%. CS includes HR/EZ.
  Spider-Man Theme (/b/4784320, CS 6.1, 256 objects) SS NM / DT 1314 / 3887 → 1117 / 3304 (−15%);
  Kami no Kotoba small CS (/b/2589130, CS 6.5, 46 objects) 321 / 807 → 273 / 686 (−15%).
  Not touched: Attack (/b/5637274, CS 5, 500 objects) and all other test maps (CS ≤ 5).

## MosuPp 1.8.0 (cache 17)

- **Extreme Jump Nerf (RX)** (new): extreme jump = move between two objects with cursor velocity ≥ 4.5 px/ms
  (measured at clock rate 1, so NM / DT / HR get the same multiplier). Aim PP × (1 − 0.31 · smoothstep(extreme jumps, 80, 180)).
  glass beach (/b/5870318, 180 extreme jumps) SS NM / DT 3859 / 11881 → 2697 / 8350 (−30%).
  Not touched (±0%): Attack (/b/5637274, 60), Lament (/b/5826020, 51), Kami no Kotoba (/b/2589130, 43),
  drivers license (/b/4566443, /b/4566445, 20), Novae Ruptis (/b/3802844), jump pack 3 (/b/2303920), YURI (/b/5377716),
  Sky of Twilight (/b/5208003), mcr (/b/5326952) and all other test maps.

## MosuPp 1.7.0 (cache 16)

- **Length Bonus (RX)**: max bonus lowered from +60% to **+20%**.
- **Spike Nerf (RX)**: max nerf lowered from −45% to **−30%**.
- **Short Aim Nerf (RX)**: **removed**.
  Examples (SS, NM / DT, before → after): jump pack 3 (/b/2303920) 3148 / 10503 → 2445 / 7999;
  glass beach (/b/5870318) 4008 / 12417 → 3859 / 11881; Expurget (/b/4565459) 777 / 2204 → 678 / 1818;
  WE ♥ YURI (/b/5377716) 2484 / 8138 → 1909 / 6148; A Tale Of Salt And Light (/b/5571670) 877 / 2731 → 773 / 2337;
  Worms of Soul (/b/5245120) 447 / 1440 → 564 / 1518; Attack (/b/5637274) 2212 / 5668 → 2951 / 7515.

## MosuPp 1.6.0 (cache 15)

- **Traceable = Hidden (RX)** (new): the Traceable mod (TC) is calculated exactly like Hidden (HD) — same reading PP,
  same HD reading guard, same PP for TC and HD scores (also with DT etc.). MosuRealistik unchanged.

## MosuPp 1.5.0 (cache 14)

- **Short Aim Nerf (RX)** (new): total × (1 − 0.10 · (1 − smoothstep(objects, 300, 700))
  · (1 − smoothstep(SS speed PP / aim PP, 0.5, 0.8))). Short aim-only maps −10% (bbydoll, Harumachi Clover,
  drivers license [aim], Spider-Man, Kami no Kotoba), Attack −5%, Crazy banger −2%; long maps and stream/mixed maps ±0%.
  By object count, so NM and DT are nerfed the same.

## MosuPp 1.4.1 (cache 13)

- **Map-type decisions use the SS reference**: Aim-Focused Flow Guard, Simple Stream Nerf and Stream-Only Guard now
  read speed PP / aim PP from an SS on the same map + mods (cached per map + mods) instead of the score's own values.
  Accuracy and misses no longer move a score across a rule threshold. SS results are unchanged.

## MosuPp 1.4.0 (cache 12)

- **One-Point Map Guard (RX)** (new): maps where almost every note is stacked on the previous one (< 5 px) and the
  whole map uses 1–2 spots give practically nothing with any mods: total × (1 − 0.97 · smoothstep(stacked, 60%, 90%)
  · (1 − smoothstep(spots per 32 notes, 2, 4))).
  hehehe - Iyul' [HS]: NM 134 → 4, DT 135 → 4, HD 208 → 6, HD+DT 264 → 8.
  Full maps with stacked streams (5+ spots), Kami no Kotoba (nothing stacked) and all normal maps ±0%.

## MosuPp 1.3.3 (cache 11)

- **1.3.2 reverted**: Point Variety Nerf cuts only aim and speed PP again (as in 1.1.0–1.3.1).
- **Stream-Only Guard (RX)**: stronger — speed PP stops counting in the total:
  speed × (1 − smoothstep(speed PP / aim PP, 5, 10)), **no speed PP at all from 10×**; the existing
  total × (1 − 0.9 · smoothstep(ratio, 6, 10)) stays.
  Point stream 400 BPM CS 10: NM 113 → 72, DT 159 → 101; 800 BPM CS 10 DT 3522 (1.0.0) → 151.
  Worms of Soul (≤ 3.1×), A Tale Of Salt And Light (≤ 2.1×), heat, drivers license ±0%.

## MosuPp 1.3.2 (cache 10) (reverted in 1.3.3)

- **Point Variety Nerf (RX)**: now also cuts accuracy and reading PP (before: aim and speed only).
  hehehe - Iyul' [HS] (every note on the centre, AR 0): NM 134 → 19, HD 253 → 31, DT 135 → 20.
  Kami no Kotoba small CS NM 321 → 208. Maps with normal patterns (20–30 spots per 32 notes) ±0%.

## MosuPp 1.3.1 (cache 9)

- **Simple Stream Nerf (RX)**: now only for stream maps — extra gate smoothstep(speed PP / aim PP, 1.3, 1.65).
  Jump maps with streams are no longer touched: drivers license (full diff) back to NM 1479 / DT 4520 (was −21%).
  Worms of Soul stays −30% (speed/aim 1.71 NM, 3.06 DT); Tale ±0%.

## MosuPp 1.3.0 (cache 8)

- **Aim-Focused Flow Guard (RX)**: enabled again (as in 1.1.0).
- **Simple Stream Nerf (RX)** (new): stream-dominated maps with simple streams (straight lines / one smooth curve)
  lose up to **30%** of the total PP; must be simple by both angle variety (< 16°, normal from 26°) and bending-direction
  changes (< 12%, normal from 18%); stream share 40% → 60%. Same for NM and DT.
  Worms of Soul −30% (NM 639 → 447, DT 2058 → 1440); drivers license (full diff) −21%;
  A Tale Of Salt And Light, heat, Novae Ruptis, Sky of Twilight, jump maps ±0%.

## MosuPp 1.2.0 (cache 7)

- **Aim-Focused Flow Guard (RX)**: **disabled for testing** (code kept in `RxAimFocusedFlowGuard.cs`, not called).
  Sky of Twilight / Novae Ruptis / Attack / Lament / Calm Down Juliet get their flow bonus back (≈ +15% vs 1.1.0).
  All other 1.1.0 rules stay.

## MosuPp 1.1.0 (cache 6)

- **Length Bonus (RX)**: max bonus raised from +40% to **+60%** (smoothstep of difficult strain count 100 → 400).
  A Tale Of Salt And Light NM +6% / DT +8%, Worms of Soul +1% / +2%, WE ♥ YURI +13%, most maps 0–1%.
- **Point Variety Nerf (RX)** (new): average distinct 24 px spots per 32-object window; below 12 spots aim and speed
  PP are cut, up to −85% at ≤ 3 spots. Kami no Kotoba "small CS" / "TURBO": about ×0.2 of total PP. Normal maps ±0%.
- **Aim-Focused Flow Guard (RX)** (new): the Realistik flow bonus (up to +17% aim strain) is removed on aim-focused
  maps (speed PP < 0.8× aim PP; full bonus again from 1.1×). Sky of Twilight / Novae Ruptis / Attack / Lament −13…14%,
  Calm Down Juliet −10% / −4%; Tale, Worms, heat and jump maps without flow ±0%.
- **Stream-Only Guard (RX)** (new): total PP × (1 − 0.9 · smoothstep(speed PP / aim PP, 6, 10)).
  Streams into one point at absurd BPM keep ~10% (400 BPM CS 10 DT 2270 → 227). Worms DT (3.1×) and Tale (≤ 2.1×) ±0%.

## MosuPp 1.0.0 (cache 5)

- **CS PP Buff (RX)**: ×1 at CS ≤ 3.5, +10% CS 4, +20% CS 5, +30% CS 6, +50% CS 7, +100% CS 8, +200% CS 9, +400% CS 10 (PCHIP).
- **Length Bonus (RX)**: up to +40% by difficult strain count (100 → 400).
- **Spike Nerf (RX)**: up to −45% by peak ratio (top-10 sections / 75th percentile) 1.8 → 4.0.
- **Low Accuracy Nerf (RX)**: ≥ 75% ×1, 75% → 70% smooth to ×0.25, below 70% ×0.25·((acc − 55%) / 15%)³.
