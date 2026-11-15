using System;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>The popup prefabs (Assets/GardenCannon/Prefabs/Popups), one per popup.</summary>
    [Serializable]
    public class PopupPrefabs
    {
        public GameObject win, lose, settings, cheat, password, levelInput, haptics;
    }

    /// <summary>
    /// Default layout of every popup. The editor saves these as prefabs once (then they are edited by hand); the game
    /// only builds them itself when a prefab is missing.
    /// </summary>
    public static class PopupLayout
    {
        public struct Kit
        {
            public PopupSkin skin;
            public Font font;
            public Sprite white;
            public Color text, titleOutline, buttonOutline;
        }

        public const string Win = "PopupWin", Lose = "PopupLose", Settings = "PopupSettings", Cheat = "PopupCheat",
            Password = "PopupPassword", LevelInput = "PopupLevelInput", Haptics = "PopupHaptics";

        // ---------------- pieces ----------------

        static PopupView Root(Kit k, string name, float dimAlpha = 0.65f)
        {
            var go = new GameObject(name);
            var view = go.AddComponent<PopupView>();
            if (k.white != null && dimAlpha > 0f)
            {
                var sz = k.white.bounds.size;
                var dim = VisualUtil.MakeSprite("Dim", go.transform, k.white, Vector3.zero, 0f, 900);
                dim.color = new Color(0f, 0f, 0f, dimAlpha);
                dim.transform.localScale = new Vector3(80f / sz.x, 120f / sz.y, 1f);
            }
            return view;
        }

        static SpriteRenderer Sliced(string name, Transform parent, Sprite s, Vector3 pos, Vector2 size, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = pos;
            sr.sprite = s;
            if (s != null)
            {
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = size;
            }
            sr.sortingOrder = order;
            return sr;
        }

        public static OutlinedLabel Label(Kit k, Transform parent, string name, string text, Vector3 pos, float size, Color fill, Color outline, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<OutlinedLabel>();
            float w = size * 0.075f;
            l.outline = new TextMesh[12];
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                l.outline[i] = VisualUtil.MakeText("Outline", go.transform, k.font, new Vector3(Mathf.Cos(a) * w, Mathf.Sin(a) * w, 0.02f), size, outline, TextAnchor.MiddleCenter, order);
            }
            l.main = VisualUtil.MakeText("Text", go.transform, k.font, Vector3.zero, size, fill, TextAnchor.MiddleCenter, order + 1);
            l.Text = text;
            return l;
        }

        static TextMesh Text(Kit k, Transform parent, string name, string text, Vector3 pos, float size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, int order = 903)
        {
            var t = VisualUtil.MakeText(name, parent, k.font, pos, size, color, anchor, order);
            t.text = text;
            return t;
        }

        public static PopupButton Button(Kit k, Transform parent, string id, string label, Vector3 pos, Vector2 size, Sprite art, float textSize = 0.6f)
        {
            var go = new GameObject("Btn_" + id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var b = go.AddComponent<PopupButton>();
            b.id = id;
            b.art = Sliced("Art", go.transform, art, Vector3.zero, size, 905);
            b.label = Label(k, go.transform, "Label", label, new Vector3(0f, 0.04f, -0.1f), textSize, Color.white, k.buttonOutline, 906);
            b.hitSize = size;
            return b;
        }

        static PopupButton Close(Kit k, Transform parent, Vector3 pos, string id = "close")
        {
            var go = new GameObject("Btn_" + id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var b = go.AddComponent<PopupButton>();
            b.id = id;
            if (k.skin != null && k.skin.close != null) b.art = VisualUtil.MakeSprite("Art", go.transform, k.skin.close, Vector3.zero, 1.25f, 907);
            else
            {
                var t = Text(k, go.transform, "X", "X", Vector3.zero, 0.8f, Color.white, TextAnchor.MiddleCenter, 907);
                t.transform.localPosition = Vector3.zero;
            }
            b.hitSize = new Vector2(1.35f, 1.35f);
            return b;
        }

        /// <summary>Body + title ribbon (+ title text) + optional close button.</summary>
        static void Panel(Kit k, PopupView v, string title, float w, float h, bool close, string closeId = "close")
        {
            var root = v.transform;
            v.body = Sliced("Body", root, k.skin != null ? k.skin.body : null, new Vector3(0f, 0f, -0.05f), new Vector2(w, h), 901);
            if (k.skin == null || k.skin.body == null)
            {
                v.body.sprite = k.white;
                v.body.color = new Color(1f, 0.965f, 0.9f);
                var sz = k.white != null ? k.white.bounds.size : Vector3.one;
                v.body.transform.localScale = new Vector3(w / sz.x, h / sz.y, 1f);
            }
            var hc = new Vector3(0f, h * 0.5f + 0.15f, -0.1f);
            if (k.skin != null && k.skin.header != null)
                VisualUtil.MakeSprite("Header", root, k.skin.header, hc, Mathf.Min(w * 1.02f, 9.8f), 902);
            var anchor = new GameObject("TitleAnchor").transform;
            anchor.SetParent(root, false);
            anchor.localPosition = hc + new Vector3(0f, 0.2f, -0.25f);
            v.titleAnchor = anchor;
            v.title = Label(k, root, "Title", title, hc + new Vector3(0f, 0.12f, -0.1f), 1.0f, Color.white, k.titleOutline, 903);
            if (close) Close(k, root, new Vector3(w * 0.5f - 0.35f, h * 0.5f - 0.2f, -0.3f), closeId);
        }

        static void Inside(Kit k, Transform root, Vector3 pos, Vector2 size)
        {
            if (k.skin != null && k.skin.inside != null) Sliced("Inside", root, k.skin.inside, pos, size, 902);
        }

        static Sprite S(Kit k, Func<PopupSkin, Sprite> pick) => k.skin != null ? pick(k.skin) : null;

        // ---------------- popups ----------------

        static GameObject Message(Kit k, string name, string title, (string id, string label, bool main)[] buttons)
        {
            var v = Root(k, name);
            const float w = 10.6f, h = 8.8f;
            Panel(k, v, title, w, h, false);
            Inside(k, v.transform, new Vector3(0f, 0.45f, -0.1f), new Vector2(w - 1.6f, 3.8f));
            v.bodyText = Text(k, v.transform, "Body", "", new Vector3(0f, 0.5f, -0.2f), 0.6f, k.text);
            float bw = buttons.Length > 1 ? 3.9f : 5f, gap = 0.35f;
            float total = buttons.Length * bw + (buttons.Length - 1) * gap;
            for (int i = 0; i < buttons.Length; i++)
            {
                var (id, label, main) = buttons[i];
                var p = new Vector3(-total * 0.5f + bw * (i + 0.5f) + gap * i, -2.75f, -0.2f);
                Button(k, v.transform, id, label, p, new Vector2(bw, 1.5f), main ? S(k, s => s.green) : S(k, s => s.yellow), 0.68f);
            }
            return v.gameObject;
        }

        /// <summary>Well Done: Spine title on the ribbon (TitleAnchor), body text, Replay / Next.</summary>
        public static GameObject BuildWin(Kit k) =>
            Message(k, Win, "", new[] { ("replay", "Replay", false), ("next", "Next", true) });

        /// <summary>Out of moves: title, what is left to break, Try Again.</summary>
        public static GameObject BuildLose(Kit k) =>
            Message(k, Lose, "Out of Moves", new[] { ("retry", "Try Again", true) });

        static GameObject Menu(Kit k, string name, string title, (string id, string label, bool main)[] items, string closeId = "close")
        {
            var v = Root(k, name);
            const float bh = 1.4f, gap = 0.3f;
            float listH = items.Length * bh + Mathf.Max(0, items.Length - 1) * gap;
            float w = 9.6f, h = listH + 3.6f;
            Panel(k, v, title, w, h, true, closeId);
            float y = h * 0.5f - 2.05f - bh * 0.5f;
            foreach (var (id, label, main) in items)
            {
                Button(k, v.transform, id, label, new Vector3(0f, y, -0.2f), new Vector2(6.6f, bh), main ? S(k, s => s.green) : S(k, s => s.blue), 0.6f);
                y -= bh + gap;
            }
            return v.gameObject;
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

        /// <summary>Settings: Music / Sound / Vibration rows with ON/OFF, then Replay and (dev builds) Haptics Tools.</summary>
        public static GameObject BuildSettings(Kit k)
        {
            var v = Root(k, Settings);
            var rows = new[] { ("music", "Music"), ("sound", "Sound"), ("vibration", "Vibration") };
            var buttons = new[] { ("replay", "Replay", true), ("haptics", "Haptics Tools", false) };
            const float rowH = 1.35f, rowGap = 0.22f, bh = 1.4f, gap = 0.3f;
            float insideH = rows.Length * rowH + (rows.Length - 1) * rowGap + 0.6f;
            float buttonsH = buttons.Length * bh + (buttons.Length - 1) * gap;
            float w = 9.8f, h = 2.3f + insideH + 0.5f + buttonsH + 1.2f;
            Panel(k, v, "Settings", w, h, true);
            float top = h * 0.5f - 2.05f;
            Inside(k, v.transform, new Vector3(0f, top - insideH * 0.5f, -0.1f), new Vector2(w - 1.3f, insideH));
            float y = top - 0.3f - rowH * 0.5f;
            foreach (var (id, label) in rows)
            {
                var row = new GameObject("Row_" + id).AddComponent<PopupToggleRow>();
                row.transform.SetParent(v.transform, false);
                row.transform.localPosition = new Vector3(0f, y, -0.2f);
                row.id = id;
                row.label = Text(k, row.transform, "Label", label, new Vector3(-3.6f, 0f, 0f), 0.62f, k.text, TextAnchor.MiddleLeft);
                row.onSprite = S(k, s => s.green);
                row.offSprite = S(k, s => s.red != null ? s.red : s.yellow);
                row.toggle = Button(k, row.transform, id, "ON", new Vector3(2.65f, 0f, 0f), new Vector2(2.3f, 1.12f), row.onSprite, 0.56f);
                y -= rowH + rowGap;
            }
            y = top - insideH - 0.5f - bh * 0.5f;
            foreach (var (id, label, main) in buttons)
            {
                Button(k, v.transform, id, label, new Vector3(0f, y, -0.2f), new Vector2(6.6f, bh), main ? S(k, s => s.yellow) : S(k, s => s.blue), 0.6f);
                y -= bh + gap;
            }
            return v.gameObject;
        }

        static GameObject Prompt(Kit k, string name, string title, string hint)
        {
            var v = Root(k, name);
            const float w = 10f, h = 7.6f;
            Panel(k, v, title, w, h, true);
            bool skinned = k.skin != null && k.skin.body != null;
            v.hintText = Text(k, v.transform, "Hint", hint, new Vector3(0f, 1.4f, -0.2f), 0.5f, skinned ? Color.white : k.text);
            var field = Button(k, v.transform, "field", "", new Vector3(0f, 0.15f, -0.15f), new Vector2(6.8f, 1.45f), S(k, s => s.inside), 0.6f);
            field.art.sortingOrder = 902;
            UnityEngine.Object.DestroyImmediate(field.label.gameObject);
            field.label = null;
            v.fieldText = Text(k, field.transform, "Text", "Tap to type", new Vector3(0f, 0f, -0.1f), 0.62f, k.text);
            v.errorText = Text(k, v.transform, "Error", "", new Vector3(0f, -1.0f, -0.2f), 0.45f, new Color(1f, 0.85f, 0.3f));
            Button(k, v.transform, "cancel", "Cancel", new Vector3(-2.2f, -2.45f, -0.2f), new Vector2(3.8f, 1.45f), S(k, s => s.red != null ? s.red : s.yellow), 0.6f);
            Button(k, v.transform, "ok", "OK", new Vector3(2.2f, -2.45f, -0.2f), new Vector2(3.8f, 1.45f), S(k, s => s.green), 0.6f);
            return v.gameObject;
        }

        /// <summary>Cheat password (hidden text).</summary>
        public static GameObject BuildPassword(Kit k) => Prompt(k, Password, "Cheat", "Enter password");

        /// <summary>Cheat: level number.</summary>
        public static GameObject BuildLevelInput(Kit k) => Prompt(k, LevelInput, "Go to Level", "Level number");
    }
}
