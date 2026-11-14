using System;
using System.Linq;
using Sonat.Game.Haptics;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>Persists local haptics tuning overrides and restores the game's asset values on reset.</summary>
    public static class HapticsLocalOverrides
    {
        // v2: new defaults and moments 18-33; tuning saved by older builds is dropped so the new defaults show.
        private const string PrefKey = "gardencannon.haptics.localOverrides.v2";

        [Serializable]
        private sealed class Snapshot
        {
            public ConfigSnapshot config;
            public MapSnapshot map;
        }

        [Serializable]
        private sealed class ConfigSnapshot
        {
            public bool enabled;
            public int minIntervalMs;
            public float intensity;
            public int maxQuality;
            public int minimalMinType;
            public int androidMaxTier;
            public int noAmplitudeExtraMs;
            public string[] androidBlocklist;
            public string[] basicList;
            public HapticTypeTuning[] types;
        }

        [Serializable]
        private sealed class MapSnapshot
        {
            public MapEntry[] entries;
            public float comboBase;
            public float comboStep;
            public int comboPresetFrom;
            public string comboPreset;
        }

        [Serializable]
        private sealed class MapEntry
        {
            public int moment;
            public bool enabled;
            public int type;
            public float intensity;
            public string preset;
            public int minGapMs;
        }

        private static ConfigSnapshot s_DefaultConfig;
        private static MapSnapshot s_DefaultMap;
        private static bool s_DefaultsCaptured;

        public static void Load()
        {
            CaptureDefaults();
            if (!PlayerPrefs.HasKey(PrefKey)) return;

            Snapshot snapshot;
            try
            {
                snapshot = JsonUtility.FromJson<Snapshot>(PlayerPrefs.GetString(PrefKey));
            }
            catch (ArgumentException exception)
            {
                ResetInvalidData(exception.Message);
                return;
            }
            catch (FormatException exception)
            {
                ResetInvalidData(exception.Message);
                return;
            }

            if (snapshot?.config == null || snapshot.config.types == null || snapshot.map == null)
            {
                ResetInvalidData("The saved haptics data is incomplete.");
                return;
            }

            ApplyConfig(snapshot.config);
            ApplyMap(snapshot.map);
            HapticsConfig.Current.NotifyChanged();
        }

        public static void Save()
        {
            CaptureDefaults();
            var snapshot = new Snapshot
            {
                config = CaptureConfig(),
                map = CaptureMap()
            };
            PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(snapshot));
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            CaptureDefaults();
            ApplyConfig(s_DefaultConfig);
            ApplyMap(s_DefaultMap);
            HapticsConfig.Current.NotifyChanged();
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }

        public static void ResetConfigToDefaults()
        {
            CaptureDefaults();
            ApplyConfig(s_DefaultConfig);
            HapticsConfig.Current.NotifyChanged();
            Save();
        }

        public static void ResetMapToDefaults()
        {
            CaptureDefaults();
            ApplyMap(s_DefaultMap);
            Save();
        }

        private static void CaptureDefaults()
        {
            if (s_DefaultsCaptured) return;
            s_DefaultConfig = CaptureConfig();
            s_DefaultMap = CaptureMap();
            s_DefaultsCaptured = true;
        }

        private static void ResetInvalidData(string reason)
        {
            Debug.LogWarning($"[Haptics] Could not load local tuning; restoring game defaults. {reason}");
            ResetToDefaults();
        }

        private static ConfigSnapshot CaptureConfig()
        {
            var config = HapticsConfig.Current;
            return new ConfigSnapshot
            {
                enabled = config.enabled,
                minIntervalMs = config.minIntervalMs,
                intensity = config.intensity,
                maxQuality = (int)config.maxQuality,
                minimalMinType = (int)config.minimalMinType,
                androidMaxTier = (int)config.androidMaxTier,
                noAmplitudeExtraMs = config.noAmplitudeExtraMs,
                androidBlocklist = config.androidBlocklist?.ToArray() ?? Array.Empty<string>(),
                basicList = config.basicList?.ToArray() ?? Array.Empty<string>(),
                types = Enum.GetValues(typeof(HapticType)).Cast<HapticType>()
                    .Select(type => new HapticTypeTuning { type = type, tuning = config.TuningOf(type).Clone() })
                    .ToArray()
            };
        }

        private static MapSnapshot CaptureMap()
        {
            var map = GameHapticsMap.Current;
            return new MapSnapshot
            {
                entries = map.entries?.Select(entry => entry == null ? null : new MapEntry
                {
                    moment = (int)entry.moment,
                    enabled = entry.enabled,
                    type = (int)entry.type,
                    intensity = entry.intensity,
                    preset = entry.preset != null ? entry.preset.name : null,
                    minGapMs = entry.minGapMs
                }).ToArray(),
                comboBase = map.comboBase,
                comboStep = map.comboStep,
                comboPresetFrom = map.comboPresetFrom,
                comboPreset = map.comboPreset != null ? map.comboPreset.name : null
            };
        }

        private static void ApplyConfig(ConfigSnapshot snapshot)
        {
            var config = HapticsConfig.Current;
            config.enabled = snapshot.enabled;
            config.minIntervalMs = Mathf.Max(0, snapshot.minIntervalMs);
            config.intensity = Mathf.Clamp01(snapshot.intensity);
            config.maxQuality = (HapticQuality)Mathf.Clamp(snapshot.maxQuality, 0, (int)HapticQuality.Full);
            config.minimalMinType = (HapticType)Mathf.Clamp(snapshot.minimalMinType, 0, (int)HapticType.Failure);
            config.androidMaxTier = (AndroidHapticTier)Mathf.Clamp(snapshot.androidMaxTier, 0, (int)AndroidHapticTier.Composition);
            config.noAmplitudeExtraMs = Mathf.Max(0, snapshot.noAmplitudeExtraMs);
            config.androidBlocklist = snapshot.androidBlocklist ?? Array.Empty<string>();
            config.basicList = snapshot.basicList ?? Array.Empty<string>();
            config.types = CloneTypes(snapshot.types);
        }

        private static void ApplyMap(MapSnapshot snapshot)
        {
            var map = GameHapticsMap.Current;
            map.entries = snapshot.entries?.Where(entry => entry != null &&
                    Enum.IsDefined(typeof(GameHapticMoment), entry.moment))
                .Select(entry => new GameHapticEntry((GameHapticMoment)entry.moment,
                    (HapticType)Mathf.Clamp(entry.type, 0, (int)HapticType.Failure),
                    Mathf.Clamp01(entry.intensity), entry.enabled, Mathf.Max(0, entry.minGapMs))
                {
                    preset = string.IsNullOrEmpty(entry.preset) ? null : HapticsConfig.Current.FindPreset(entry.preset)
                }).ToArray();
            map.comboBase = Mathf.Clamp01(snapshot.comboBase);
            map.comboStep = Mathf.Clamp(snapshot.comboStep, 0f, 0.5f);
            map.comboPresetFrom = Mathf.Max(1, snapshot.comboPresetFrom);
            map.comboPreset = string.IsNullOrEmpty(snapshot.comboPreset)
                ? null
                : HapticsConfig.Current.FindPreset(snapshot.comboPreset);
        }

        private static HapticTypeTuning[] CloneTypes(HapticTypeTuning[] types) =>
            types?.Where(row => row?.tuning != null)
                .Select(row => new HapticTypeTuning { type = row.type, tuning = row.tuning.Clone() })
                .ToArray() ?? Array.Empty<HapticTypeTuning>();
    }
}
