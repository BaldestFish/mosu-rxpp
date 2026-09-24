using System.Collections.Generic;
using System.IO;
using MosuPp;
using MosuPp.Model;
using MosuPp.Osu;
using Xunit;

namespace MosuPp.Tests
{
    /// <summary>
    /// Golden values produced by the original Rust rosu-pp 4.0.1 (tools/parity/cmp.rs).
    /// The port is expected to be bit-exact, hence exact equality.
    /// </summary>
    public class ParityWithRustTests
    {
        private static readonly Beatmap Map = Beatmap.FromPath(Path.Combine(System.AppContext.BaseDirectory, "Resources", "2785319.osu"));

        [Fact]
        public void Case00()
        {
            // L:0 acc=- n100=- n50=- miss=- combo=- lazer=- passed=- legacy=-
            var perf = new OsuPerformance(Map).Mods(GameMods.FromLegacy(0u));
            OsuPerformanceAttributes r = perf.Calculate();

            Assert.Equal(5.740766046562338, r.Stars);
            Assert.Equal(287.9051448920619, r.Pp);
            Assert.Equal(113.66811014707582, r.PpAim);
            Assert.Equal(65.7316947411581, r.PpSpeed);
            Assert.Equal(98.99847982709288, r.PpAcc);
            Assert.Equal(0.0, r.PpFlashlight);
            Assert.Equal(909u, r.MaxCombo);
            Assert.Equal(new uint[] { 909, 601, 0, 0, 0 },
                new[] { r.State.MaxCombo, r.State.HitResults.N300, r.State.HitResults.N100, r.State.HitResults.N50, r.State.HitResults.Misses });
        }

        [Fact]
        public void Case01()
        {
            // L:128 acc=- n100=- n50=- miss=- combo=- lazer=- passed=- legacy=-
            var perf = new OsuPerformance(Map).Mods(GameMods.FromLegacy(128u));
            OsuPerformanceAttributes r = perf.Calculate();

            Assert.Equal(4.5408408157009, r.Stars);
            Assert.Equal(93.89318102567644, r.Pp);
            Assert.Equal(82.3624394962074, r.PpAim);
            Assert.Equal(0.0, r.PpSpeed);
            Assert.Equal(0.0, r.PpAcc);
            Assert.Equal(0.0, r.PpFlashlight);
            Assert.Equal(909u, r.MaxCombo);
            Assert.Equal(new uint[] { 909, 601, 0, 0, 0 },
                new[] { r.State.MaxCombo, r.State.HitResults.N300, r.State.HitResults.N100, r.State.HitResults.N50, r.State.HitResults.Misses });
        }

        [Fact]
        public void Case02()
        {
            // L:136 acc=98.5 n100=- n50=- miss=2 combo=400 lazer=- passed=- legacy=-
            var perf = new OsuPerformance(Map).Mods(GameMods.FromLegacy(136u)).Accuracy(98.5).Misses(2u).Combo(400u);
            OsuPerformanceAttributes r = perf.Calculate();

            Assert.Equal(4.69040570504243, r.Stars);
            Assert.Equal(58.439352454009736, r.Pp);
            Assert.Equal(51.26258987193836, r.PpAim);
            Assert.Equal(0.0, r.PpSpeed);
            Assert.Equal(0.0, r.PpAcc);
            Assert.Equal(0.0, r.PpFlashlight);
            Assert.Equal(909u, r.MaxCombo);
            Assert.Equal(new uint[] { 400, 588, 0, 11, 2 },
                new[] { r.State.MaxCombo, r.State.HitResults.N300, r.State.HitResults.N100, r.State.HitResults.N50, r.State.HitResults.Misses });
        }

        [Fact]
        public void Case03()
        {
            // L:192 acc=- n100=12 n50=3 miss=1 combo=500 lazer=- passed=- legacy=-
            var perf = new OsuPerformance(Map).Mods(GameMods.FromLegacy(192u)).N100(12u).N50(3u).Misses(1u).Combo(500u);
            OsuPerformanceAttributes r = perf.Calculate();

            Assert.Equal(6.41615240761904, r.Stars);
            Assert.Equal(197.7305731518523, r.Pp);
            Assert.Equal(173.44787118583534, r.PpAim);
            Assert.Equal(0.0, r.PpSpeed);
            Assert.Equal(0.0, r.PpAcc);
            Assert.Equal(0.0, r.PpFlashlight);
            Assert.Equal(909u, r.MaxCombo);
            Assert.Equal(new uint[] { 500, 585, 12, 3, 1 },
                new[] { r.State.MaxCombo, r.State.HitResults.N300, r.State.HitResults.N100, r.State.HitResults.N50, r.State.HitResults.Misses });
        }

        [Fact]
        public void Case04()
        {
            // L:216 acc=97 n100=- n50=- miss=0 combo=- lazer=- passed=- legacy=-
            var perf = new OsuPerformance(Map).Mods(GameMods.FromLegacy(216u)).Accuracy(97.0).Misses(0u);
            OsuPerformanceAttributes r = perf.Calculate();

            Assert.Equal(7.341065069392153, r.Stars);
            Assert.Equal(198.18956083743336, r.Pp);
            Assert.Equal(173.8504919626608, r.PpAim);
            Assert.Equal(0.0, r.PpSpeed);
            Assert.Equal(0.0, r.PpAcc);
            Assert.Equal(0.0, r.PpFlashlight);
            Assert.Equal(909u, r.MaxCombo);
            Assert.Equal(new uint[] { 909, 574, 0, 27, 0 },
                new[] { r.State.MaxCombo, r.State.HitResults.N300, r.State.HitResults.N100, r.State.HitResults.N50, r.State.HitResults.Misses });
        }

        [Fact]
        public void Case05()
        {
            // L:1224 acc=- n100=- n50=- miss=- combo=- lazer=- passed=- legacy=-
            var perf = new OsuPerformance(Map).Mods(GameMods.FromLegacy(1224u));
            OsuPerformanceAttributes r = perf.Calculate();

            Assert.Equal(7.686571415523493, r.Stars);
            Assert.Equal(456.2195807096041, r.Pp);
            Assert.Equal(245.63439493777275, r.PpAim);
            Assert.Equal(0.0, r.PpSpeed);
            Assert.Equal(0.0, r.PpAcc);
            Assert.Equal(180.08078006610447, r.PpFlashlight);
            Assert.Equal(909u, r.MaxCombo);
            Assert.Equal(new uint[] { 909, 601, 0, 0, 0 },
                new[] { r.State.MaxCombo, r.State.HitResults.N300, r.State.HitResults.N100, r.State.HitResults.N50, r.State.HitResults.Misses });
        }

        [Fact]
        public void Case06()
        {
            // L:128 acc=96 n100=- n50=- miss=3 combo=300 lazer=false passed=- legacy=-
            var perf = new OsuPerformance(Map).Mods(GameMods.FromLegacy(128u)).Accuracy(96.0).Misses(3u).Combo(300u).Lazer(false);
            OsuPerformanceAttributes r = perf.Calculate();

            Assert.Equal(4.5408408157009, r.Stars);
            Assert.Equal(35.566286997053695, r.Pp);
            Assert.Equal(31.198497365836573, r.PpAim);
            Assert.Equal(0.0, r.PpSpeed);
            Assert.Equal(0.0, r.PpAcc);
            Assert.Equal(0.0, r.PpFlashlight);
            Assert.Equal(909u, r.MaxCombo);
            Assert.Equal(new uint[] { 300, 572, 4, 22, 3 },
                new[] { r.State.MaxCombo, r.State.HitResults.N300, r.State.HitResults.N100, r.State.HitResults.N50, r.State.HitResults.Misses });
        }

        [Fact]
        public void Case07()
        {
            // L:128 acc=99 n100=- n50=- miss=0 combo=- lazer=- passed=300 legacy=-
            var perf = new OsuPerformance(Map).Mods(GameMods.FromLegacy(128u)).Accuracy(99.0).Misses(0u).PassedObjects(300u);
            OsuPerformanceAttributes r = perf.Calculate();

            Assert.Equal(4.415425278425774, r.Stars);
            Assert.Equal(66.35930848312047, r.Pp);
            Assert.Equal(58.20991972203549, r.PpAim);
            Assert.Equal(0.0, r.PpSpeed);
            Assert.Equal(0.0, r.PpAcc);
            Assert.Equal(0.0, r.PpFlashlight);
            Assert.Equal(431u, r.MaxCombo);
            Assert.Equal(new uint[] { 431, 295, 3, 2, 0 },
                new[] { r.State.MaxCombo, r.State.HitResults.N300, r.State.HitResults.N100, r.State.HitResults.N50, r.State.HitResults.Misses });
        }

        [Fact]
        public void Case08()
        {
            // Z:RX,DT=1.3 acc=- n100=5 n50=0 miss=0 combo=- lazer=- passed=- legacy=-
            var perf = new OsuPerformance(Map).Mods(GameMods.FromLazer(new[] { new LazerMod("RX"), new LazerMod("DT", new Dictionary<string, object> { ["speed_change"] = 1.3 }) })).N100(5u).N50(0u).Misses(0u);
            OsuPerformanceAttributes r = perf.Calculate();

            Assert.Equal(5.670600234310002, r.Stars);
            Assert.Equal(167.9985187419268, r.Pp);
            Assert.Equal(147.36712170344455, r.PpAim);
            Assert.Equal(0.0, r.PpSpeed);
            Assert.Equal(0.0, r.PpAcc);
            Assert.Equal(0.0, r.PpFlashlight);
            Assert.Equal(909u, r.MaxCombo);
            Assert.Equal(new uint[] { 909, 596, 5, 0, 0 },
                new[] { r.State.MaxCombo, r.State.HitResults.N300, r.State.HitResults.N100, r.State.HitResults.N50, r.State.HitResults.Misses });
        }

        [Fact]
        public void Case09()
        {
            // Z:RX,CL acc=97.2 n100=- n50=- miss=1 combo=- lazer=- passed=- legacy=-
            var perf = new OsuPerformance(Map).Mods(GameMods.FromLazer(new[] { new LazerMod("RX"), new LazerMod("CL") })).Accuracy(97.2).Misses(1u);
            OsuPerformanceAttributes r = perf.Calculate();

            Assert.Equal(4.5408408157009, r.Stars);
            Assert.Equal(43.29438613163336, r.Pp);
            Assert.Equal(37.97753169441523, r.PpAim);
            Assert.Equal(0.0, r.PpSpeed);
            Assert.Equal(0.0, r.PpAcc);
            Assert.Equal(0.0, r.PpFlashlight);
            Assert.Equal(909u, r.MaxCombo);
            Assert.Equal(new uint[] { 908, 579, 3, 18, 1 },
                new[] { r.State.MaxCombo, r.State.HitResults.N300, r.State.HitResults.N100, r.State.HitResults.N50, r.State.HitResults.Misses });
        }

        [Fact]
        public void Case10()
        {
            // Z:RX,DA=ar:10;od:9;cs:4.5 acc=- n100=- n50=- miss=- combo=- lazer=- passed=- legacy=-
            var perf = new OsuPerformance(Map).Mods(GameMods.FromLazer(new[] { new LazerMod("RX"), new LazerMod("DA", new Dictionary<string, object> { ["approach_rate"] = 10.0, ["overall_difficulty"] = 9.0, ["circle_size"] = 4.5 }) }));
            OsuPerformanceAttributes r = perf.Calculate();

            Assert.Equal(4.542611467876061, r.Stars);
            Assert.Equal(94.0058093901409, r.Pp);
            Assert.Equal(82.46123630714114, r.PpAim);
            Assert.Equal(0.0, r.PpSpeed);
            Assert.Equal(0.0, r.PpAcc);
            Assert.Equal(0.0, r.PpFlashlight);
            Assert.Equal(909u, r.MaxCombo);
            Assert.Equal(new uint[] { 909, 601, 0, 0, 0 },
                new[] { r.State.MaxCombo, r.State.HitResults.N300, r.State.HitResults.N100, r.State.HitResults.N50, r.State.HitResults.Misses });
        }

        [Fact]
        public void Case11()
        {
            // L:8 acc=95 n100=- n50=- miss=4 combo=250 lazer=false passed=- legacy=8000000
            var perf = new OsuPerformance(Map).Mods(GameMods.FromLegacy(8u)).Accuracy(95.0).Misses(4u).Combo(250u).Lazer(false).LegacyTotalScore(8000000u);
            OsuPerformanceAttributes r = perf.Calculate();

            Assert.Equal(5.934133851244852, r.Stars);
            Assert.Equal(143.6610675385064, r.Pp);
            Assert.Equal(88.60078710298386, r.PpAim);
            Assert.Equal(39.2222208868148, r.PpSpeed);
            Assert.Equal(7.41211015471999, r.PpAcc);
            Assert.Equal(0.0, r.PpFlashlight);
            Assert.Equal(909u, r.MaxCombo);
            Assert.Equal(new uint[] { 250, 565, 4, 28, 4 },
                new[] { r.State.MaxCombo, r.State.HitResults.N300, r.State.HitResults.N100, r.State.HitResults.N50, r.State.HitResults.Misses });
        }

        [Fact]
        public void Case12()
        {
            // L:0 acc=- n100=- n50=- miss=- combo=- lazer=- passed=- legacy=-
            var perf = new OsuPerformance(Map).Mods(GameMods.FromLegacy(0u));
            OsuPerformanceAttributes r = perf.Calculate();

            Assert.Equal(5.740766046562338, r.Stars);
            Assert.Equal(287.9051448920619, r.Pp);
            Assert.Equal(113.66811014707582, r.PpAim);
            Assert.Equal(65.7316947411581, r.PpSpeed);
            Assert.Equal(98.99847982709288, r.PpAcc);
            Assert.Equal(0.0, r.PpFlashlight);
            Assert.Equal(909u, r.MaxCombo);
            Assert.Equal(new uint[] { 909, 601, 0, 0, 0 },
                new[] { r.State.MaxCombo, r.State.HitResults.N300, r.State.HitResults.N100, r.State.HitResults.N50, r.State.HitResults.Misses });
        }
    }
}
