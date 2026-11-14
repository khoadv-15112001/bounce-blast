using System;
using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// HUD built from SpriteRenderers + TextMesh (the project's uGUI/TMP assemblies are stubs).
    /// Level plate, goals, restart button, tutorial banner and popups live on the UI layer (UI camera);
    /// the move counter sits on the GrassBottom art in the world, like the original MoveCountText.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        [Header("Cameras")]
        public Camera uiCamera;

        [Header("Anchors")]
        public Transform levelAnchor;      // UI
        public Transform goalsAnchor;      // UI
        public Transform settingsAnchor;   // UI
        public Transform bannerAnchor;     // UI
        public Transform movesAnchor;      // world (on GrassBottom)

        [Header("Art (set at runtime from GameAssets)")]
        public Font font;
        public Sprite levelPlate;
        public Sprite settingsButton;
        public Sprite panelSprite;
        public Sprite whiteSprite;

        [Tooltip("The original game shows no goal row (clear everything). Turn on to show remaining goals.")]
        public bool showGoals;

        [Header("Colours")]
        public Color levelTextColor = new Color(0.42f, 0.14f, 0.13f);
        public Color panelColor = new Color(1f, 0.965f, 0.9f);
        public Color panelBorderColor = new Color(0.6f, 0.12f, 0.15f);
        public Color mainButtonColor = new Color(0.97f, 0.77f, 0.22f);
        public Color altButtonColor = new Color(0.5f, 0.75f, 0.23f);

        class Button
        {
            public Transform Target;
            public Vector2 Size;
            public Action OnClick;
            public bool Popup;
        }

        readonly List<Button> _buttons = new List<Button>();
        TextMesh _levelText, _movesText, _movesShadow;
        readonly List<TextMesh> _movesOutline = new List<TextMesh>();
        [Tooltip("Outline around the moves number (world units, before the anchor scale).")]
        public float movesOutline = 0.085f;
        public Color movesOutlineColor = new Color(0.36f, 0.2f, 0.07f);
        [Tooltip("Font size of the moves number (TextMesh, the other texts use 64).")]
        public int movesFontSize = 55;
        [Header("Balls badge (top-left)")]
        [Tooltip("Show the moves counter as a 'Balls' badge in the top-left corner (mirrors the settings button).")]
        public bool movesBadgeTopLeft = true;
        public Vector2 movesBadgeSize = new Vector2(2.0f, 1.85f);
        public string movesBadgeTitle = "Shots";
        public Color movesBadgeFill = new Color(0.12f, 0.38f, 0.9f);
        public Color movesBadgeBorder = new Color(1f, 0.78f, 0.22f);
        Transform _goalsRoot, _popupRoot, _bannerRoot;
        readonly Dictionary<int, (TextMesh text, Transform icon)> _goals = new Dictionary<int, (TextMesh, Transform)>();
        readonly Dictionary<Transform, float> _punch = new Dictionary<Transform, float>();
        readonly Dictionary<Transform, Vector3> _baseScale = new Dictionary<Transform, Vector3>();
        readonly List<Transform> _punchDone = new List<Transform>();
        readonly List<Transform> _punchKeys = new List<Transform>();
        int _uiLayer;

        public bool PopupOpen => _popupRoot != null;

        /// <summary>Raised for every HUD button press (click sound).</summary>
        public Action ButtonClicked;

        public void Init(Action restart, Action prev, Action next)
        {
            _uiLayer = gameObject.layer;
            _buttons.Clear();

            if (levelAnchor != null)
            {
                if (levelPlate != null) VisualUtil.MakeSprite("LevelPlate", levelAnchor, levelPlate, Vector3.zero, 4.15f, 60);
                _levelText = VisualUtil.MakeText("LevelText", levelAnchor, font, new Vector3(0f, 0f, -0.1f), 0.8f, levelTextColor, TextAnchor.MiddleCenter, 61);
                var l = VisualUtil.MakeText("Prev", levelAnchor, font, new Vector3(-2.75f, 0.05f, -0.1f), 0.9f, Color.white, TextAnchor.MiddleCenter, 61);
                l.text = "<";
                var r = VisualUtil.MakeText("Next", levelAnchor, font, new Vector3(2.75f, 0.05f, -0.1f), 0.9f, Color.white, TextAnchor.MiddleCenter, 61);
                r.text = ">";
                AddButton(l.transform, new Vector2(1.1f, 1.3f), prev);
                AddButton(r.transform, new Vector2(1.1f, 1.3f), next);
                Relayer(levelAnchor);
            }
            if (settingsAnchor != null)
            {
                if (settingsButton != null) VisualUtil.MakeSprite("RestartButton", settingsAnchor, settingsButton, Vector3.zero, 1.5f, 60);
                else if (settingsAnchor.GetComponentInChildren<SpriteRenderer>(true) == null)
                {
                    var t = VisualUtil.MakeText("Restart", settingsAnchor, font, Vector3.zero, 0.8f, Color.white);
                    t.text = "R";
                }
                AddButton(settingsAnchor, new Vector2(1.6f, 1.6f), restart);
                Relayer(settingsAnchor);
            }
            if (movesBadgeTopLeft && settingsAnchor != null) movesAnchor = BuildMovesBadge();
            if (movesAnchor != null)
            {
                // Drop shadow, then a dark outline all around (12 copies), then the white number on top.
                _movesShadow = VisualUtil.MakeText("MovesShadow", movesAnchor, font, new Vector3(0.06f, -0.11f, 0.06f), 1.45f, new Color(0.2f, 0.11f, 0.03f, 0.85f), TextAnchor.MiddleCenter, 60);
                _movesOutline.Clear();
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12f;
                    var off = new Vector3(Mathf.Cos(a) * movesOutline, Mathf.Sin(a) * movesOutline, 0.03f);
                    _movesOutline.Add(VisualUtil.MakeText("MovesOutline", movesAnchor, font, off, 1.45f, movesOutlineColor, TextAnchor.MiddleCenter, 61));
                }
                _movesText = VisualUtil.MakeText("Moves", movesAnchor, font, Vector3.zero, 1.45f, Color.white, TextAnchor.MiddleCenter, 62);
                // Same character size, smaller font size: the number shrinks to 55/64 (outline and shadow too).
                _movesText.fontSize = movesFontSize;
                if (_movesShadow != null) _movesShadow.fontSize = movesFontSize;
                foreach (var o in _movesOutline) if (o != null) o.fontSize = movesFontSize;
                // The badge lives on the UI layer (UI camera): the number texts are created after it was relayered.
                if (movesBadgeTopLeft) Relayer(movesAnchor);
            }
        }

        /// <summary>
        /// 'Balls' badge in the top-left corner, mirroring the settings button (same safe-area anchor, x negated):
        /// rounded blue panel with a gold rim, the title on top and the moves number below. Returns the number anchor.
        /// </summary>
        Transform BuildMovesBadge()
        {
            var badge = new GameObject("MovesBadge").transform;
            badge.SetParent(settingsAnchor.parent, false);
            var src = settingsAnchor.GetComponent<UiAnchor>();
            var p = settingsAnchor.localPosition;
            badge.localPosition = new Vector3(-p.x, p.y, p.z);
            if (src != null)
            {
                var a = badge.gameObject.AddComponent<UiAnchor>();
                a.uiCamera = src.uiCamera != null ? src.uiCamera : uiCamera;
                a.edge = src.edge;
                a.offset = src.offset;
                a.x = -src.x;
                a.respectSafeArea = src.respectSafeArea;
                a.Apply();
            }
            var panel = new GameObject("Panel").AddComponent<SpriteRenderer>();
            panel.transform.SetParent(badge, false);
            panel.sprite = BadgeSprite();
            panel.drawMode = SpriteDrawMode.Sliced;
            panel.size = movesBadgeSize;
            panel.sortingOrder = 58;
            var title = VisualUtil.MakeText("Title", badge, font, new Vector3(0f, movesBadgeSize.y * 0.29f, -0.1f), 0.42f, Color.white, TextAnchor.MiddleCenter, 61);
            title.text = movesBadgeTitle;
            var number = new GameObject("Number").transform;
            number.SetParent(badge, false);
            number.localPosition = new Vector3(0f, -movesBadgeSize.y * 0.12f, -0.1f);
            number.localScale = Vector3.one * 0.72f;
            Relayer(badge);
            return number;
        }

        static Sprite _badgeSprite;

        /// <summary>Rounded panel: gold rim, blue fill with a lighter top (9-sliced, generated once, tinted white).</summary>
        Sprite BadgeSprite()
        {
            if (_badgeSprite != null) return _badgeSprite;
            const int size = 64, border = 22;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "BallsBadge" };
            var px = new Color32[size * size];
            const float radius = 16f, rim = 4.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                    float qy = Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                    float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float a = Mathf.Clamp01(0.5f - d);
                    // Rim: gold, a little darker towards the outside; fill: blue, lighter in the upper part.
                    Color c;
                    if (d > -rim) c = Color.Lerp(movesBadgeBorder, movesBadgeBorder * 0.75f, Mathf.Clamp01((d + rim) / rim));
                    else
                    {
                        float up = y / (float)size;
                        c = Color.Lerp(movesBadgeFill * 0.85f, Color.Lerp(movesBadgeFill, Color.white, 0.18f), up);
                        if (d > -rim - 1.5f) c = Color.Lerp(c, Color.black, 0.25f);   // thin inner shade under the rim
                    }
                    c.a = a;
                    px[y * size + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _badgeSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 1.6f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            _badgeSprite.name = "BallsBadge";
            return _badgeSprite;
        }

        void AddButton(Transform t, Vector2 size, Action a, bool popup = false) =>
            _buttons.Add(new Button { Target = t, Size = size, OnClick = a, Popup = popup });

        void Relayer(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = _uiLayer;
        }

        void Update()
        {
            if (_punch.Count == 0) return;
            var done = _punchDone;
            var keys = _punchKeys;
            done.Clear();
            keys.Clear();
            foreach (var k in _punch.Keys) keys.Add(k);
            foreach (var t in keys)
            {
                if (t == null) { done.Add(t); continue; }
                float v = Mathf.Max(0f, _punch[t] - Time.deltaTime * 4f);
                _punch[t] = v;
                t.localScale = _baseScale[t] * (1f + 0.35f * Mathf.Sin(v * Mathf.PI));
                if (v <= 0f) done.Add(t);
            }
            foreach (var t in done) { _punch.Remove(t); }
        }

        void Punch(Transform t)
        {
            if (t == null) return;
            if (!_baseScale.ContainsKey(t)) _baseScale[t] = t.localScale;
            _punch[t] = 1f;
        }

        public void SetLevel(int number) { if (_levelText != null) _levelText.text = "Level " + number; }

        public void SetMoves(int moves, bool punch = false)
        {
            if (_movesText != null) _movesText.text = moves.ToString();
            if (_movesShadow != null) _movesShadow.text = moves.ToString();
            foreach (var o in _movesOutline) if (o != null) o.text = moves.ToString();
            if (_movesText != null) _movesText.color = moves <= 5 ? new Color(1f, 0.55f, 0.5f) : Color.white;
            if (punch && movesAnchor != null) Punch(movesAnchor);
        }

        // ---------------- Goals ----------------

        public Func<int, int> VariantForType;

        /// <summary>Goal row: a small copy of each item prefab plus a counter.</summary>
        public void BuildGoals(IList<GoalData> goals, ItemCatalog catalog)
        {
            if (_goalsRoot != null) Destroy(_goalsRoot.gameObject);
            _goals.Clear();
            if (goalsAnchor == null || !showGoals) return;
            _goalsRoot = new GameObject("Goals").transform;
            _goalsRoot.SetParent(goalsAnchor, false);

            const float slot = 2.2f;
            float width = goals.Count * slot;
            if (whiteSprite != null)
            {
                var bg = VisualUtil.MakeSprite("GoalsBg", _goalsRoot, whiteSprite, new Vector3(0f, 0f, 1f), 0f, 55);
                bg.color = new Color(0.2f, 0.1f, 0.03f, 0.45f);
                var sz = whiteSprite.bounds.size;
                bg.transform.localScale = new Vector3((width + 0.4f) / sz.x, 1.3f / sz.y, 1f);
            }
            for (int i = 0; i < goals.Count; i++)
            {
                var g = goals[i];
                float x = -width * 0.5f + slot * (i + 0.5f);
                var e = catalog.Get(g.type);
                Transform iconT = null;
                if (e != null && e.prefab != null)
                {
                    var icon = Instantiate(e.prefab, _goalsRoot);
                    VisualUtil.StripRuntimeComponents(icon);
                    icon.transform.localPosition = new Vector3(x - 0.5f, 0f, 0f);
                    icon.transform.localScale = Vector3.Scale(icon.transform.localScale, Vector3.one * 0.85f);
                    VisualUtil.ApplyOrientation(icon.transform, false);
                    if (e.colorByVariant) VisualUtil.ApplyAtlasTile(icon.transform, catalog, catalog.AtlasTileForVariant(VariantForType != null ? VariantForType(g.type) : 0));
                    foreach (var anim in icon.GetComponentsInChildren<Animator>(true)) anim.enabled = false;
                    iconT = icon.transform;
                }
                var t = VisualUtil.MakeText("Goal_" + g.type, _goalsRoot, font, new Vector3(x + 0.45f, 0f, -1f), 0.65f, Color.white, TextAnchor.MiddleCenter, 62);
                _goals[g.type] = (t, iconT);
            }
            Relayer(_goalsRoot);
        }

        public void SetGoal(int type, int remaining, bool punch = false)
        {
            if (!_goals.TryGetValue(type, out var g)) return;
            g.text.text = remaining > 0 ? remaining.ToString() : "✓";
            if (punch) { Punch(g.text.transform); Punch(g.icon); }
        }

        /// <summary>World position of a goal icon on screen, projected onto the given camera's view (for fly-to effects).</summary>
        public bool TryGetGoalScreenPos(int type, out Vector3 screen)
        {
            screen = default;
            if (uiCamera == null || !_goals.TryGetValue(type, out var g) || g.text == null) return false;
            screen = uiCamera.WorldToScreenPoint(g.icon != null ? g.icon.position : g.text.transform.position);
            return true;
        }

        // ---------------- Tutorial banner ----------------

        public void ShowBanner(string text)
        {
            HideBanner();
            if (bannerAnchor == null) return;
            _bannerRoot = new GameObject("Banner").transform;
            _bannerRoot.SetParent(bannerAnchor, false);
            if (whiteSprite != null)
            {
                var sz = whiteSprite.bounds.size;
                var border = VisualUtil.MakeSprite("Border", _bannerRoot, whiteSprite, new Vector3(0, 0, 0.1f), 0f, 70);
                border.color = panelBorderColor;
                border.transform.localScale = new Vector3(7.4f / sz.x, 2.5f / sz.y, 1f);
                var fill = VisualUtil.MakeSprite("Fill", _bannerRoot, whiteSprite, Vector3.zero, 0f, 71);
                fill.color = panelColor;
                fill.transform.localScale = new Vector3(7.0f / sz.x, 2.1f / sz.y, 1f);
            }
            var t = VisualUtil.MakeText("Text", _bannerRoot, font, new Vector3(0, 0, -0.1f), 0.62f, panelBorderColor, TextAnchor.MiddleCenter, 72);
            t.text = text;
            Relayer(_bannerRoot);
        }

        public void HideBanner()
        {
            if (_bannerRoot != null) Destroy(_bannerRoot.gameObject);
            _bannerRoot = null;
        }

        // ---------------- Popup ----------------

        public void ShowPopup(string title, Color titleColor, string body, params (string label, Action action, bool main)[] buttons)
        {
            ClosePopup();
            _popupRoot = new GameObject("Popup").transform;
            _popupRoot.SetParent(transform, false);
            Vector3 c = uiCamera != null ? uiCamera.transform.position + uiCamera.transform.forward * 5f : new Vector3(0, 0, -5f);
            _popupRoot.position = c;

            if (whiteSprite != null)
            {
                var sz = whiteSprite.bounds.size;
                var dim = VisualUtil.MakeSprite("Dim", _popupRoot, whiteSprite, Vector3.zero, 0f, 900);
                dim.color = new Color(0f, 0f, 0f, 0.6f);
                dim.transform.localScale = new Vector3(80f / sz.x, 120f / sz.y, 1f);
                var border = VisualUtil.MakeSprite("Border", _popupRoot, whiteSprite, new Vector3(0, 0, -0.05f), 0f, 901);
                border.color = panelBorderColor;
                border.transform.localScale = new Vector3(10.4f / sz.x, 8.6f / sz.y, 1f);
                var panel = VisualUtil.MakeSprite("Panel", _popupRoot, whiteSprite, new Vector3(0, 0, -0.1f), 0f, 902);
                panel.color = panelColor;
                panel.transform.localScale = new Vector3(10f / sz.x, 8.2f / sz.y, 1f);
            }
            var tt = VisualUtil.MakeText("Title", _popupRoot, font, new Vector3(0f, 2.7f, -0.2f), 1.1f, titleColor, TextAnchor.MiddleCenter, 903);
            tt.text = title;
            var bt = VisualUtil.MakeText("Body", _popupRoot, font, new Vector3(0f, 0.8f, -0.2f), 0.55f, new Color(0.35f, 0.23f, 0.12f), TextAnchor.MiddleCenter, 903);
            bt.text = body;

            float bw = 4.2f, gap = 0.4f;
            float total = buttons.Length * bw + (buttons.Length - 1) * gap;
            for (int i = 0; i < buttons.Length; i++)
            {
                var (label, action, main) = buttons[i];
                Vector3 p = new Vector3(-total * 0.5f + bw * (i + 0.5f) + gap * i, -2.4f, -0.2f);
                if (whiteSprite != null)
                {
                    var b = VisualUtil.MakeSprite("Btn_" + label, _popupRoot, whiteSprite, p, 0f, 903);
                    b.color = main ? mainButtonColor : altButtonColor;
                    var sz = whiteSprite.bounds.size;
                    b.transform.localScale = new Vector3(bw / sz.x, 1.6f / sz.y, 1f);
                }
                var lt = VisualUtil.MakeText("Label", _popupRoot, font, p + new Vector3(0, 0, -0.1f), 0.7f, Color.white, TextAnchor.MiddleCenter, 904);
                lt.text = label;
                AddButton(lt.transform, new Vector2(bw, 1.6f), action, true);
            }
            Relayer(_popupRoot);
            _popupRoot.localScale = Vector3.one * 0.6f;
            Punch(_popupRoot);
            _baseScale[_popupRoot] = Vector3.one;
        }

        /// <summary>Vertical button list popup (settings).</summary>
        public void ShowMenu(string title, params (string label, Action action, bool main)[] items)
        {
            ClosePopup();
            _popupRoot = new GameObject("Menu").transform;
            _popupRoot.SetParent(transform, false);
            _popupRoot.position = uiCamera != null ? uiCamera.transform.position + uiCamera.transform.forward * 5f : new Vector3(0, 0, -5f);
            float bh = 1.45f, gap = 0.35f;
            float listH = items.Length * bh + (items.Length - 1) * gap;
            float panelH = listH + 3.2f;
            if (whiteSprite != null)
            {
                var sz = whiteSprite.bounds.size;
                var dim = VisualUtil.MakeSprite("Dim", _popupRoot, whiteSprite, Vector3.zero, 0f, 900);
                dim.color = new Color(0f, 0f, 0f, 0.6f);
                dim.transform.localScale = new Vector3(80f / sz.x, 120f / sz.y, 1f);
                var border = VisualUtil.MakeSprite("Border", _popupRoot, whiteSprite, new Vector3(0, 0, -0.05f), 0f, 901);
                border.color = panelBorderColor;
                border.transform.localScale = new Vector3(8.4f / sz.x, (panelH + 0.4f) / sz.y, 1f);
                var panel = VisualUtil.MakeSprite("Panel", _popupRoot, whiteSprite, new Vector3(0, 0, -0.1f), 0f, 902);
                panel.color = panelColor;
                panel.transform.localScale = new Vector3(8f / sz.x, panelH / sz.y, 1f);
            }
            var tt = VisualUtil.MakeText("Title", _popupRoot, font, new Vector3(0f, panelH * 0.5f - 1.1f, -0.2f), 1.0f, panelBorderColor, TextAnchor.MiddleCenter, 903);
            tt.text = title;
            float y = panelH * 0.5f - 2.4f - bh * 0.5f;
            foreach (var (label, action, main) in items)
            {
                var p = new Vector3(0f, y, -0.2f);
                if (whiteSprite != null)
                {
                    var b = VisualUtil.MakeSprite("Btn_" + label, _popupRoot, whiteSprite, p, 0f, 903);
                    b.color = main ? mainButtonColor : altButtonColor;
                    var sz = whiteSprite.bounds.size;
                    b.transform.localScale = new Vector3(6f / sz.x, bh / sz.y, 1f);
                }
                var lt = VisualUtil.MakeText("Label", _popupRoot, font, p + new Vector3(0, 0, -0.1f), 0.62f, Color.white, TextAnchor.MiddleCenter, 904);
                lt.text = label;
                AddButton(lt.transform, new Vector2(6f, bh), action, true);
                y -= bh + gap;
            }
            Relayer(_popupRoot);
        }

        public void ClosePopup()
        {
            if (_popupRoot != null) Destroy(_popupRoot.gameObject);
            _popupRoot = null;
            _buttons.RemoveAll(b => b.Popup);
        }

        /// <summary>Screen click -> HUD button. Returns true when the click was used by the HUD.</summary>
        public bool HandleClick(Vector3 screenPos)
        {
            if (uiCamera == null) return false;
            Vector2 world = uiCamera.ScreenToWorldPoint(screenPos);
            bool popup = PopupOpen;
            for (int i = _buttons.Count - 1; i >= 0; i--)
            {
                var b = _buttons[i];
                if (popup && !b.Popup) continue;
                if (b.Target == null) continue;
                Vector2 c = b.Target.position;
                Vector2 half = b.Size * 0.5f * (b.Popup && _popupRoot != null ? _popupRoot.lossyScale.x : 1f);
                if (Mathf.Abs(world.x - c.x) <= half.x && Mathf.Abs(world.y - c.y) <= half.y)
                {
                    ButtonClicked?.Invoke();
                    Punch(b.Target);
                    b.OnClick?.Invoke();
                    return true;
                }
            }
            return popup;
        }
    }
}
