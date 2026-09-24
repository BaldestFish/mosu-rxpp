using System;
using System.Globalization;
using MosuPp;
using MosuPp.Model;
using MosuPp.Osu;

// Usage: MosuPp.Example <map.osu> [mods, e.g. RXHD] [accuracy] [misses] [combo]
if (args.Length == 0)
{
    Console.WriteLine("Usage: MosuPp.Example <map.osu> [mods=RX] [accuracy] [misses] [combo]");
    return;
}

Beatmap map = Beatmap.FromPath(args[0]);

// Protects against maps built to stress-test osu! (throws TooSuspiciousException)
map.CheckSuspicion();

GameMods mods = GameMods.FromAcronyms(args.Length > 1 ? args[1] : "RX");

// 1) Difficulty attributes (stars). Calculate them once per (map, mods) and reuse them.
OsuDifficultyAttributes difficulty = new Difficulty().Mods(mods).Calculate(map);

Console.WriteLine($"Mods: {mods}");
Console.WriteLine($"Stars: {difficulty.Stars:F2} (aim {difficulty.Aim:F2}, speed {difficulty.Speed:F2})");
Console.WriteLine($"Max combo: {difficulty.MaxCombo}, AR {difficulty.Ar:F2}, OD {difficulty.Od:F2}");

// 2) Performance for a score. Re-using the attributes is much faster than passing the map again.
var perf = new OsuPerformance(difficulty).Mods(mods);

if (args.Length > 2) perf.Accuracy(double.Parse(args[2], CultureInfo.InvariantCulture));
if (args.Length > 3) perf.Misses(uint.Parse(args[3]));
if (args.Length > 4) perf.Combo(uint.Parse(args[4]));

OsuPerformanceAttributes result = perf.Calculate();

Console.WriteLine($"PP: {result.Pp:F2} (aim {result.PpAim:F2}, speed {result.PpSpeed:F2}, acc {result.PpAcc:F2}, fl {result.PpFlashlight:F2})");
Console.WriteLine($"Used hit results: {result.State.HitResults}, combo {result.State.MaxCombo}");

// 3) Max pp (SS) for the same mods
double maxPp = new OsuPerformance(difficulty).Mods(mods).Calculate().Pp;
Console.WriteLine($"SS PP: {maxPp:F2}");
