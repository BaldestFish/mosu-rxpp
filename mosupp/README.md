# MosuPp — PP-система Relax (osu!standard) на C#

MosuPp — PP-система для Relax, в основе — порт [rosu-pp](https://github.com/MaxOhn/rosu-pp) **4.0.1** (коммит `27a6724`, апрель 2026) на C#
для режима **osu!standard**. Включает парсер `.osu` из [rosu-map](https://github.com/MaxOhn/rosu-map),
расчёт звёзд, расчёт PP (в том числе с **Relax**) и генерацию хит-результатов по точности.
Библиотека самостоятельная: без зависимостей от `osu.Game`, без NuGet-пакетов.

Формулы — те же, что в osu!lazer на момент порта rosu-pp (lazer `28c846b`, ребаланс PP/SR от 2025-10-29).

## Точность порта

Результаты **побитово совпадают** с оригинальным Rust rosu-pp: сравнивались все поля
атрибутов сложности и производительности (звёзды, aim/speed/fl, pp и все его части, оценки
промахов, хит-результаты и т.д.):

| Набор                                                                     | Кейсов | Побитово равны |
|---------------------------------------------------------------------------|-------:|---------------:|
| 160 реальных/тестовых карт (ресурсы rosu-pp, rosu-map, тестовые карты lazer) × 25 наборов модов × 6 вариантов скора | 24 320 | 24 320 |
| 400 случайно сгенерированных карт (все типы слайдеров, SV, стаки, v3–v14) | 1 600 | 1 600 |
| 300 намеренно битых карт (мусорные строки, NaN, переполнения, кривые слайдеры) | 1 500 | 1 500 |

Проверка проводилась на Linux x64 (.NET 8). На других платформах системные `Math.Pow/Exp/Log`
могут отличаться в последнем бите — это отклонения порядка 1e-15, на PP не влияют.

Среди модов — RX, RXHD, RXDT, RXHRDT, RXEZ, RXFL, RXHT, AP, lazer-моды с настройками
(DT/NC со `speed_change`, DA, CL, MR, MG, DF, BL, TC). Скрипты проверки лежат в `tools/parity`.

Для этого в порт перенесены особенности Rust, которые обычный C# делает иначе: min/max,
игнорирующие NaN, насыщающие приведения `as`, `total_cmp`, сортировка .NET Framework
для вложенных объектов слайдера, `binary_search` Rust, корректно округлённый `cbrt`,
а также `powf(x, 2.0)` → `x * x` и `powf(x, 0.5)` → `sqrt`, как их компилирует LLVM.

## Быстрый старт

```csharp
using MosuPp;
using MosuPp.Model;
using MosuPp.Osu;

Beatmap map = Beatmap.FromPath("map.osu");   // также FromBytes / FromStream / FromString
map.CheckSuspicion();                        // защита от «карт-бомб» (TooSuspiciousException)

GameMods mods = GameMods.FromAcronyms("RXHD");

// 1) Звёзды. Атрибуты стоит считать один раз на пару (карта, моды) и кэшировать.
OsuDifficultyAttributes diff = new Difficulty().Mods(mods).Calculate(map);
Console.WriteLine($"{diff.Stars:F2}★, max combo {diff.MaxCombo}");

// 2) PP скора. Передача атрибутов вместо карты — это микросекунды вместо миллисекунд.
OsuPerformanceAttributes pp = new OsuPerformance(diff)
    .Mods(mods)                 // те же моды, что и при расчёте атрибутов!
    .Combo(812)
    .N300(560).N100(12).N50(1).Misses(2)
    .Calculate();

Console.WriteLine($"{pp.Pp:F2}pp (aim {pp.PpAim:F2}, speed {pp.PpSpeed:F2}, acc {pp.PpAcc:F2})");

// 3) PP за SS с этими модами
double ssPp = new OsuPerformance(diff).Mods(mods).Calculate().Pp;
```

## Relax

Relax здесь считается **так, как в rosu-pp / osu!lazer** (официальная RX-логика lazer):

- в звёздах: `aim × 0.9`, speed = 0, у FL множитель 0.7, AR-бонус к aim не начисляется;
- в PP: speed PP = 0, accuracy PP = 0; сотки и полтинники добавляются к «эффективным промахам»
  с весами, зависящими от OD (`0.75·(1 − OD/13.33)` для 100 и `1 − (OD/13.33)^5` для 50).

> ⚠️ Это **не** система Mosu Realistik (порт akatsuki-pp из `osu.Game.Rulesets.Osu/Difficulty/Relax/Realistik`) —
> там другой алгоритм. MosuPp даёт «ванильный» lazer-Relax, как в rosu-pp.

### Stable-скор (legacy-биты модов)

```csharp
var result = new OsuPerformance(map)
    .Mods(GameMods.FromLegacy(LegacyMods.Relax | LegacyMods.Hidden | LegacyMods.DoubleTime))
    .Lazer(false)                       // stable: головы слайдеров не влияют на точность
    .Combo(1024).N300(700).N100(15).N50(0).Misses(1)
    .LegacyTotalScore(45_000_000)       // необязательно; улучшает оценку слайдербрейков
    .Calculate();
```

`GameMods.FromLegacy(uint)` принимает обычные биты osu!api (RX = 128, HD = 8, DT = 64 ...).
Как и в rosu-pp, Nightcore в legacy-битах должен содержать бит DT: `LegacyMods.Nightcore` = 512 | 64.

### Lazer-скор (моды с настройками)

```csharp
GameMods mods = GameMods.FromJson("""
[{"acronym":"RX"},{"acronym":"DT","settings":{"speed_change":1.3}},{"acronym":"CL"}]
""");

var result = new OsuPerformance(map)
    .Mods(mods)
    .Combo(900).N300(640).N100(9).N50(0).Misses(0)
    .LargeTickHits(120)     // тики и повторы (без CL: только тики/повторы; с CL: + головы слайдеров)
    .SliderEndHits(95)      // хвосты слайдеров (для скоров без Classic)
    .SmallTickHits(95)      // хвосты слайдеров для скоров с Classic (без slider accuracy)
    .Calculate();
```

По умолчанию `Lazer(true)`. Для lazer-скоров стоит передавать `LargeTickHits`/`SliderEndHits` —
без них считается, что все тики и хвосты попаданы.

### Только точность

```csharp
var result = new OsuPerformance(diff).Mods(mods)
    .Accuracy(98.5).Misses(2)
    .HitResultGenerator(HitResultGenerator.Closest)   // по умолчанию Fast, как в rosu-pp
    .Calculate();

Console.WriteLine(result.State.HitResults);           // какие 300/100/50 были сгенерированы
```

Для RX это заметно: 100 и 50 штрафуются по-разному, а генератор `Fast` может выбрать много 50.
Если реальные 100/50 известны, передавайте их напрямую.

## API

| Класс | Назначение |
|---|---|
| `Beatmap` | `.osu` карта: `FromPath/FromBytes/FromStream/FromString`, `CheckSuspicion()`, `Attributes(difficulty)` |
| `GameMods` | `FromLegacy(bits)`, `FromAcronyms("RXHD")`, `FromJson(json)`, `FromLazer(mods)`, `ClockRate()`, `IsRelax` |
| `Difficulty` | настройки: `Mods`, `ClockRate`, `PassedObjects` (фейлы), `Ar/Od/Cs/Hp(value, fixed)`, `Lazer`; `Calculate(map)`, `CheckedCalculate(map)`, `Strains(map)` |
| `OsuDifficultyAttributes` | звёзды, aim/speed/fl, max combo, количество объектов, окна попадания, AR/OD … |
| `OsuPerformance` | билдер PP: `Combo`, `N300/N100/N50/Misses`, `Accuracy`, `LargeTickHits/SliderEndHits/SmallTickHits`, `LegacyTotalScore`, `HitResultPriority`, `HitResultGenerator`, `PassedObjects`, `ClockRate`; `Calculate()`, `CheckedCalculate()`, `GenerateState()` |
| `OsuPerformanceAttributes` | `Pp`, `PpAim/PpSpeed/PpAcc/PpFlashlight`, `EffectiveMissCount`, `SpeedDeviation`, `State` (использованные хит-результаты), `Difficulty` |

Потокобезопасность: `Beatmap` и `OsuDifficultyAttributes` после создания только читаются —
их можно делить между потоками. Билдеры `Difficulty`/`OsuPerformance` — по одному на поток.

Скорость (.NET 8, x64): парсинг обычной карты ~4 мс, звёзды ~8 мс (марафон на 6500 объектов — ~27 мс),
PP из готовых атрибутов ~1–2 мкс.

## Что не портировано

- taiko / catch / mania и конвертация карт между режимами;
- gradual-расчёт (`OsuGradualDifficulty/Performance` — PP «на лету» по мере игры);
- lazer-мод `RandomOsu` (в rosu-pp его тоже нет).

## Сборка и тесты

```bash
dotnet build src/MosuPp/MosuPp.csproj -c Release
dotnet test tests/MosuPp.Tests
dotnet run --project examples/MosuPp.Example -- map.osu RXHD 98.5 2 500
```

`tests/MosuPp.Tests/ParityWithRustTests.cs` — эталонные значения, полученные из Rust rosu-pp;
тесты требуют точного равенства.

## Соответствие файлов

| C# | rosu-pp / rosu-map |
|---|---|
| `Model/BeatmapDecoder.cs` | `rosu-map/src/decode.rs`, `rosu-pp/src/model/beatmap/decode.rs` |
| `Model/Curve.cs`, `Model/SliderEvents.cs` | `rosu-map/.../slider/curve.rs`, `event.rs` |
| `Model/BeatmapAttributes.cs` | `model/beatmap/attributes/*` |
| `Model/GameMods.cs` | `model/mods.rs` (+ `rosu-mods`) |
| `Osu/OsuObject.cs`, `Osu/OsuConvert.cs` | `osu/object.rs`, `osu/convert.rs`, `osu/difficulty/scaling_factor.rs` |
| `Osu/OsuDifficultyObject.cs` | `osu/difficulty/object.rs` |
| `Osu/Skills.cs`, `Osu/Evaluators.cs` | `osu/difficulty/skills/*`, `osu/difficulty/evaluators/*` |
| `Osu/OsuRatingCalculator.cs` | `osu/difficulty/rating.rs` |
| `Osu/OsuDifficultyCalculator.cs` | `osu/difficulty/mod.rs` |
| `Osu/LegacyScore.cs` | `osu/legacy_score_simulator`, `osu/utils/legacy_score.rs` |
| `Osu/OsuPerformanceCalculator.cs` | `osu/performance/calculator.rs`, `osu/legacy_score_miss_calc.rs` |
| `Osu/HitResultGenerators.cs` | `osu/performance/hitresult_generator/*` |

Лицензия — MIT (как у rosu-pp / rosu-map / osu!), см. `LICENSE`.
