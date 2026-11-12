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

        AsyncOperationHandle<GameAssets> _assetsHandle;
        AsyncOperationHandle<IList<TextAsset>> _levelsHandle;

        IEnumerator Start()
        {
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
                while (s != null && !s.IsDone) yield return null;

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
            if (fx != null) fx.sparkleSprite = a.sparkle;

            if (sfx != null)
            {
                sfx.ballFire = a.ballFire;
                sfx.ballFireRefused = a.ballFireRefused;
                sfx.wallBounce = a.wallBounce;
                sfx.ballCollide = a.ballCollide;
                sfx.waterSplash = a.waterSplash;
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
