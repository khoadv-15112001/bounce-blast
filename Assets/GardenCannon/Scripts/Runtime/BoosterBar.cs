using System;
using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    public enum BoosterId { X2, Arrow, Bowling, Bomb }

    /// <summary>How a booster is used once its button is tapped.</summary>
    public enum BoosterUse
    {
        /// <summary>Takes effect at once (none of the current boosters).</summary>
        Instant,
        /// <summary>Armed for the next shot (x2 fireballs).</summary>
        NextShot,
        /// <summary>Waits for a tap on the board (arrow, bowling, bomb).</summary>
        Target,
    }

    /// <summary>
    /// Booster buttons on both sides of the cannon (the original game's booster icons). One booster can be selected at a
    /// time; tapping it again cancels. Each level starts with <see cref="Def.perLevel"/> uses of each booster.
    /// The effects themselves live in <see cref="GameController"/>.
    /// </summary>
    public class BoosterBar : MonoBehaviour
    {
        [Serializable]
        public class Def
        {
            public BoosterId id;
            public Sprite icon;
            [Tooltip("Text drawn on the icon (e.g. +5 for extra moves).")]
            public string label = "";
            public int perLevel = 2;
            [Tooltip("Position relative to the cannon pivot (world units).")]
            public Vector3 offset;
        }

        public static BoosterUse UseOf(BoosterId id) => id switch
        {
            BoosterId.X2 => BoosterUse.NextShot,
            _ => BoosterUse.Target,
        };

        [Header("Boosters (filled with the defaults when empty)")]
        public List<Def> boosters = new List<Def>();
        public Font font;
        public Transform cannon;
        public float buttonSize = 1.25f;
        [Tooltip("Button plate art (round purple, red rim). Empty = the generated blue plate.")]
        public Sprite plateSprite;
        [Tooltip("Count bubble art (red dot). Empty = the generated bubble.")]
        public Sprite bubbleSprite;
        [Tooltip("Scale of the bubble art (x and y).")]
        public float bubbleScale = 0.22f;
        [Tooltip("Local x of the bubble (and its number) on the button.")]
        public float bubbleX = 0.486f;
        public int sortingOrder = 45;

        /// <summary>The selected booster (armed for the next shot or waiting for a board tap), or null.</summary>
        public BoosterId? Selected { get; private set; }
        public BoosterUse? SelectedUse => Selected.HasValue ? UseOf(Selected.Value) : (BoosterUse?)null;

        /// <summary>Selection changed (null = nothing selected).</summary>
        public Action<BoosterId?> SelectionChanged;
        /// <summary>An instant booster was used.</summary>
        public Action<BoosterId> InstantUsed;

        class Button
        {
            public Def def;
            public int left;
            public Transform root;
            public SpriteRenderer plate, icon, glow, bubble;
            public TextMesh count, countShadow;
            public float punch;
            /// <summary>Canvas button (HUD canvas mode).</summary>
            public UiBoosterButton ui;
            public float baseX;
        }

        [Header("HUD canvas")]
        [Tooltip("HUD canvas holding the booster buttons (set by the game). Empty = world buttons next to the cannon.")]
        [NonSerialized] public HudCanvas hud;
        /// <summary>Screen point -> ground point (projectiles start under the canvas button).</summary>
        public Func<Vector2, Vector2> screenToGround;
        [Tooltip("Canvas mode: gap kept between a button and the side of the screen (canvas units).")]
        public float uiEdgeMargin = 37f;

        bool UiMode => hud != null && hud.boosters != null;

        readonly List<Button> _buttons = new List<Button>();
        float _pulse;

        /// <summary>Defaults: two buttons on each side of the cannon.</summary>
        public static List<Def> DefaultDefs() => new List<Def>
        {
            new Def { id = BoosterId.X2,      offset = new Vector3(-5.41f, 0f, -0.4f) },
            new Def { id = BoosterId.Arrow,   offset = new Vector3(-3.55f, 0f, -0.4f) },
            new Def { id = BoosterId.Bowling, offset = new Vector3(3.55f, 0f, -0.4f) },
            new Def { id = BoosterId.Bomb,    offset = new Vector3(5.41f, 0f, -0.4f) },
        };

        public Def Get(BoosterId id) => boosters.Find(d => d.id == id);
        public Sprite IconOf(BoosterId id) => Get(id)?.icon;
        public Vector3 ButtonPos(BoosterId id)
        {
            var b = _buttons.Find(x => x.def.id == id);
            if (b == null) return transform.position;
            if (b.ui != null)
            {
                var cam = hud.canvas != null ? hud.canvas.worldCamera : null;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, b.ui.transform.position);
                Vector2 g = screenToGround != null ? screenToGround(screen) : (Vector2)(cannon != null ? cannon.position : Vector3.zero);
                return new Vector3(g.x, g.y, -0.4f);
            }
            return b.root.position;
        }

        public void Build()
        {
            if (_buttons.Count > 0) return;
            if (boosters.Count == 0) boosters = DefaultDefs();
            if (UiMode)
            {
                // Canvas buttons from the HUD prefab (a booster without a button there is simply not offered).
                var uis = hud.boosters.GetComponentsInChildren<UiBoosterButton>(true);
                foreach (var d in boosters)
                {
                    UiBoosterButton ui = null;
                    foreach (var u in uis) if (u.booster == d.id) { ui = u; break; }
                    if (ui == null) continue;
                    var b = new Button { def = d, root = ui.transform, ui = ui, baseX = ((RectTransform)ui.transform).anchoredPosition.x };
                    if (ui.icon != null && d.icon != null) ui.icon.sprite = d.icon;
                    if (ui.label != null) ui.label.text = d.label ?? "";
                    if (ui.glow != null) ui.glow.enabled = false;
                    if (ui.button != null) { var bb = b; ui.button.onClick = () => Tap(bb); }
                    _buttons.Add(b);
                }
                foreach (var u in uis) if (!_buttons.Exists(x => x.ui == u)) u.gameObject.SetActive(false);
            }
            else foreach (var d in boosters) _buttons.Add(MakeButton(d));
            Place();
            Refresh();
        }

        public void ResetForLevel()
        {
            foreach (var b in _buttons) b.left = Mathf.Max(0, b.def.perLevel);
            Select(null);
            Refresh();
        }

        /// <summary>Tap at a world point on the ground plane. Returns true when a booster button was hit.</summary>
        public bool HandleTap(Vector2 world)
        {
            foreach (var b in _buttons)
            {
                if (b.ui != null) continue;   // canvas buttons take their own taps
                Vector2 c = b.root.position;
                float r = buttonSize * 0.6f;
                if ((world - c).sqrMagnitude > r * r) continue;
                Tap(b);
                return true;
            }
            return false;
        }

        void Tap(Button b)
        {
            b.punch = 1f;
            if (Selected == b.def.id) { Select(null); return; }
            if (b.left <= 0) return;
            if (UseOf(b.def.id) == BoosterUse.Instant)
            {
                b.left--;
                Select(null);
                Refresh();
                InstantUsed?.Invoke(b.def.id);
                return;
            }
            Select(b.def.id);
        }

        /// <summary>The selected booster is used (shot fired / target picked). Returns its id.</summary>
        public BoosterId? ConsumeSelected()
        {
            if (!Selected.HasValue) return null;
            var b = _buttons.Find(x => x.def.id == Selected.Value);
            if (b == null || b.left <= 0) { Select(null); return null; }
            var id = Selected.Value;
            b.left--;
            Select(null);
            Refresh();
            return id;
        }

        public void Cancel() => Select(null);

        void Select(BoosterId? id)
        {
            if (Selected == id) return;
            Selected = id;
            Refresh();
            SelectionChanged?.Invoke(id);
        }

        [Tooltip("Gap kept between a button and the side of the screen (world units). Narrow screens (1440x3120 and "
                 + "taller) pull the outer buttons in.")]
        public float edgeMargin = 0.3f;

        /// <summary>Canvas mode: the booster group follows the cannon on screen; outer buttons are kept on screen.</summary>
        void PlaceUi()
        {
            var canvasRect = hud.Rect;
            var cam = Camera.main;
            if (cam == null || canvasRect == null) return;
            Vector3 pivot = cannon != null ? cannon.position : new Vector3(0f, -10.54f, 0f);
            Vector2 screen = cam.WorldToScreenPoint(pivot);
            var parent = hud.boosters.parent as RectTransform;
            if (parent == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, hud.canvas != null ? hud.canvas.worldCamera : null, out var local)) return;
            var rt = hud.boosters;
            // Keep the group's own anchors: convert the local point into an anchored position.
            Vector2 anchorRef = Vector2.Scale(parent.rect.size, (rt.anchorMin + rt.anchorMax) * 0.5f - parent.pivot);
            rt.anchoredPosition = new Vector2(local.x - anchorRef.x, local.y - anchorRef.y);
            float halfW = parent.rect.width * 0.5f;
            foreach (var b in _buttons)
            {
                if (b.ui == null) continue;
                var brt = (RectTransform)b.ui.transform;
                float half = brt.rect.width * 0.5f;
                float x = b.baseX;
                float centreX = local.x + x;
                float max = halfW - uiEdgeMargin - half;
                if (Mathf.Abs(centreX) > max) x = Mathf.Sign(centreX) * max - local.x;
                brt.anchoredPosition = new Vector2(x, brt.anchoredPosition.y);
            }
        }

        void Place()
        {
            if (UiMode) { PlaceUi(); return; }
            Vector3 pivot = cannon != null ? cannon.position : new Vector3(0f, -10.54f, 0f);
            var cam = Camera.main;
            float camX = cam != null ? cam.transform.position.x : 0f;
            float maxX = cam != null ? cam.orthographicSize * cam.aspect - edgeMargin - buttonSize * 0.5f : float.MaxValue;
            foreach (var b in _buttons)
            {
                var p = pivot + b.def.offset;
                float dx = p.x - camX;
                if (Mathf.Abs(dx) > maxX) p.x = camX + Mathf.Sign(dx) * maxX;
                b.root.position = p;
            }
        }

        void Refresh()
        {
            foreach (var b in _buttons)
            {
                string s = b.left.ToString();
                var c = b.left <= 0 ? new Color(0.55f, 0.55f, 0.6f, 1f) : Color.white;
                if (b.ui != null)
                {
                    if (b.ui.count != null) b.ui.count.text = s;
                    if (b.ui.plate != null) b.ui.plate.color = c;
                    if (b.ui.icon != null) b.ui.icon.color = c;
                    continue;
                }
                if (b.count != null) b.count.text = s;
                if (b.countShadow != null) b.countShadow.text = s;
                b.plate.color = c;
                b.icon.color = c;
            }
        }

        void Update()
        {
            if (_buttons.Count == 0) return;
            Place();
            _pulse += Time.deltaTime;
            foreach (var b in _buttons)
            {
                bool active = Selected == b.def.id;
                if (b.ui != null)
                {
                    // Canvas: the UiButton does the press scale; the selection pulses the glow and the icon.
                    if (b.ui.glow != null)
                    {
                        b.ui.glow.enabled = active;
                        if (active) b.ui.glow.color = new Color(1f, 0.85f, 0.3f, 0.55f + 0.35f * Mathf.Sin(_pulse * 9f));
                    }
                    if (b.ui.icon != null) b.ui.icon.transform.localScale = Vector3.one * (active ? 1f + 0.08f * Mathf.Sin(_pulse * 9f) : 1f);
                    continue;
                }
                float k = active ? 1f + 0.08f * Mathf.Sin(_pulse * 9f) : 1f;
                b.punch = Mathf.Max(0f, b.punch - Time.deltaTime * 5f);
                b.root.localScale = Vector3.one * (k + Mathf.Sin(b.punch * Mathf.PI) * 0.15f);
                b.glow.enabled = active;
                if (active) b.glow.color = new Color(1f, 0.85f, 0.3f, 0.55f + 0.35f * Mathf.Sin(_pulse * 9f));
            }
        }

        // ------------------------------------------------------------------ building

        Button MakeButton(Def d)
        {
            var b = new Button { def = d, root = new GameObject("Booster" + d.id).transform };
            b.root.SetParent(transform, false);
            b.glow = VisualUtil.MakeSprite("Glow", b.root, CircleSprite(true), Vector3.zero, buttonSize * 1.35f, sortingOrder - 1);
            b.glow.enabled = false;
            b.plate = VisualUtil.MakeSprite("Plate", b.root, plateSprite != null ? plateSprite : CircleSprite(false), Vector3.zero, buttonSize, sortingOrder);
            b.icon = VisualUtil.MakeSprite("Icon", b.root, d.icon, Vector3.zero, buttonSize * 0.78f, sortingOrder + 1);
            if (!string.IsNullOrEmpty(d.label))
            {
                var o = VisualUtil.MakeText("LabelShadow", b.root, font, new Vector3(0.02f, -0.03f, -0.01f), buttonSize * 0.34f, new Color(0.2f, 0.05f, 0.3f, 0.85f), TextAnchor.MiddleCenter, sortingOrder + 2);
                o.text = d.label;
                var t = VisualUtil.MakeText("Label", b.root, font, new Vector3(0f, 0f, -0.02f), buttonSize * 0.34f, Color.white, TextAnchor.MiddleCenter, sortingOrder + 3);
                t.text = d.label;
            }
            var bubblePos = new Vector3(bubbleSprite != null ? bubbleX : buttonSize * 0.36f, -buttonSize * 0.34f, -0.03f);
            b.bubble = VisualUtil.MakeSprite("Bubble", b.root, bubbleSprite != null ? bubbleSprite : BubbleSprite(), bubblePos, buttonSize * 0.44f, sortingOrder + 4);
            if (bubbleSprite != null) b.bubble.transform.localScale = Vector3.one * bubbleScale;
            b.countShadow = VisualUtil.MakeText("CountShadow", b.root, font, bubblePos + new Vector3(0.02f, -0.03f, -0.01f), buttonSize * 0.26f, new Color(0.25f, 0.05f, 0.05f, 0.8f), TextAnchor.MiddleCenter, sortingOrder + 5);
            b.count = VisualUtil.MakeText("Count", b.root, font, bubblePos + new Vector3(0f, 0f, -0.02f), buttonSize * 0.26f, Color.white, TextAnchor.MiddleCenter, sortingOrder + 6);
            return b;
        }

        static Sprite _plate, _glow, _bubble;

        /// <summary>Round button plate (gold rim, blue fill lighter at the top) or a soft glow ring.</summary>
        static Sprite CircleSprite(bool glow)
        {
            if (glow && _glow != null) return _glow;
            if (!glow && _plate != null) return _plate;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = glow ? "BoosterGlow" : "BoosterPlate" };
            var px = new Color32[size * size];
            var rim = new Color(1f, 0.78f, 0.22f);
            var fillTop = new Color(0.33f, 0.62f, 1f);
            var fillBottom = new Color(0.1f, 0.3f, 0.82f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c;
                    if (glow)
                    {
                        float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.78f) / 0.22f);
                        c = new Color(1f, 1f, 1f, a * a);
                    }
                    else
                    {
                        float edge = Mathf.Clamp01((1f - r) * size * 0.5f);
                        if (r > 0.86f) c = Color.Lerp(rim, rim * 0.7f, (r - 0.86f) / 0.14f);
                        else c = Color.Lerp(fillBottom, fillTop, (dy + 1f) * 0.5f);
                        if (r > 0.82f && r <= 0.86f) c = Color.Lerp(c, Color.black, 0.25f);
                        c.a = edge;
                    }
                    px[y * size + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            if (glow) _glow = s; else _plate = s;
            return s;
        }

        static Sprite BubbleSprite()
        {
            if (_bubble != null) return _bubble;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BoosterBubble" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = r > 0.8f ? Color.white : Color.Lerp(new Color(0.75f, 0.08f, 0.1f), new Color(0.98f, 0.3f, 0.25f), (dy + 1f) * 0.5f);
                    c.a = Mathf.Clamp01((1f - r) * size * 0.5f);
                    px[y * size + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _bubble = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _bubble;
        }
    }
}
