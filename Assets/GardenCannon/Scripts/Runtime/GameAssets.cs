using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Root content asset (Addressables group Default, address <see cref="Address"/>).
    /// The scene only holds the address; everything below is loaded through Addressables, and each
    /// referenced asset lives in its own group (prefabs/sprites: Default, SFX: AudioClips, font: LocalFont).
    /// </summary>
    [CreateAssetMenu(menuName = "Garden Cannon/Game Assets", fileName = "GameAssets")]
    public class GameAssets : ScriptableObject
    {
        public const string Address = "GardenCannon/GameAssets";

        public ItemCatalog catalog;
        public GameObject ballPrefab;
        public Font font;

        [Header("Background")]
        public Sprite grassTop;
        public Sprite grassTop12x17;
        public Sprite water;
        public Sprite grassBottom;

        [Header("Cannon / aim")]
        public Sprite cannon;
        public Sprite cannonShadow;
        public Sprite aimDot;
        public Sprite sparkle;

        [Header("HUD")]
        public Sprite levelPlate;
        public Sprite settingsButton;
        public Sprite white;

        [Header("SFX")]
        public AudioClip ballFire;
        public AudioClip ballFireRefused;
        public AudioClip wallBounce;
        public AudioClip ballCollide;
        public AudioClip waterSplash;
        public AudioClip aimTick;
        public AudioClip lastMoves;
        public AudioClip refillLand;

        [Header("UI / flow SFX (extracted from audioclips_assets_all)")]
        public AudioClip buttonClick;
        public AudioClip appear;
        public AudioClip goalHit;
        public AudioClip goalComplete;
        public AudioClip levelWin;
        public AudioClip levelWinFireworks;
        public AudioClip outOfBalls;
        public AudioClip levelFail;

        [Header("Music")]
        public AudioClip music;

        [Header("Animation")]
        [Tooltip("Original CannonController (Idle / Fire / PlayOn). Clips animate Piv/Base/Rail.")]
        public RuntimeAnimatorController cannonAnimator;
    }
}
