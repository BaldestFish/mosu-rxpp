# Changelog — PP changes vs. default

Every PP change vs. the base is logged here: version, date, **title**, what changed,
who is affected (Relax only / all), star rating impact, how to disable.

Base: **rosu-pp 4.0.1** (commit `27a67242`, 2026-04-12), osu!standard.
Base values match Rust rosu-pp bit-for-bit.

---

## [0.1.0] — 2026-09-24 — "Base port"

- Plain C# port of rosu-pp 4.0.1 (osu!std): `.osu` parser, stars, PP incl. Relax.
- Library name: **MosuPp** (namespace `MosuPp`).
- **No changes vs. default.**

### Reverted

- "Relax CS buff" (+20% at CS 4 … +500% at CS 10, final RX PP) — added and reverted
  the same day pending discussion. Not in code.
