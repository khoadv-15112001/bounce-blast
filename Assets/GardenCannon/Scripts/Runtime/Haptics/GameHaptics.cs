using System.Collections.Generic;
using Sonat.Core.Services;
using Sonat.Game.Haptics;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Plays a game moment as <see cref="GameHapticsMap"/> says, through <see cref="IHapticService"/> (which drops what
    /// the device or the player's setting does not allow), and owns the player's vibration toggle.
    /// </summary>
    public static class GameHaptics
    {
        private const string VibrationPref = "gc_vibration_on";

        /// <summary>When each moment with a <see cref="GameHapticEntry.minGapMs"/> last played (unscaled seconds).</summary>
        private static readonly Dictionary<GameHapticMoment, float> s_LastPlayed = new Dictionary<GameHapticMoment, float>();
        private static bool s_Initialized;

        /// <summary>The player's vibration setting (Settings menu). On by default.</summary>
        public static bool VibrationOn
        {
            get => PlayerPrefs.GetInt(VibrationPref, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(VibrationPref, value ? 1 : 0);
                PlayerPrefs.Save();
                if (ServiceLocator.TryGet<IHapticService>(out var haptics)) haptics.Enabled = value;
                if (value) Play(GameHapticMoment.VibrationTurnedOn);
            }
        }

        /// <summary>Loads the tuning saved on this device and applies the player's toggle. Safe to call more than once.</summary>
        public static void Init()
        {
            if (s_Initialized) return;
            s_Initialized = true;
            HapticsLocalOverrides.Load();
            if (ServiceLocator.TryGet<IHapticService>(out var haptics)) haptics.Enabled = VibrationOn;
            else HapticsLog.Verbose($"Init: no {nameof(IHapticService)}");
        }

        public static void ToggleVibration() => VibrationOn = !VibrationOn;

        /// <param name="combo">For <see cref="GameHapticMoment.ItemBroken"/>: breaks by the same ball before this one.</param>
        public static void Play(GameHapticMoment moment, int combo = 0)
        {
            if (!ServiceLocator.TryGet<IHapticService>(out var haptics))
            {
                HapticsLog.Verbose($"{moment} skipped: no {nameof(IHapticService)}");
                return;
            }
            var map = GameHapticsMap.Current;
            var entry = map.Find(moment);
            if (entry == null || !entry.enabled)
            {
                HapticsLog.Verbose($"{moment} skipped: off in {nameof(GameHapticsMap)}");
                return;
            }

            if (entry.minGapMs > 0)
            {
                var now = Time.unscaledTime;
                if (s_LastPlayed.TryGetValue(moment, out var last) && (now - last) * 1000f < entry.minGapMs)
                {
                    HapticsLog.Verbose($"{moment} skipped: within its {entry.minGapMs} ms gap");
                    return;
                }
                s_LastPlayed[moment] = now;
            }

            var source = moment.ToString();
            var intensity = entry.intensity;
            if (moment == GameHapticMoment.ItemBroken)
            {
                source = $"{moment} combo {combo}";
                if (map.comboPreset != null && combo >= map.comboPresetFrom)
                {
                    haptics.Play(map.comboPreset, source: source);
                    return;
                }
                intensity = Mathf.Clamp01(entry.intensity * (map.comboBase + map.comboStep * Mathf.Max(0, combo)));
            }

            if (entry.preset != null) haptics.Play(entry.preset, intensity, source);
            else haptics.Play(entry.type, intensity, source);
        }
    }
}
