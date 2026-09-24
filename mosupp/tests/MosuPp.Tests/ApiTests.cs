using System.IO;
using MosuPp;
using MosuPp.Model;
using MosuPp.Osu;
using Xunit;

namespace MosuPp.Tests
{
    public class ApiTests
    {
        private static readonly string MapPath = Path.Combine(System.AppContext.BaseDirectory, "Resources", "2785319.osu");

        [Fact]
        public void AcronymsJsonAndLegacyAgree()
        {
            Beatmap map = Beatmap.FromPath(MapPath);

            double legacy = new Difficulty().Mods(GameMods.FromLegacy(LegacyMods.Relax | LegacyMods.Hidden)).Calculate(map).Stars;
            double acronyms = new Difficulty().Mods(GameMods.FromAcronyms("RXHD")).Calculate(map).Stars;
            double json = new Difficulty().Mods(GameMods.FromJson("[{\"acronym\":\"RX\"},{\"acronym\":\"HD\"}]")).Calculate(map).Stars;

            Assert.Equal(legacy, acronyms);
            Assert.Equal(legacy, json);
        }

        [Fact]
        public void LazerSpeedChangeMatchesClockRate()
        {
            Beatmap map = Beatmap.FromPath(MapPath);

            double viaSetting = new Difficulty().Mods(GameMods.FromJson("[{\"acronym\":\"RX\"},{\"acronym\":\"DT\",\"settings\":{\"speed_change\":1.25}}]"))
                                                .Calculate(map).Stars;
            double viaClockRate = new Difficulty().Mods(GameMods.FromAcronyms("RXDT")).ClockRate(1.25).Calculate(map).Stars;

            Assert.Equal(viaSetting, viaClockRate);
        }

        [Fact]
        public void ParseFromStringEqualsFromPath()
        {
            Beatmap a = Beatmap.FromPath(MapPath);
            Beatmap b = Beatmap.FromString(File.ReadAllText(MapPath));

            Assert.Equal(new Difficulty().Calculate(a).Stars, new Difficulty().Calculate(b).Stars);
        }

        [Fact]
        public void ReusingAttributesGivesSamePp()
        {
            Beatmap map = Beatmap.FromPath(MapPath);
            GameMods mods = GameMods.FromAcronyms("RX");

            OsuDifficultyAttributes attrs = new Difficulty().Mods(mods).Calculate(map);

            double fromMap = new OsuPerformance(map).Mods(mods).Accuracy(98.5).Misses(2).Calculate().Pp;
            double fromAttrs = new OsuPerformance(attrs).Mods(mods).Accuracy(98.5).Misses(2).Calculate().Pp;

            Assert.Equal(fromMap, fromAttrs);
        }

        [Fact]
        public void RelaxHasNoSpeedOrAccuracyPp()
        {
            Beatmap map = Beatmap.FromPath(MapPath);

            OsuPerformanceAttributes r = new OsuPerformance(map).Mods(GameMods.FromAcronyms("RX")).Calculate();

            Assert.Equal(0.0, r.PpSpeed);
            Assert.Equal(0.0, r.PpAcc);
            Assert.True(r.PpAim > 0);
        }

        [Fact]
        public void SuspicionCheckPassesForNormalMap()
        {
            Beatmap map = Beatmap.FromPath(MapPath);

            Assert.True(map.GetSuspicion() == null);
        }
    }
}
