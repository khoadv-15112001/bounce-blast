using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GardenCannon
{
    /// <summary>
    /// Default layout of the HUD canvas and of every canvas popup (canvas units: 1440 x 3120 reference, ~122 per world
    /// unit). The editor saves these as prefabs once (then they are edited by hand); the game only builds them itself
    /// when a prefab is missing.
    /// </summary>
    public static class UiLayout
    {
        public struct Kit
        {
            public UiTheme theme;
            public PopupSkin skin;
            public Sprite white;
            public Sprite badge, boosterPlate, boosterBubble;
            public Func<BoosterId, Sprite> boosterIcon;
            public Color bodyColor;
        }

        public const string Hud = "HudCanvas";
        public const string Win = "UiPopupWin", Lose = "UiPopupLose", Settings = "UiPopupSettings", Cheat = "UiPopupCheat",
            Password = "UiPopupPassword", LevelInput = "UiPopupLevelInput", Haptics = "UiPopupHaptics";

        /// <summary>Every popup prefab (loaded through Addressables at <see cref="Address"/>).</summary>
        public static readonly string[] Popups = { Win, Lose, Settings, Cheat, Password, LevelInput, Haptics };

        /// <summary>Addressables key of a popup prefab.</summary>
        public static string Address(string popup) => "GardenCannon/UI/" + popup;

        static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);

        // ---------------- pieces ----------------

        public static RectTransform RT(string name, Transform parent, Vector2 pos, Vector2 size, Vector2? anchor = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor ?? Centre;
            rt.pivot = Centre;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Image Img(string name, Transform parent, Sprite s, Vector2 pos, Vector2 size, bool sliced = true, float ppuMul = 1f)
        {
            var img = RT(name, parent, pos, size).gameObject.AddComponent<Image>();
            img.sprite = s;
            if (s != null && sliced && s.border != Vector4.zero)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = ppuMul;
            }
            else if (s != null) img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI Txt(Kit k, string name, Transform parent, string text, Vector2 pos, Vector2 size, float fontSize, Material mat,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var t = RT(name, parent, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
            if (k.theme != null && k.theme.font != null) t.font = k.theme.font;
            if (mat != null) t.fontSharedMaterial = mat;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        static Material TextMat(Kit k, Sprite art)
        {
            var th = k.theme;
            if (th == null) return null;
            var s = k.skin;
            if (s != null)
            {
                if (art == s.green && th.greenText != null) return th.greenText;
                if (art == s.yellow && th.yellowText != null) return th.yellowText;
                if (art == s.blue && th.blueText != null) return th.blueText;
                if (art == s.red && th.redText != null) return th.redText;
            }
            return th.titleMaterial;
        }

        public static UiButton Button(Kit k, Transform parent, string id, string label, Vector2 pos, Vector2 size, Sprite art, float fontSize = 72f)
        {
            var rt = RT("Btn_" + id, parent, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = art;
            if (art != null && art.border != Vector4.zero) img.type = Image.Type.Sliced;
            else if (art == null) img.color = new Color(1f, 1f, 1f, 0f);   // invisible, still takes taps
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<UiButton>();
            b.id = id;
            b.art = img;
            if (!string.IsNullOrEmpty(label) || label == "")
                b.label = Txt(k, "Label", rt, label, new Vector2(0f, 6f), size, fontSize, TextMat(k, art), Color.white);
            return b;
        }

        static UiButton Close(Kit k, Transform parent, Vector2 pos, string id = "close")
        {
            var rt = RT("Btn_" + id, parent, pos, new Vector2(152f, 152f));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = k.skin != null ? k.skin.close : null;
            img.preserveAspect = true;
            if (img.sprite == null) img.color = new Color(0.8f, 0.1f, 0.1f);
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<UiButton>();
            b.id = id;
            b.art = img;
            return b;
        }

        static UiPopup Root(Kit k, string name, float dimAlpha = 0.65f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            Stretch(rt);
            var p = go.AddComponent<UiPopup>();
            var dim = Img("Dim", rt, k.white, Vector2.zero, Vector2.zero, false);
            dim.preserveAspect = false;
            Stretch(dim.rectTransform);
            dim.color = new Color(0f, 0f, 0f, dimAlpha);
            dim.raycastTarget = true;
            p.dim = dim;
            p.panel = RT("Panel", rt, Vector2.zero, Vector2.zero);
            return p;
        }

        /// <summary>Body + title ribbon (+ title text, Spine anchor) + optional close button.</summary>
        static void Panel(Kit k, UiPopup p, string title, float w, float h, bool close, string closeId = "close")
        {
            var root = p.panel;
            var bodySprite = k.skin != null ? k.skin.body : null;
            p.body = Img("Body", root, bodySprite != null ? bodySprite : k.white, Vector2.zero, new Vector2(w, h), true, 0.82f);
            if (bodySprite == null) { p.body.color = new Color(1f, 0.965f, 0.9f); p.body.preserveAspect = false; }
            float hw = Mathf.Min(w * 1.02f, 1195f);
            var hc = new Vector2(0f, h * 0.5f + 18f);
            if (k.skin != null && k.skin.header != null)
            {
                var hs = k.skin.header.rect;
                Img("Header", root, k.skin.header, hc, new Vector2(hw, hw * hs.height / Mathf.Max(1f, hs.width)), false);
            }
            p.titleAnchor = RT("TitleAnchor", root, hc + new Vector2(0f, 24f), new Vector2(hw * 0.8f, 200f));
            p.title = Txt(k, "Title", root, title, hc + new Vector2(0f, 15f), new Vector2(hw - 200f, 200f), 120f, k.theme != null ? k.theme.titleMaterial : null, Color.white);
            if (close) Close(k, root, new Vector2(w * 0.5f - 43f, h * 0.5f - 24f), closeId);
        }

        static void Inside(Kit k, Transform root, Vector2 pos, Vector2 size)
        {
            if (k.skin != null && k.skin.inside != null) Img("Inside", root, k.skin.inside, pos, size, true, 1f);
        }

        static Sprite S(Kit k, Func<PopupSkin, Sprite> pick) => k.skin != null ? pick(k.skin) : null;

        // ---------------- popups ----------------

        static GameObject Message(Kit k, string name, string title, (string id, string label, bool main)[] buttons)
        {
            var p = Root(k, name);
            const float w = 1290f, h = 1075f;
            Panel(k, p, title, w, h, false);
            Inside(k, p.panel, new Vector2(0f, 55f), new Vector2(w - 195f, 463f));
            p.bodyText = Txt(k, "Body", p.panel, "", new Vector2(0f, 60f), new Vector2(w - 260f, 440f), 72f, k.theme != null ? k.theme.bodyMaterial : null, k.bodyColor);
            float bw = buttons.Length > 1 ? 475f : 610f, gap = 43f;
            float total = buttons.Length * bw + (buttons.Length - 1) * gap;
            for (int i = 0; i < buttons.Length; i++)
            {
                var (id, label, main) = buttons[i];
                var pos = new Vector2(-total * 0.5f + bw * (i + 0.5f) + gap * i, -335f);
                Button(k, p.panel, id, label, pos, new Vector2(bw, 183f), main ? S(k, s => s.green) : S(k, s => s.yellow), 80f);
            }
            return p.gameObject;
        }

        /// <summary>Well Done: Spine title on the ribbon (TitleAnchor), body text, Replay / Next.</summary>
        public static GameObject BuildWin(Kit k) => Message(k, Win, "", new[] { ("replay", "Replay", false), ("next", "Next", true) });

        /// <summary>Out of moves: what is left to break, Try Again.</summary>
        public static GameObject BuildLose(Kit k) => Message(k, Lose, "Out of Moves", new[] { ("retry", "Try Again", true) });

        static GameObject Menu(Kit k, string name, string title, (string id, string label, bool main)[] items)
        {
            var p = Root(k, name);
            const float bh = 171f, gap = 37f;
            float listH = items.Length * bh + Mathf.Max(0, items.Length - 1) * gap;
            float w = 1170f, h = listH + 440f;
            Panel(k, p, title, w, h, true);
            float y = h * 0.5f - 250f - bh * 0.5f;
            foreach (var (id, label, main) in items)
            {
                Button(k, p.panel, id, label, new Vector2(0f, y), new Vector2(805f, bh), main ? S(k, s => s.green) : S(k, s => s.blue), 72f);
                y -= bh + gap;
            }
            return p.gameObject;
        }

        /// <summary>Cheats: win / lose / +10 shots / refill boosters / go to level, close X.</summary>
        public static GameObject BuildCheat(Kit k) => Menu(k, Cheat, "Cheats", new[]
        {
            ("win", "Win Level", true), ("lose", "Lose Level", false), ("shots", "+10 Shots", false),
            ("boosters", "Refill Boosters", false), ("level", "Go to Level...", false),
        });

        /// <summary>Dev haptics tools; the X goes back to the settings.</summary>
        public static GameObject BuildHaptics(Kit k) => Menu(k, Haptics, "Haptics", new[]
        {
            ("lab", "Haptics Lab", false), ("map", "Game Haptics Map", false), ("reset", "Reset device tuning", false),
        });

        /// <summary>Settings: Music / Sound / Vibration rows with ON/OFF, Replay, Haptics Tools (dev), close X.</summary>
        public static GameObject BuildSettings(Kit k)
        {
            var p = Root(k, Settings);
            var rows = new[] { ("music", "Music"), ("sound", "Sound"), ("vibration", "Vibration") };
            var buttons = new[] { ("replay", "Replay", true), ("haptics", "Haptics Tools", false) };
            const float rowH = 165f, rowGap = 27f, bh = 171f, gap = 37f;
            float insideH = rows.Length * rowH + (rows.Length - 1) * rowGap + 73f;
            float buttonsH = buttons.Length * bh + (buttons.Length - 1) * gap;
            float w = 1195f, h = 280f + insideH + 61f + buttonsH + 146f;
            Panel(k, p, "Settings", w, h, true);
            float top = h * 0.5f - 250f;
            Inside(k, p.panel, new Vector2(0f, top - insideH * 0.5f), new Vector2(w - 160f, insideH));
            float y = top - 37f - rowH * 0.5f;
            foreach (var (id, label) in rows)
            {
                var rt = RT("Row_" + id, p.panel, new Vector2(0f, y), new Vector2(w - 220f, rowH));
                var row = rt.gameObject.AddComponent<UiToggleRow>();
                row.id = id;
                row.label = Txt(k, "Label", rt, label, new Vector2(-180f, 0f), new Vector2(560f, rowH), 76f,
                    k.theme != null ? k.theme.bodyMaterial : null, k.bodyColor, TextAlignmentOptions.Left);
                row.onSprite = S(k, s => s.green);
                row.offSprite = S(k, s => s.red != null ? s.red : s.yellow);
                row.onTextMaterial = TextMat(k, row.onSprite);
                row.offTextMaterial = TextMat(k, row.offSprite);
                row.toggle = Button(k, rt, id, "ON", new Vector2(323f - 0f, 0f), new Vector2(280f, 137f), row.onSprite, 68f);
                y -= rowH + rowGap;
            }
            y = top - insideH - 61f - bh * 0.5f;
            foreach (var (id, label, main) in buttons)
            {
                Button(k, p.panel, id, label, new Vector2(0f, y), new Vector2(805f, bh), main ? S(k, s => s.yellow) : S(k, s => s.blue), 72f);
                y -= bh + gap;
            }
            return p.gameObject;
        }

        static GameObject Prompt(Kit k, string name, string title, string hint)
        {
            var p = Root(k, name);
            const float w = 1220f, h = 927f;
            Panel(k, p, title, w, h, true);
            var th = k.theme;
            p.hintText = Txt(k, "Hint", p.panel, hint, new Vector2(0f, 171f), new Vector2(1000f, 90f), 62f, th != null ? th.titleMaterial : null, Color.white);
            // Text field (TMP_InputField: system keyboard on phones, hidden text for passwords).
            var field = RT("Field", p.panel, new Vector2(0f, 18f), new Vector2(830f, 177f));
            var bg = field.gameObject.AddComponent<Image>();
            bg.sprite = S(k, s => s.inside);
            if (bg.sprite != null && bg.sprite.border != Vector4.zero) bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;
            var area = RT("Text Area", field, Vector2.zero, Vector2.zero);
            Stretch(area);
            area.offsetMin = new Vector2(40f, 20f);
            area.offsetMax = new Vector2(-40f, -20f);
            area.gameObject.AddComponent<RectMask2D>();
            var text = Txt(k, "Text", area, "", Vector2.zero, Vector2.zero, 76f, th != null ? th.bodyMaterial : null, k.bodyColor);
            Stretch(text.rectTransform);
            var ph = Txt(k, "Placeholder", area, "Tap to type", Vector2.zero, Vector2.zero, 64f, th != null ? th.bodyMaterial : null,
                new Color(k.bodyColor.r, k.bodyColor.g, k.bodyColor.b, 0.45f));
            Stretch(ph.rectTransform);
            var input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = ph;
            input.targetGraphic = bg;
            input.characterLimit = 32;
            input.fontAsset = th != null ? th.font : null;
            input.pointSize = 76f;
            input.lineType = TMP_InputField.LineType.SingleLine;
            p.input = input;
            p.errorText = Txt(k, "Error", p.panel, "", new Vector2(0f, -122f), new Vector2(1000f, 80f), 54f, th != null ? th.titleMaterial : null, new Color(1f, 0.88f, 0.35f));
            Button(k, p.panel, "cancel", "Cancel", new Vector2(-268f, -299f), new Vector2(464f, 177f), S(k, s => s.red != null ? s.red : s.yellow), 72f);
            Button(k, p.panel, "ok", "OK", new Vector2(268f, -299f), new Vector2(464f, 177f), S(k, s => s.green), 72f);
            return p.gameObject;
        }

        /// <summary>Cheat password (hidden text).</summary>
        public static GameObject BuildPassword(Kit k) => Prompt(k, Password, "Cheat", "Enter password");

        /// <summary>Cheat: level number (digits only).</summary>
        public static GameObject BuildLevelInput(Kit k) => Prompt(k, LevelInput, "Go to Level", "Level number");

        // ---------------- HUD ----------------

        /// <summary>HUD canvas: safe area (moves badge top left, settings top right), boosters (follow the cannon), popup layer.</summary>
        public static GameObject BuildHud(Kit k)
        {
            var go = new GameObject(Hud, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = 5f;
            canvas.sortingOrder = 800;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 3120f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            go.AddComponent<GraphicRaycaster>();
            var hud = go.AddComponent<HudCanvas>();
            hud.canvas = canvas;
            var th = k.theme;

            // Boosters first (under the safe-area HUD and the popups).
            hud.boosters = RT("Boosters", go.transform, Vector2.zero, Vector2.zero);
            var defs = new[] { (BoosterId.X2, -660f), (BoosterId.Arrow, -433f), (BoosterId.Bowling, 433f), (BoosterId.Bomb, 660f) };
            foreach (var (id, x) in defs) BoosterButton(k, hud.boosters, id, new Vector2(x, 0f));

            var safe = RT("SafeArea", go.transform, Vector2.zero, Vector2.zero);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            hud.safeArea = safe;

            // Moves badge (top left)
            var badge = RT("MovesBadge", safe, new Vector2(150f, -162f), new Vector2(268f, 251f), new Vector2(0f, 1f));
            var bi = badge.gameObject.AddComponent<Image>();
            bi.sprite = k.badge;
            bi.raycastTarget = false;
            if (k.badge == null) bi.color = new Color(0.45f, 0.2f, 0.8f);
            hud.movesBadge = badge;
            hud.movesTitle = Txt(k, "Title", badge, "Shots", new Vector2(0f, 65f), new Vector2(250f, 70f), 52f, th != null ? th.purpleText : null, Color.white);
            hud.movesText = Txt(k, "Moves", badge, "0", new Vector2(0f, -27f), new Vector2(250f, 150f), 128f, th != null ? th.purpleText : null, Color.white);

            // Settings (top right)
            var set = RT("SettingsButton", safe, new Vector2(-150f, -162f), new Vector2(183f, 183f), new Vector2(1f, 1f));
            var si = set.gameObject.AddComponent<Image>();
            si.sprite = th != null ? th.settingsButton : null;
            si.preserveAspect = true;
            si.raycastTarget = true;
            hud.settingsButton = set.gameObject.AddComponent<UiButton>();
            hud.settingsButton.id = "settings";
            hud.settingsButton.art = si;

            // Popups (last: above everything)
            var layer = RT("Popups", go.transform, Vector2.zero, Vector2.zero);
            Stretch(layer);
            hud.popupLayer = layer;
            return go;
        }

        static void BoosterButton(Kit k, Transform parent, BoosterId id, Vector2 pos)
        {
            const float size = 152f;
            var rt = RT("Booster_" + id, parent, pos, new Vector2(size, size));
            var ub = rt.gameObject.AddComponent<UiBoosterButton>();
            ub.booster = id;
            ub.glow = Img("Glow", rt, k.boosterPlate, Vector2.zero, new Vector2(size * 1.3f, size * 1.3f), false);
            ub.glow.color = new Color(1f, 0.85f, 0.3f, 0.6f);
            ub.glow.enabled = false;
            ub.plate = Img("Plate", rt, k.boosterPlate, Vector2.zero, new Vector2(size, size), false);
            ub.plate.raycastTarget = true;
            ub.icon = Img("Icon", rt, k.boosterIcon != null ? k.boosterIcon(id) : null, Vector2.zero, new Vector2(size * 0.78f, size * 0.78f), false);
            ub.bubble = Img("Bubble", rt, k.boosterBubble, new Vector2(57f, -52f), new Vector2(91f, 91f), false);
            ub.count = Txt(k, "Count", rt, "2", new Vector2(57f, -50f), new Vector2(90f, 70f), 46f, k.theme != null ? k.theme.redText : null, Color.white);
            ub.label = Txt(k, "Label", rt, "", Vector2.zero, new Vector2(size, 70f), 52f, k.theme != null ? k.theme.purpleText : null, Color.white);
            var b = rt.gameObject.AddComponent<UiButton>();
            b.id = id.ToString();
            b.art = ub.plate;
            ub.button = b;
        }
    }
}
