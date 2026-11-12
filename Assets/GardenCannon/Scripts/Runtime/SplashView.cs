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
    public class SplashView : MonoBehaviour
    {
        public Camera cam;
        [Tooltip("Background sprite renderer, scaled to cover the whole screen.")]
        public SpriteRenderer background;
        public Transform logo;
        public Transform loadingAnchor;
        public AssetReferenceT<Font> font;
        public string loadingText = "Loading";
        public Color loadingColor = Color.white;
        public string gameScene = "GardenCannon";
        [Tooltip("Minimum time the splash stays on screen (seconds).")]
        public float minDuration = 1.5f;

        TextMesh _text, _shadow;
        Vector3 _logoScale;
        float _t;

        IEnumerator Start()
        {
            if (cam == null) cam = Camera.main;
            if (logo != null) { _logoScale = logo.localScale; logo.localScale = Vector3.zero; }

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

            // Wait for the splash sprites (background / logo) to come out of Addressables.
            foreach (var s in FindObjectsOfType<AddressableSprite>(true))
                while (s != null && !s.IsDone) yield return null;

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
            if (logo != null && _logoScale != Vector3.zero)
            {
                float k = Mathf.Clamp01(_t / 0.45f);
                float back = 1f + 2.70158f * Mathf.Pow(k - 1f, 3f) + 1.70158f * Mathf.Pow(k - 1f, 2f); // ease out back
                logo.localScale = _logoScale * back * (1f + 0.02f * Mathf.Sin(_t * 3f));
            }
            if (_text != null)
            {
                string s = loadingText + new string('.', 1 + (int)(_t * 3f) % 3);
                _text.text = _shadow.text = s;
            }
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
