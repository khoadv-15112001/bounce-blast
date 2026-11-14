using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GardenCannon
{
    /// <summary>
    /// Entry point of the game scene. The layout (background, cannon, board, HUD anchors) is placed in the
    /// scene; sprites on it come from Addressables via <see cref="AddressableSprite"/>. This component loads
    /// the remaining content (GameAssets: catalog, ball, font, SFX; levels by label) through Addressables,
    /// hands it to the scene components and starts the game. Nothing uses Resources.
    /// </summary>
    [DefaultExecutionOrder(-500)] // Awake before the scene's AddressableSprites / other components
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Addressables")]
        public string gameAssetsAddress = GameAssets.Address;
        public string levelsLabel = LevelLibrary.Label;

        [Header("Scene")]
        public CameraRig rig;
        public Camera uiCamera;
        public Board board;
        public CannonView cannon;
        public AimGuide aimGuide;
        public GameAudio sfx;
        public GameHud hud;
        public GameController controller;
        public FxPool fx;
        [Tooltip("Lawn sprite renderer, swapped to the 12x17 lawn for 12-column levels.")]
        public SpriteRenderer lawnRenderer;
        [Tooltip("Row of waiting balls right of the cannon (off: that spot holds boosters now).")]
        public bool showAmmoBelt = false;

        AsyncOperationHandle<GameAssets> _assetsHandle;
        AsyncOperationHandle<IList<TextAsset>> _levelsHandle;

        /// <summary>
        /// First frame: with everything preloaded by the splash, put the lawn picture and the 3D scenery (Lawn3D under
        /// the transparent lawn, cannon, BG) in place right away, so the screen never shows the empty background.
        /// </summary>
        void Awake()
        {
            if (!GamePreloader.IsReady) return;
            var a = GamePreloader.Assets;
            if (lawnRenderer != null && a.grassTop != null) lawnRenderer.sprite = a.grassTop;
            ApplySceneArt(a);
        }

        void ApplySceneArt(GameAssets a)
        {
            if (a.cannon3D == null && a.bg3D == null && a.grass3D == null) return;
            var art = GetComponent<SceneArt3D>();
            if (art == null) art = gameObject.AddComponent<SceneArt3D>();
            art.cannonPrefab = a.cannon3D;
            art.bgPrefab = a.bg3D;
            art.bgMesh = a.bgMesh;
            art.grassPrefab = a.grass3D;   // checker lawn drawn under the GrassTop picture (its lawn is transparent)
            art.Apply(board, cannon, hud);
            if (controller != null) controller.sceneArt = art;
        }

        IEnumerator Start()
        {
            // Normal path: the splash already loaded everything (GamePreloader) -> build the level on the first frame.
            // (The lawn picture is the same for both board sizes now, so a late AddressableSprite load can't undo it.)
            if (GamePreloader.IsReady)
            {
                LevelLibrary.Initialize(GamePreloader.Levels);
                Wire(GamePreloader.Assets);
                yield break;
            }

            // Game scene started directly (editor): load here.
            _assetsHandle = Addressables.LoadAssetAsync<GameAssets>(gameAssetsAddress);
            _levelsHandle = Addressables.LoadAssetsAsync<TextAsset>(levelsLabel, null);
            yield return _assetsHandle;
            yield return _levelsHandle;

            if (_assetsHandle.Status != AsyncOperationStatus.Succeeded || _assetsHandle.Result == null)
            {
                Debug.LogError($"[GardenCannon] Could not load '{gameAssetsAddress}' from Addressables. Run Garden Cannon > Setup.");
                yield break;
            }
            if (_levelsHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[GardenCannon] Could not load levels with label '{levelsLabel}'. Run Garden Cannon > Setup.");
                yield break;
            }

            // Wait for the scene sprites so the lawn swap below is not overwritten by a late load.
            foreach (var s in FindObjectsOfType<AddressableSprite>(true))
                while (s != null && s.isActiveAndEnabled && !s.IsDone) yield return null;

            LevelLibrary.Initialize(_levelsHandle.Result);
            Wire(_assetsHandle.Result);
        }

        void OnDestroy()
        {
            if (_assetsHandle.IsValid()) Addressables.Release(_assetsHandle);
            if (_levelsHandle.IsValid()) Addressables.Release(_levelsHandle);
        }

        void Wire(GameAssets a)
        {
            if (aimGuide != null) aimGuide.dotSprite = a.aimDot;
            if (cannon != null) cannon.UseAnimator(a.cannonAnimator);
            if (fx != null)
            {
                fx.chipSprite = a.fxChip;
                fx.glowSprite = a.fxGlow;
                fx.starSprite = a.fxStar != null ? a.fxStar : a.sparkle;
                fx.confettiSprites = a.confetti;
                fx.splashFx = a.splashFx;
            }
            if (fx != null) fx.birdFrames = a.birdFrames;
            if (showAmmoBelt && controller != null && a.ballPrefab != null && cannon != null)
            {
                // Balls waiting on the right of the cannon; one rolls in on each shot.
                var belt = controller.ammoBelt;
                if (belt == null)
                {
                    belt = new GameObject("AmmoBelt").AddComponent<AmmoBelt>();
                    belt.transform.SetParent(transform, false);
                }
                belt.ballPrefab = a.ballPrefab;
                belt.cannon = cannon.transform;
                controller.ammoBelt = belt;
            }
            if (controller != null && cannon != null)
            {
                // Boosters left of the cannon (x2 fireballs, rocket).
                var bar = controller.boosters;
                if (bar == null)
                {
                    bar = new GameObject("Boosters").AddComponent<BoosterBar>();
                    bar.transform.SetParent(transform, false);
                }
                if (bar.boosters.Count == 0) bar.boosters = BoosterBar.DefaultDefs();
                foreach (var d in bar.boosters)
                {
                    if (d.icon != null) continue;
                    d.icon = d.id switch
                    {
                        BoosterId.X2 => a.boosterX2Icon,
                        BoosterId.Rocket => a.boosterRocketIcon,
                        BoosterId.Hammer => a.boosterHammerIcon,
                        BoosterId.Bomb => a.boosterBombIcon,
                        BoosterId.Electro => a.boosterElectroIcon,
                        BoosterId.Glove => a.boosterGloveIcon,
                        BoosterId.Kettlebell => a.boosterKettlebellIcon,
                        _ => a.boosterExtraMovesIcon,
                    };
                }
                bar.font = a.font;
                bar.cannon = cannon.transform;
                controller.boosters = bar;
            }
            if (fx != null)
            {
                fx.balloonPrefab = a.balloonPrefab;
                fx.balloonMaterials = a.balloonMaterials;
            }
            ApplySceneArt(a);
            if (a.waterMaterial != null)
            {
                // Animated caustics / foam on the water strip in front of the cannon (as in the original Game scene).
                foreach (var sr in FindObjectsOfType<SpriteRenderer>(true))
                    if (sr.gameObject.name == "Water") sr.sharedMaterial = a.waterMaterial;
            }
            DigitLabel.Digits = a.digits;
            if (a.digitShader != null && DigitLabel.OverlayMaterial == null)
                DigitLabel.OverlayMaterial = new Material(a.digitShader) { name = "DigitOverlay" };

            if (sfx != null)
            {
                sfx.ballFire = a.ballFire;
                sfx.ballFireRefused = a.ballFireRefused;
                sfx.wallBounce = a.wallBounce;
                sfx.ballCollide = a.ballCollide;
                sfx.waterSplash = a.waterSplash;
                // Clips are imported with "Preload Audio Data" off: load them now so the first play isn't silent.
                if (a.waterSplash != null) a.waterSplash.LoadAudioData();
                sfx.aimTick = a.aimTick;
                sfx.lastMoves = a.lastMoves;
                sfx.refillLand = a.refillLand;
                sfx.buttonClick = a.buttonClick;
                sfx.appear = a.appear;
                sfx.goalHit = a.goalHit;
                sfx.goalComplete = a.goalComplete;
                sfx.levelWin = a.levelWin;
                sfx.levelWinFireworks = a.levelWinFireworks;
                sfx.outOfBalls = a.outOfBalls;
                sfx.levelFail = a.levelFail;
                sfx.music = a.music;
            }

            if (hud != null)
            {
                hud.uiCamera = uiCamera;
                hud.font = a.font;
                hud.whiteSprite = a.white;
                hud.panelSprite = a.white;
                // Level plate and restart button are already placed in the scene.
                hud.levelPlate = null;
                hud.settingsButton = null;
            }

            controller.rig = rig;
            controller.fx = fx;
            controller.board = board;
            controller.cannon = cannon;
            controller.aimGuide = aimGuide;
            controller.hud = hud;
            controller.sfx = sfx;
            controller.catalog = a.catalog;
            controller.ballPrefab = a.ballPrefab;
            controller.lawnRenderer = lawnRenderer;
            controller.lawn9x12 = a.grassTop;
            controller.lawn12x17 = a.grassTop12x17;
            controller.Begin();
        }
    }
}
