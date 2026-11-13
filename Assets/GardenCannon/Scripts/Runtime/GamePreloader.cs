using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GardenCannon
{
    /// <summary>
    /// Loads everything the game scene needs while the splash is on screen (GameAssets with its prefabs, sounds and
    /// sprites, and the levels), keeps it loaded for the whole session and warms the GPU up, so the game scene can
    /// build the first level on its very first frame.
    /// </summary>
    public static class GamePreloader
    {
        public static GameAssets Assets { get; private set; }
        public static IList<TextAsset> Levels { get; private set; }
        public static bool IsReady => Assets != null && Levels != null;

        static AsyncOperationHandle<GameAssets> _assets;
        static AsyncOperationHandle<IList<TextAsset>> _levels;

        public static IEnumerator Load(string assetsAddress, string levelsLabel)
        {
            if (IsReady) yield break;
            if (!_assets.IsValid()) _assets = Addressables.LoadAssetAsync<GameAssets>(assetsAddress);
            if (!_levels.IsValid()) _levels = Addressables.LoadAssetsAsync<TextAsset>(levelsLabel, null);
            yield return _assets;
            yield return _levels;
            if (_assets.Status == AsyncOperationStatus.Succeeded) Assets = _assets.Result;
            if (_levels.Status == AsyncOperationStatus.Succeeded) Levels = _levels.Result;
            if (Assets == null) Debug.LogError("[GardenCannon] Preload: GameAssets could not be loaded.");
            if (Levels == null) Debug.LogError("[GardenCannon] Preload: levels could not be loaded.");
            if (Levels != null) LevelLibrary.Initialize(Levels);
            if (Assets != null) LoadAudio(Assets);
        }

        /// <summary>Decompress the sound effects now (they are imported with "Preload Audio Data" off).</summary>
        static void LoadAudio(GameAssets a)
        {
            var clips = new List<AudioClip>
            {
                a.ballFire, a.ballFireRefused, a.wallBounce, a.ballCollide, a.waterSplash, a.aimTick, a.lastMoves,
                a.refillLand, a.buttonClick, a.appear, a.goalHit, a.goalComplete, a.levelWin, a.levelWinFireworks,
                a.outOfBalls, a.levelFail,
            };
            if (a.catalog != null)
                foreach (var e in a.catalog.entries)
                {
                    clips.Add(e.hitSfx); clips.Add(e.breakSfx); clips.Add(e.extraBreakSfx);
                    clips.Add(e.openSfx); clips.Add(e.partBreakSfx); clips.Add(e.altSfx);
                }
            foreach (var c in clips)
                if (c != null && c.loadState == AudioDataLoadState.Unloaded && c.loadType != AudioClipLoadType.Streaming)
                    c.LoadAudioData();
        }

        /// <summary>Every prefab the game can show (items in all sizes / colours, effects, 3D scenery, balls).</summary>
        public static List<GameObject> AllPrefabs()
        {
            var list = new List<GameObject>();
            var a = Assets;
            if (a == null) return list;
            void Add(GameObject g) { if (g != null && !list.Contains(g)) list.Add(g); }
            if (a.catalog != null)
                foreach (var e in a.catalog.entries)
                {
                    Add(e.prefab);
                    if (e.colorPrefabs != null) foreach (var cp in e.colorPrefabs) if (cp != null) Add(cp.prefab);
                    if (e.sizePrefabs != null) foreach (var p in e.sizePrefabs) Add(p);
                    Add(e.eggPrefab);
                }
            Add(a.ballPrefab); Add(a.cannon3D); Add(a.bg3D); Add(a.grass3D); Add(a.balloonPrefab);
            return list;
        }

        /// <summary>Effects are warmed separately (their particles must not play).</summary>
        public static List<GameObject> AllEffects()
        {
            var list = new List<GameObject>();
            var a = Assets;
            if (a == null) return list;
            void Add(GameObject g) { if (g != null && !list.Contains(g)) list.Add(g); }
            if (a.catalog != null)
                foreach (var e in a.catalog.entries) { Add(e.breakFx); Add(e.hitFx); Add(e.openFx); Add(e.hatchFx); Add(e.partFx); }
            Add(a.splashFx);
            return list;
        }
    }
}
