using System;
using System.Linq;
using Sonat.Game.Haptics;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// The moments of Garden Cannon that vibrate; the code calls <see cref="GameHaptics.Play"/> with one.
    /// Values are explicit because assets and saved overrides store the number: never renumber, only add.
    /// </summary>
    public enum GameHapticMoment
    {
        /// <summary>The cannon turns while aiming (with the aim tick sound).</summary>
        AimTick = 0,
        /// <summary>The first ball of a shot leaves the cannon.</summary>
        Fire = 1,
        /// <summary>A shot the game refused (balls still flying, no moves).</summary>
        FireRefused = 2,
        /// <summary>Five moves left.</summary>
        LastMoves = 3,
        /// <summary>A ball bounces off a wall.</summary>
        WallBounce = 4,
        /// <summary>A ball hits a solid item that can never break.</summary>
        ItemHit = 5,
        /// <summary>An item breaks; stronger with the ball's combo.</summary>
        ItemBroken = 6,
        /// <summary>A multi-cell item breaks.</summary>
        BigItemBroken = 7,
        /// <summary>A box / egg box opens (its lid flies off).</summary>
        ObstacleStep = 8,
        /// <summary>A dynamite wall explodes.</summary>
        Explosion = 9,
        /// <summary>A goal counter goes down.</summary>
        GoalHit = 10,
        /// <summary>A goal counter reaches 0.</summary>
        GoalComplete = 11,
        /// <summary>A ball drops into the water.</summary>
        BallSplash = 12,
        /// <summary>The level is won.</summary>
        LevelWon = 13,
        /// <summary>The win fireworks start.</summary>
        WinFireworks = 14,
        /// <summary>The level is lost (out of moves).</summary>
        LevelLost = 15,
        /// <summary>A HUD or popup button press.</summary>
        UiButton = 16,
        /// <summary>The player turns vibration on in Settings.</summary>
        VibrationTurnedOn = 17,
        /// <summary>A crate of a wooden box, or a log fed into the cutter, breaks (the item lives on).</summary>
        PartBroken = 18,
        /// <summary>An electric orb switches on.</summary>
        OrbActivated = 19,
        /// <summary>A brick wall's dynamite fuse is lit.</summary>
        FuseLit = 20,
        /// <summary>An egg hatches (a bird flies away).</summary>
        EggHatched = 21,
        /// <summary>The electric orbs switch off because the chain stalled.</summary>
        OrbsReset = 22,
        /// <summary>All orbs lit: the electric orb item discharges.</summary>
        OrbsDischarged = 23,
        /// <summary>A ball hits an obstacle that does not react (orb already on, burning fuse).</summary>
        ItemBlocked = 24,
        /// <summary>A breakable item takes a hit but does not break yet.</summary>
        ItemDamaged = 25,
        /// <summary>Balls 2..N of a volley leave the cannon.</summary>
        VolleyBall = 26,
        /// <summary>The finger goes down and aiming starts.</summary>
        AimStart = 27,
        /// <summary>A level starts (the cannon is refilled).</summary>
        LevelStart = 28,
        /// <summary>The tutorial banner appears (level 1).</summary>
        TutorialShown = 29,
        /// <summary>The Win / Lose popup appears.</summary>
        PopupShown = 30,
        /// <summary>One move left.</summary>
        LastMove = 31,
        /// <summary>A ball's combo reaches 5, 10, 15...</summary>
        ComboMilestone = 32,
        /// <summary>The last ball in play drops into the water (the shot is over).</summary>
        ShotEnded = 33,
    }

    [Serializable]
    public sealed class GameHapticEntry
    {
        public GameHapticMoment moment;
        public bool enabled = true;
        [Tooltip("Played when Preset is empty.")]
        public HapticType type = HapticType.Medium;
        [Range(0f, 1f)] public float intensity = 1f;
        [Tooltip("A designer-made feedback; replaces Type when set.")]
        public HapticPreset preset;
        [Tooltip("Within this many ms of this moment's last play it is skipped. 0 = no gap.")]
        [Min(0)] public int minGapMs;

        public GameHapticEntry() { }

        public GameHapticEntry(GameHapticMoment moment, HapticType type, float intensity = 1f, bool enabled = true,
            int minGapMs = 0)
        {
            this.moment = moment;
            this.type = type;
            this.intensity = intensity;
            this.enabled = enabled;
            this.minGapMs = minGapMs;
        }
    }

    /// <summary>
    /// What each moment of the game plays, at <c>Resources/GameHapticsMap</c>: a built-in type at a strength, or a
    /// <see cref="HapticPreset"/>. Without the asset the defaults below apply. Tuned on device in the Game Haptics Map
    /// screen (Settings in a development build).
    /// </summary>
    [CreateAssetMenu(menuName = "GardenCannon/Haptics Map", fileName = ResourceName)]
    public sealed class GameHapticsMap : ScriptableObject
    {
        public const string ResourceName = "GameHapticsMap";

        public GameHapticEntry[] entries = Defaults();

        [Header("Item broken: intensity = Combo Base + Combo Step × combo (combo = breaks by the same ball before)")]
        [Range(0f, 1f)] public float comboBase = 0.5f;
        [Range(0f, 0.5f)] public float comboStep = 0.06f;
        [Tooltip("From this combo on, Combo Preset plays instead (when set).")]
        [Min(1)] public int comboPresetFrom = 5;
        public HapticPreset comboPreset;

        private static GameHapticsMap s_Loaded;
        private static GameHapticsMap s_Default;

        /// <summary>The project's map, or the defaults.</summary>
        public static GameHapticsMap Current
        {
            get
            {
                if (s_Loaded != null) return s_Loaded;
                s_Loaded = Resources.Load<GameHapticsMap>(ResourceName);
                return s_Loaded != null ? s_Loaded : s_Default != null ? s_Default : s_Default = CreateInstance<GameHapticsMap>();
            }
        }

        /// <summary>The entry of <paramref name="moment"/>: its row, or the default when the asset has none.</summary>
        public GameHapticEntry Find(GameHapticMoment moment)
        {
            if (entries != null)
                foreach (var entry in entries)
                    if (entry != null && entry.moment == moment) return entry;
            foreach (var entry in Defaults())
                if (entry.moment == moment) return entry;
            return null;
        }

        /// <summary>The row of <paramref name="moment"/> to change in place; adds the default row when the asset has none.</summary>
        public GameHapticEntry Row(GameHapticMoment moment)
        {
            if (entries != null)
                foreach (var entry in entries)
                    if (entry != null && entry.moment == moment) return entry;
            var row = Find(moment) ?? new GameHapticEntry(moment, HapticType.Medium);
            entries = (entries ?? Array.Empty<GameHapticEntry>()).Append(row).ToArray();
            return row;
        }

        // ON = confident it feels right; OFF = worth trying on device (toggle it in Game Haptics Map).
        // Frequent moments (several per second with a 5-ball volley) get a minGapMs so they never turn into a buzz.
        public static GameHapticEntry[] Defaults() => new[]
        {
            // Aim & fire
            new GameHapticEntry(GameHapticMoment.AimStart, HapticType.Selection, 0.5f, enabled: false),
            new GameHapticEntry(GameHapticMoment.AimTick, HapticType.Selection, 0.35f, minGapMs: 70),
            new GameHapticEntry(GameHapticMoment.Fire, HapticType.Medium, 0.75f),
            new GameHapticEntry(GameHapticMoment.VolleyBall, HapticType.Selection, 0.35f, enabled: false),
            new GameHapticEntry(GameHapticMoment.FireRefused, HapticType.Warning, 0.5f, minGapMs: 300),
            new GameHapticEntry(GameHapticMoment.LastMoves, HapticType.Warning),
            new GameHapticEntry(GameHapticMoment.LastMove, HapticType.Warning, 0.7f, enabled: false),
            // Ball contacts
            new GameHapticEntry(GameHapticMoment.WallBounce, HapticType.Selection, 0.3f, enabled: false, minGapMs: 80),
            new GameHapticEntry(GameHapticMoment.ItemHit, HapticType.Soft, 0.4f, minGapMs: 60),
            new GameHapticEntry(GameHapticMoment.ItemDamaged, HapticType.Light, 0.5f, minGapMs: 50),
            new GameHapticEntry(GameHapticMoment.ItemBlocked, HapticType.Selection, 0.4f, enabled: false, minGapMs: 80),
            new GameHapticEntry(GameHapticMoment.ItemBroken, HapticType.Medium),
            new GameHapticEntry(GameHapticMoment.ComboMilestone, HapticType.Rigid, 0.8f, enabled: false),
            new GameHapticEntry(GameHapticMoment.BigItemBroken, HapticType.Heavy),
            new GameHapticEntry(GameHapticMoment.BallSplash, HapticType.Soft, 0.3f, enabled: false, minGapMs: 120),
            new GameHapticEntry(GameHapticMoment.ShotEnded, HapticType.Soft, 0.4f, enabled: false),
            // Obstacles
            new GameHapticEntry(GameHapticMoment.ObstacleStep, HapticType.Rigid, 0.8f),
            new GameHapticEntry(GameHapticMoment.EggHatched, HapticType.Medium, 0.7f, minGapMs: 50),
            new GameHapticEntry(GameHapticMoment.PartBroken, HapticType.Rigid, 0.6f, minGapMs: 50),
            new GameHapticEntry(GameHapticMoment.OrbActivated, HapticType.Light, 0.7f, minGapMs: 40),
            new GameHapticEntry(GameHapticMoment.OrbsDischarged, HapticType.Heavy, 0.9f),
            new GameHapticEntry(GameHapticMoment.OrbsReset, HapticType.Warning, 0.5f, minGapMs: 200),
            new GameHapticEntry(GameHapticMoment.FuseLit, HapticType.Soft, 0.8f),
            new GameHapticEntry(GameHapticMoment.Explosion, HapticType.Heavy, minGapMs: 80),
            // Goals & level flow
            new GameHapticEntry(GameHapticMoment.GoalHit, HapticType.Light, 0.35f, enabled: false, minGapMs: 100),
            new GameHapticEntry(GameHapticMoment.GoalComplete, HapticType.Rigid),
            new GameHapticEntry(GameHapticMoment.LevelStart, HapticType.Light, 0.6f),
            new GameHapticEntry(GameHapticMoment.TutorialShown, HapticType.Light, 0.5f, enabled: false),
            new GameHapticEntry(GameHapticMoment.LevelWon, HapticType.Success),
            new GameHapticEntry(GameHapticMoment.WinFireworks, HapticType.Heavy, 0.8f),
            new GameHapticEntry(GameHapticMoment.LevelLost, HapticType.Failure),
            new GameHapticEntry(GameHapticMoment.PopupShown, HapticType.Light, 0.6f, enabled: false),
            // UI
            new GameHapticEntry(GameHapticMoment.UiButton, HapticType.Light, 0.6f),
            new GameHapticEntry(GameHapticMoment.VibrationTurnedOn, HapticType.Heavy),
        };

        /// <summary>Every moment, grouped as in <see cref="Defaults"/>, for the tuning screen.</summary>
        public static GameHapticMoment[] DisplayOrder() =>
            Defaults().Select(entry => entry.moment)
                .Concat(((GameHapticMoment[])Enum.GetValues(typeof(GameHapticMoment))))
                .Distinct().ToArray();

        [ContextMenu("Reset entries to defaults")]
        private void ResetEntries() => entries = Defaults();
    }
}
