using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace GardenCannon
{
    /// <summary>
    /// Splash scene, laid out like the original Intro scene: full-screen "splash asset" background (cover-fit),
    /// logo at y 8.98, "Loading" text 2.25 above the bottom edge. Initialises Addressables, preloads the level data
    /// and opens the game scene.
    /// </summary>
    [DefaultExecutionOrder(-1000)] // before AddressableSprite.OnEnable, so the direct sprites win
    public class SplashView : MonoBehaviour
    {
        public Camera cam;
        [Tooltip("Background sprite renderer, scaled to cover the whole screen.")]
        public SpriteRenderer background;
        public Transform logo;
        [Tooltip("Shown on the very first frame (built into the Splash scene, not loaded from Addressables).")]
        public Sprite backgroundSprite;
        public Sprite logoSprite;
        [Tooltip("Gap kept between the logo and the top of the screen when the background is cropped (wide screens, iPad).")]
        public float logoTopMargin = 0.35f;
        [Tooltip("The logo never gets wider than this share of the screen width.")]
        [Range(0.5f, 1f)] public float logoMaxWidth = 0.92f;

        const string BackgroundPath = "Assets/SpriteExport/splash_bg.png";
        const string LogoPath = "Assets/SpriteExport/splash_logo.png";

        void Awake()
        {
            // The splash art is the first thing on screen: assign it directly so there is no blue frame while the
            // Addressables catalog / bundles are still loading.
            if (background != null && backgroundSprite != null)
            {
                background.sprite = backgroundSprite;
                var a = background.GetComponent<AddressableSprite>();
                if (a != null) a.enabled = false;
                FitBackground();
            }
            if (logo != null && logoSprite != null)
            {
                var sr = logo.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = logoSprite;
                var a = logo.GetComponent<AddressableSprite>();
                if (a != null) a.enabled = false;
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            bool changed = false;
            if (backgroundSprite == null) { backgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath); changed |= backgroundSprite != null; }
            if (logoSprite == null) { logoSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath); changed |= logoSprite != null; }
            if (changed) UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>Editor/build helper: fill the direct sprite references.</summary>
        public void EnsureSprites() => OnValidate();
#endif
        public Transform loadingAnchor;
        public AssetReferenceT<Font> font;
        public string loadingText = "Loading";
        public Color loadingColor = Color.white;
        public string gameScene = "GardenCannon";
        [Tooltip("Minimum time the splash stays on screen (seconds).")]
        public float minDuration = 1.5f;

        TextMesh _text, _shadow;
        Vector3 _logoScale;
        Vector3 _logoLocal;
        bool _logoLocalSet;
        float _t;

        IEnumerator Start()
        {
            if (cam == null) cam = Camera.main;
            if (logo != null) { _logoScale = logo.localScale; logo.localScale = Vector3.zero; }
            if (logo != null && !_logoLocalSet) { _logoLocal = logo.localPosition; _logoLocalSet = true; }

            yield return Addressables.InitializeAsync();

            if (font != null && font.RuntimeKeyIsValid())
            {
                var fh = font.LoadAssetAsync<Font>();
                yield return fh;
                if (fh.Status == AsyncOperationStatus.Succeeded && loadingAnchor != null)
                {
                    _shadow = VisualUtil.MakeText("LoadingShadow", loadingAnchor, fh.Result, new Vector3(0.05f, -0.07f, 0.01f), 1.1f, new Color(0.1f, 0.2f, 0.05f, 0.6f), TextAnchor.MiddleCenter, 20);
                    _text = VisualUtil.MakeText("LoadingText", loadingAnchor, fh.Result, Vector3.zero, 1.1f, loadingColor, TextAnchor.MiddleCenter, 21);
                }
            }

            // Load the whole game now (assets, prefabs, sounds, levels) and warm the GPU up, so the game scene
            // can build the first level on its very first frame instead of loading after the scene switch.
            yield return GamePreloader.Load(GameAssets.Address, LevelLibrary.Label);
            yield return WarmUp();

            // Wait for the splash sprites (background / logo) to come out of Addressables.
            foreach (var s in FindObjectsOfType<AddressableSprite>(true))
                while (s != null && s.isActiveAndEnabled && !s.IsDone) yield return null;

            // Warm up the level bundle so the game scene starts faster.
            var levels = Addressables.DownloadDependenciesAsync(LevelLibrary.Label);
            yield return levels;
            if (levels.IsValid()) Addressables.Release(levels);

            while (_t < minDuration) yield return null;

            // Load the game scene only now. A scene load held with allowSceneActivation = false blocks every async
            // load queued after it (asset bundles included), which froze the first launch on "Loading".
            yield return SceneManager.LoadSceneAsync(gameScene);
        }

        void Update()
        {
            _t += Time.deltaTime;
            FitBackground();
            float fit = FitLogo();
            if (logo != null && _logoScale != Vector3.zero)
            {
                float k = Mathf.Clamp01(_t / 0.45f);
                float back = 1f + 2.70158f * Mathf.Pow(k - 1f, 3f) + 1.70158f * Mathf.Pow(k - 1f, 2f); // ease out back
                logo.localScale = _logoScale * fit * back * (1f + 0.02f * Mathf.Sin(_t * 3f));
            }
            if (_text != null)
            {
                string s = loadingText + new string('.', 1 + (int)(_t * 3f) % 3);
                _text.text = _shadow.text = s;
            }
        }

        /// <summary>
        /// Draw every game model once, hidden behind the splash background, so meshes / textures are uploaded and
        /// shaders compiled now (first draws on phones otherwise stall the first level).
        /// </summary>
        IEnumerator WarmUp()
        {
            if (!GamePreloader.IsReady || cam == null) yield break;
            var root = new GameObject("WarmUp").transform;
            var c = cam.transform.position;
            // Behind the background sprite (z = 0): drawn first, then covered by the splash art.
            root.position = new Vector3(c.x, c.y, 40f);
            int i = 0;
            foreach (var prefab in GamePreloader.AllPrefabs())
            {
                var go = Instantiate(prefab, root);
                VisualUtil.StripRuntimeComponents(go);
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                go.transform.localPosition = new Vector3((i % 5 - 2) * 1.5f, (i / 5 % 6 - 3) * 1.5f, 0f);
                i++;
                if (i % 8 == 0) yield return null;   // spread the instantiation over a few frames
            }
            foreach (var fx in GamePreloader.AllEffects())
            {
                var go = Instantiate(fx, root);
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Clear(true);
                    ps.Emit(1);   // one particle so the effect's materials are drawn (hidden behind the art)
                }
            }
            Shader.WarmupAllShaders();
            yield return null;
            yield return null;
            Destroy(root.gameObject);
        }

        /// <summary>
        /// The background is cover-fitted, so on wider screens (iPad 3:4) its top is cropped and the logo with it.
        /// Keep the logo where it sits on the art, but push it down (and shrink it if needed) so it stays on screen.
        /// Returns the extra scale for the logo.
        /// </summary>
        float FitLogo()
        {
            if (logo == null || cam == null) return 1f;
            var sr = logo.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return 1f;
            if (!_logoLocalSet) { _logoLocal = logo.localPosition; _logoLocalSet = true; }
            var parent = logo.parent;
            float parentScale = parent != null ? Mathf.Abs(parent.lossyScale.y) : 1f;
            Vector3 full = Vector3.Scale(sr.sprite.bounds.size, _logoScale == Vector3.zero ? logo.localScale : _logoScale) * parentScale;
            float camH = cam.orthographicSize * 2f, camW = camH * cam.aspect;
            float fit = full.x > 1e-4f ? Mathf.Min(1f, camW * logoMaxWidth / full.x) : 1f;
            float halfH = full.y * fit * 0.5f;
            logo.localPosition = _logoLocal;
            float top = cam.transform.position.y + cam.orthographicSize - logoTopMargin;
            float y = logo.position.y;
            if (y + halfH > top) logo.position = new Vector3(logo.position.x, top - halfH, logo.position.z);
            return fit;
        }

        /// <summary>Scale the background so it always covers the camera view (like the original game scaler).</summary>
        void FitBackground()
        {
            if (cam == null || background == null || background.sprite == null) return;
            float h = cam.orthographicSize * 2f, w = h * cam.aspect;
            var size = background.sprite.bounds.size;
            float k = Mathf.Max(w / size.x, h / size.y);
            background.transform.localScale = new Vector3(k, k, 1f);
            var p = cam.transform.position;
            background.transform.position = new Vector3(p.x, p.y, background.transform.position.z);
        }
    }
}
