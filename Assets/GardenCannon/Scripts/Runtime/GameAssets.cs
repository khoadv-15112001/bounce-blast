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
        [Tooltip("Digits 0..9 for block HP labels (Art/Digits.png sliced).")]
        public Sprite[] digits = new Sprite[0];
        [Tooltip("GardenCannon/SpriteOverlay: digits are drawn on top of the (animated) block meshes.")]
        public Shader digitShader;
        [Tooltip("Two flapping frames of the egg box bird (Art/Bird.png, built from the original bird08 Spine parts).")]
        public Sprite[] birdFrames = new Sprite[0];

        [Header("FX (Art/FxAtlas.png + original confetti)")]
        public Sprite fxChip;
        public Sprite fxGlow;
        public Sprite fxStar;
        public Sprite[] confetti = new Sprite[0];
        [Tooltip("Original ball splash effect (Assets/GardenCannon/VFX/VFX_Ball_Splash).")]
        public GameObject splashFx;
        [Tooltip("Original animated water (Assets/Material/Water_GardenB, shader K1m/Sprite Add) for the 'Water' sprite near the cannon.")]
        public Material waterMaterial;

        [Header("3D scenery (Assets/3D/Prefab/Elements)")]
        public GameObject cannon3D;
        [Tooltip("Egg box: balloon floating away instead of the bird (Assets/3D/Prefab/Baloon).")]
        public GameObject balloonPrefab;
        [Header("Boosters")]
        public Sprite boosterX2Icon;
        public Sprite boosterRocketIcon;
        public Sprite boosterHammerIcon;
        public Sprite boosterBombIcon;
        public Sprite boosterElectroIcon;
        public Sprite boosterGloveIcon;
        public Sprite boosterKettlebellIcon;
        public Sprite boosterExtraMovesIcon;
        public Material[] balloonMaterials = new Material[0];
        public GameObject bg3D;
        [Tooltip("BG mesh without the white moves plate (made by Garden Cannon > Rebuild BG without the moves plate).")]
        public Mesh bgMesh;
        public GameObject grass3D;

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
