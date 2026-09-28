// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Online.API;
using osu.Game.Rulesets.Mods;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Rulesets.Difficulty;
using Realms;

namespace osu.Game.Database
{
    public class ForkDataStore : IDisposable
    {
        private const int schema_version = 11; // 11: RelaxMosuPp* fields (MosuPp relax PP system).

        /// <summary>
        /// Calculator revision of the Mosu/Realistik relax PP system
        /// (fields <c>Relax*</c> in <see cref="ForkBeatmapData"/>).
        /// </summary>
        public const int RELAX_PERFORMANCE_CALCULATION_VERSION = 2026081302;

        /// <summary>
        /// Calculator revision of the vanilla lazer relax PP system
        /// (fields <c>RelaxVanilla*</c> in <see cref="ForkBeatmapData"/>).
        /// </summary>
        public const int RELAX_VANILLA_PERFORMANCE_CALCULATION_VERSION = 2026082201;

        /// <summary>
        /// Calculator revision of the MosuPp relax PP system (fields <c>RelaxMosuPp*</c> in <see cref="ForkBeatmapData"/>).
        /// Bump whenever a MosuPp rule changes (see MOSUPP_CHANGELOG.md).
        /// </summary>
        public const int RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION = 25;

        private readonly Storage storage;
        private readonly ConcurrentDictionary<Guid, double> ppCache = new ConcurrentDictionary<Guid, double>();
        private readonly ConcurrentDictionary<Guid, RelaxBeatmapData> relaxCache = new ConcurrentDictionary<Guid, RelaxBeatmapData>();
        private readonly ConcurrentDictionary<Guid, RelaxBeatmapData> relaxVanillaCache = new ConcurrentDictionary<Guid, RelaxBeatmapData>();
        private readonly ConcurrentDictionary<Guid, RelaxBeatmapData> relaxMosuPpCache = new ConcurrentDictionary<Guid, RelaxBeatmapData>();
        private readonly ConcurrentDictionary<Guid, int> ppVersions = new ConcurrentDictionary<Guid, int>();
        private readonly ConcurrentDictionary<Guid, int> relaxVersions = new ConcurrentDictionary<Guid, int>();
        private readonly ConcurrentDictionary<Guid, int> relaxVanillaVersions = new ConcurrentDictionary<Guid, int>();
        private readonly ConcurrentDictionary<Guid, int> relaxMosuPpVersions = new ConcurrentDictionary<Guid, int>();
        private readonly ConcurrentDictionary<Guid, DodgeDifficultyData> dodgeDifficultyCache = new ConcurrentDictionary<Guid, DodgeDifficultyData>();
        private readonly ConcurrentDictionary<Guid, string> tagCoopReplays = new ConcurrentDictionary<Guid, string>();
        private readonly ConcurrentDictionary<(Guid BeatmapId, string Ruleset), AdditionalInfoData> additionalInfoCache = new ConcurrentDictionary<(Guid, string), AdditionalInfoData>();

        public static int GetRelaxCalculationVersion(ForkRelaxPpSystem system)
            => system switch
            {
                ForkRelaxPpSystem.LazerVanilla => RELAX_VANILLA_PERFORMANCE_CALCULATION_VERSION,
                ForkRelaxPpSystem.MosuPp => RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION,
                _ => RELAX_PERFORMANCE_CALCULATION_VERSION,
            };

        public static ForkDataStore? Instance { get; private set; }

        public ForkDataStore(Storage storage)
        {
            this.storage = storage;
            Instance = this;
            loadFromRealm();
        }

        private RealmConfiguration getConfiguration()
        {
            string tempPathLocation = Path.Combine(Path.GetTempPath(), @"lazer");

            if (!Directory.Exists(tempPathLocation))
                Directory.CreateDirectory(tempPathLocation);

            return new RealmConfiguration(storage.GetFullPath("fork.realm", true))
            {
                SchemaVersion = schema_version,
                Schema = new[] { typeof(ForkBeatmapData), typeof(ForkScoreData), typeof(ForkSeasonalBackgroundData) },
                FallbackPipePath = tempPathLocation,
            };
        }

        private void loadFromRealm()
        {
            try
            {
                using var realm = Realm.GetInstance(getConfiguration());

                foreach (var data in realm.All<ForkBeatmapData>())
                {
                    ppCache[data.BeatmapID] = data.MaxPerformancePoints;
                    relaxCache[data.BeatmapID] = new RelaxBeatmapData(data.RelaxStarRating, data.RelaxMaxPerformancePoints);
                    relaxVanillaCache[data.BeatmapID] = new RelaxBeatmapData(data.RelaxVanillaStarRating, data.RelaxVanillaMaxPerformancePoints);
                    relaxMosuPpCache[data.BeatmapID] = new RelaxBeatmapData(data.RelaxMosuPpStarRating, data.RelaxMosuPpMaxPerformancePoints);

                    if (data.DodgeDifficultyVersion > 0 && data.DodgeStarRating >= 0)
                    {
                        dodgeDifficultyCache[data.BeatmapID] = new DodgeDifficultyData(
                            data.DodgeStarRating,
                            data.DodgeDifficultyVersion,
                            data.DodgeBeatmapChecksum ?? string.Empty,
                            data.DodgeDifficultyAttributesJson ?? string.Empty);
                    }

                    if (!string.IsNullOrEmpty(data.AdditionalInfoJson))
                    {
                        try
                        {
                            var entries = JsonConvert.DeserializeObject<Dictionary<string, AdditionalInfoData>>(data.AdditionalInfoJson);
                            if (entries != null)
                            {
                                foreach (var entry in entries)
                                {
                                    // Schema 9 initially stored nomod entries under the bare ruleset name.
                                    string key = entry.Key.Contains('|') ? entry.Key : createAdditionalInfoKey(entry.Key, "nomod");
                                    additionalInfoCache[(data.BeatmapID, key)] = entry.Value;
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // A damaged optional cache entry must not prevent loading the rest of fork.realm.
                        }
                    }

                    if (data.PerformancePointsCalculated)
                        ppVersions[data.BeatmapID] = data.PerformancePointsVersion;

                    if (data.RelaxPerformancePointsCalculated)
                        relaxVersions[data.BeatmapID] = data.RelaxPerformancePointsVersion;

                    if (data.RelaxVanillaPerformancePointsCalculated)
                        relaxVanillaVersions[data.BeatmapID] = data.RelaxVanillaPerformancePointsVersion;

                    if (data.RelaxMosuPpPerformancePointsCalculated)
                        relaxMosuPpVersions[data.BeatmapID] = data.RelaxMosuPpPerformancePointsVersion;
                }

                foreach (var data in realm.All<ForkScoreData>())
                    tagCoopReplays[data.ScoreID] = data.TagCoopReplayJson;

                Logger.Log($"ForkDataStore: loaded {ppCache.Count} PP entries and {tagCoopReplays.Count} Tag Co-op replays from fork.realm");
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to load: {e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        public double GetPP(Guid beatmapId) => ppCache.GetValueOrDefault(beatmapId, -1);

        public byte[]? GetSeasonalBackground(string hash)
        {
            if (!isSha256(hash))
                return null;

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());
                return realm.Find<ForkSeasonalBackgroundData>(hash.ToLowerInvariant())?.Data.ToArray();
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to read seasonal background {hash}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
                return null;
            }
        }

        public void SetSeasonalBackground(string hash, byte[] data)
        {
            if (!isSha256(hash) || data.Length == 0)
                return;

            string normalisedHash = hash.ToLowerInvariant();

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());
                realm.Write(() => realm.Add(new ForkSeasonalBackgroundData
                {
                    Hash = normalisedHash,
                    Data = data,
                    LastAccessed = DateTimeOffset.UtcNow,
                }, update: true));
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to write seasonal background {hash}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        private static bool isSha256(string hash)
            => hash.Length == 64 && hash.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

        public static string CreateModsKey(IEnumerable<Mod>? mods)
        {
            var apiMods = mods?.OrderBy(mod => mod.Acronym)
                               .Select(mod => new APIMod(mod))
                               .Select(mod => new APIMod
                               {
                                   Acronym = mod.Acronym,
                                   Settings = mod.Settings.OrderBy(setting => setting.Key, StringComparer.Ordinal)
                                                         .ToDictionary(setting => setting.Key, setting => setting.Value)
                               })
                               .ToArray() ?? Array.Empty<APIMod>();
            return apiMods.Length == 0 ? "nomod" : JsonConvert.SerializeObject(apiMods, Formatting.None);
        }

        private static string createAdditionalInfoKey(string ruleset, string modsKey)
            => $"{ruleset}|{modsKey}";

        public bool TryGetAdditionalInfo(Guid beatmapId, string ruleset, string modsKey, string? checksum, int version, out AdditionalInfoData data)
        {
            if (additionalInfoCache.TryGetValue((beatmapId, createAdditionalInfoKey(ruleset, modsKey)), out data)
                && data.Version == version
                && !string.IsNullOrEmpty(checksum)
                && string.Equals(data.Checksum, checksum, StringComparison.OrdinalIgnoreCase)
                && double.IsFinite(data.Stars)
                && data.MaxCombo >= 0
                && !string.IsNullOrEmpty(data.Text))
                return true;

            data = default;
            return false;
        }

        public void SetAdditionalInfo(Guid beatmapId, string ruleset, string modsKey, AdditionalInfoData data)
        {
            if (beatmapId == Guid.Empty || string.IsNullOrEmpty(ruleset) || string.IsNullOrEmpty(modsKey) || string.IsNullOrEmpty(data.Checksum)
                || data.Version <= 0 || !double.IsFinite(data.Stars) || data.MaxCombo < 0 || string.IsNullOrEmpty(data.Text))
                return;

            string cacheKey = createAdditionalInfoKey(ruleset, modsKey);
            if (additionalInfoCache.TryGetValue((beatmapId, cacheKey), out var existingData) && existingData == data)
                return;

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());
                realm.Write(() =>
                {
                    var existing = realm.Find<ForkBeatmapData>(beatmapId);
                    if (existing == null)
                    {
                        existing = new ForkBeatmapData { BeatmapID = beatmapId };
                        realm.Add(existing);
                    }

                    var entries = string.IsNullOrEmpty(existing.AdditionalInfoJson)
                        ? new Dictionary<string, AdditionalInfoData>()
                        : JsonConvert.DeserializeObject<Dictionary<string, AdditionalInfoData>>(existing.AdditionalInfoJson)
                          ?? new Dictionary<string, AdditionalInfoData>();
                    entries[cacheKey] = data;
                    existing.AdditionalInfoJson = JsonConvert.SerializeObject(entries);
                });
                additionalInfoCache[(beatmapId, cacheKey)] = data;
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to write additional info for {beatmapId}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        public readonly record struct AdditionalInfoData(string Checksum, int Version, double Stars, int MaxCombo, string Text);

        public double GetRelaxStarRating(Guid beatmapId) => getRelaxCache().GetValueOrDefault(beatmapId).StarRating;

        public double GetRelaxPP(Guid beatmapId) => getRelaxCache().GetValueOrDefault(beatmapId).MaxPerformancePoints;

        public bool HasPP(Guid beatmapId) => ppVersions.ContainsKey(beatmapId);

        public bool HasRelaxData(Guid beatmapId)
            => getRelaxVersions().TryGetValue(beatmapId, out int storedVersion)
               && storedVersion == GetRelaxCalculationVersion(RelaxPpSystemSelection.Current);

        private ConcurrentDictionary<Guid, RelaxBeatmapData> getRelaxCache()
            => RelaxPpSystemSelection.Current switch
            {
                ForkRelaxPpSystem.LazerVanilla => relaxVanillaCache,
                ForkRelaxPpSystem.MosuPp => relaxMosuPpCache,
                _ => relaxCache,
            };

        private ConcurrentDictionary<Guid, int> getRelaxVersions()
            => RelaxPpSystemSelection.Current switch
            {
                ForkRelaxPpSystem.LazerVanilla => relaxVanillaVersions,
                ForkRelaxPpSystem.MosuPp => relaxMosuPpVersions,
                _ => relaxVersions,
            };

        public DodgeDifficultyData GetDodgeDifficulty(Guid beatmapId)
            => dodgeDifficultyCache.GetValueOrDefault(beatmapId, new DodgeDifficultyData());

        public bool HasDodgeDifficulty(Guid beatmapId, int difficultyVersion)
            => dodgeDifficultyCache.TryGetValue(beatmapId, out DodgeDifficultyData data)
               && data.DifficultyVersion == difficultyVersion;

        public bool HasDodgeDifficultyAttributes(Guid beatmapId, int difficultyVersion, string? beatmapChecksum)
            => dodgeDifficultyCache.TryGetValue(beatmapId, out DodgeDifficultyData data)
               && data.DifficultyVersion == difficultyVersion
               && data.HasFullAttributes
               && !string.IsNullOrEmpty(beatmapChecksum)
               && string.Equals(data.BeatmapChecksum, beatmapChecksum, StringComparison.OrdinalIgnoreCase);

        public string GetTagCoopReplay(Guid scoreId) => tagCoopReplays.GetValueOrDefault(scoreId, string.Empty);

        public void SetTagCoopReplay(Guid scoreId, string json)
        {
            if (scoreId == Guid.Empty)
                return;

            if (string.IsNullOrEmpty(json))
                tagCoopReplays.TryRemove(scoreId, out _);
            else
                tagCoopReplays[scoreId] = json;

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());

                realm.Write(() =>
                {
                    var existing = realm.Find<ForkScoreData>(scoreId);

                    if (string.IsNullOrEmpty(json))
                    {
                        if (existing != null)
                            realm.Remove(existing);

                        return;
                    }

                    if (existing != null)
                        existing.TagCoopReplayJson = json;
                    else
                        realm.Add(new ForkScoreData { ScoreID = scoreId, TagCoopReplayJson = json });
                });
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to write Tag Co-op replay for {scoreId}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        internal void ImportTagCoopReplays(IEnumerable<KeyValuePair<Guid, string>> replays)
        {
            KeyValuePair<Guid, string>[] validReplays = replays.Where(r => r.Key != Guid.Empty && !string.IsNullOrEmpty(r.Value)).ToArray();

            if (validReplays.Length == 0)
                return;

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());

                realm.Write(() =>
                {
                    foreach ((Guid scoreId, string json) in validReplays)
                    {
                        tagCoopReplays[scoreId] = json;

                        var existing = realm.Find<ForkScoreData>(scoreId);

                        if (existing != null)
                            existing.TagCoopReplayJson = json;
                        else
                            realm.Add(new ForkScoreData { ScoreID = scoreId, TagCoopReplayJson = json });
                    }
                });
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to import Tag Co-op replays: {e.Message}", LoggingTarget.Database, LogLevel.Error);
                throw;
            }
        }

        internal void ImportPerformancePoints(IEnumerable<KeyValuePair<Guid, double>> performancePoints)
        {
            KeyValuePair<Guid, double>[] validEntries = performancePoints.Where(p => p.Key != Guid.Empty && p.Value >= 0).ToArray();

            if (validEntries.Length == 0)
                return;

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());

                realm.Write(() =>
                {
                    foreach ((Guid beatmapId, double pp) in validEntries)
                    {
                        ppCache[beatmapId] = pp;
                        ppVersions[beatmapId] = 0;

                        var existing = realm.Find<ForkBeatmapData>(beatmapId);

                        if (existing != null)
                        {
                            existing.MaxPerformancePoints = pp;
                            existing.PerformancePointsCalculated = true;
                            existing.PerformancePointsVersion = 0;
                        }
                        else
                            realm.Add(new ForkBeatmapData { BeatmapID = beatmapId, MaxPerformancePoints = pp, PerformancePointsCalculated = true });
                    }
                });
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to import beatmap PP values: {e.Message}", LoggingTarget.Database, LogLevel.Error);
                throw;
            }
        }

        public void SetPP(Guid beatmapId, double pp, int calculationVersion)
        {
            ppCache[beatmapId] = pp;

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());

                realm.Write(() =>
                {
                    var existing = realm.Find<ForkBeatmapData>(beatmapId);

                    if (existing != null)
                    {
                        existing.MaxPerformancePoints = pp;
                        existing.PerformancePointsCalculated = true;
                        existing.PerformancePointsVersion = calculationVersion;
                    }
                    else
                        realm.Add(new ForkBeatmapData
                        {
                            BeatmapID = beatmapId,
                            MaxPerformancePoints = pp,
                            PerformancePointsCalculated = true,
                            PerformancePointsVersion = calculationVersion,
                        });
                });

                ppVersions[beatmapId] = calculationVersion;
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to write PP for {beatmapId}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        public void SetRelaxData(Guid beatmapId, double starRating, double pp)
        {
            ForkRelaxPpSystem system = RelaxPpSystemSelection.Current;
            int calculationVersion = GetRelaxCalculationVersion(system);

            getRelaxCache()[beatmapId] = new RelaxBeatmapData(starRating, pp);

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());

                realm.Write(() =>
                {
                    var existing = realm.Find<ForkBeatmapData>(beatmapId);

                    if (existing == null)
                    {
                        existing = new ForkBeatmapData { BeatmapID = beatmapId };
                        realm.Add(existing);
                    }

                    if (system == ForkRelaxPpSystem.MosuPp)
                    {
                        existing.RelaxMosuPpStarRating = starRating;
                        existing.RelaxMosuPpMaxPerformancePoints = pp;
                        existing.RelaxMosuPpPerformancePointsCalculated = true;
                        existing.RelaxMosuPpPerformancePointsVersion = calculationVersion;
                    }
                    else if (system == ForkRelaxPpSystem.LazerVanilla)
                    {
                        existing.RelaxVanillaStarRating = starRating;
                        existing.RelaxVanillaMaxPerformancePoints = pp;
                        existing.RelaxVanillaPerformancePointsCalculated = true;
                        existing.RelaxVanillaPerformancePointsVersion = calculationVersion;
                    }
                    else
                    {
                        existing.RelaxStarRating = starRating;
                        existing.RelaxMaxPerformancePoints = pp;
                        existing.RelaxPerformancePointsCalculated = true;
                        existing.RelaxPerformancePointsVersion = calculationVersion;
                    }
                });

                getRelaxVersions()[beatmapId] = calculationVersion;
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to write Relax data for {beatmapId}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        public void SetDodgeDifficulty(Guid beatmapId, double starRating, int difficultyVersion)
        {
            if (beatmapId == Guid.Empty || !double.IsFinite(starRating) || starRating < 0 || difficultyVersion <= 0)
                return;

            writeDodgeDifficulty(beatmapId, new DodgeDifficultyData(starRating, difficultyVersion, string.Empty, string.Empty));
        }

        public void SetDodgeDifficulty(Guid beatmapId, string? beatmapChecksum, DifficultyAttributes attributes, int difficultyVersion)
        {
            if (beatmapId == Guid.Empty
                || string.IsNullOrEmpty(beatmapChecksum)
                || attributes == null
                || !double.IsFinite(attributes.StarRating)
                || attributes.StarRating < 0
                || difficultyVersion <= 0)
                return;

            string attributesJson;

            try
            {
                attributesJson = JsonConvert.SerializeObject(attributes);
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to serialise Dodge difficulty for {beatmapId}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
                return;
            }

            writeDodgeDifficulty(beatmapId, new DodgeDifficultyData(
                attributes.StarRating,
                difficultyVersion,
                beatmapChecksum,
                attributesJson));
        }

        private void writeDodgeDifficulty(Guid beatmapId, DodgeDifficultyData data)
        {
            if (dodgeDifficultyCache.TryGetValue(beatmapId, out DodgeDifficultyData existingData)
                && existingData == data)
                return;

            dodgeDifficultyCache[beatmapId] = data;

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());

                realm.Write(() =>
                {
                    var existing = realm.Find<ForkBeatmapData>(beatmapId);

                    if (existing == null)
                    {
                        existing = new ForkBeatmapData { BeatmapID = beatmapId };
                        realm.Add(existing);
                    }

                    existing.DodgeStarRating = data.StarRating;
                    existing.DodgeDifficultyVersion = data.DifficultyVersion;
                    existing.DodgeBeatmapChecksum = data.BeatmapChecksum;
                    existing.DodgeDifficultyAttributesJson = data.AttributesJson;
                });
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to write Dodge difficulty for {beatmapId}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        public HashSet<Guid> GetBeatmapIdsWithoutPP(IEnumerable<Guid> allIds)
        {
            return allIds.Where(id => GetPP(id) <= 0).ToHashSet();
        }

        public readonly record struct RelaxBeatmapData(double StarRating, double MaxPerformancePoints)
        {
            public RelaxBeatmapData()
                : this(-1, -1)
            {
            }
        }

        public readonly record struct DodgeDifficultyData(
            double StarRating,
            int DifficultyVersion,
            string BeatmapChecksum,
            string AttributesJson)
        {
            public bool HasFullAttributes => !string.IsNullOrWhiteSpace(AttributesJson);

            public DodgeDifficultyData()
                : this(-1, 0, string.Empty, string.Empty)
            {
            }
        }

        public void Dispose()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
