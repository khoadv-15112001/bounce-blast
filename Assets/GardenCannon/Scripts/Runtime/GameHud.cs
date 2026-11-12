using System;
using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// World-space HUD built from SpriteRenderers + TextMesh (the project's uGUI/TMP assemblies are stubs).
    /// Anchors are placed by the scene builder and can be moved in the scene.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        [Header("Anchors")]
        public Transform levelAnchor;
        public Transform goalsAnchor;
        public Transform movesAnchor;
        public Transform settingsAnchor;

        [Header("Art")]
        public Font font;
        public Sprite levelPlate;
        public Sprite settingsButton;
        public Sprite panelSprite;
        public Sprite whiteSprite;

        [Header("Colours")]
        public Color levelTextColor = new Color(0.42f, 0.14f, 0.13f);
        public Color panelColor = new Color(1f, 0.965f, 0.9f);
        public Color mainButtonColor = new Color(0.97f, 0.77f, 0.22f);
        public Color altButtonColor = new Color(0.5f, 0.75f, 0.23f);

        class Button
        {
            public Bounds Area;
            public Action OnClick;
            public bool Popup;
        }

        readonly List<Button> _buttons = new List<Button>();
        TextMesh _levelText, _movesText, _movesShadow;
        Transform _goalsRoot, _popupRoot;
        readonly Dictionary<int, TextMesh> _goalTexts = new Dictionary<int, TextMesh>();
        Camera _cam;

        public bool PopupOpen => _popupRoot != null;

        public void Init(Camera cam, Action restart, Action prev, Action next)
        {
            _cam = cam;
            _buttons.Clear();

            if (levelAnchor != null)
            {
                if (levelPlate != null) VisualUtil.MakeSprite("LevelPlate", levelAnchor, levelPlate, Vector3.zero, 3.2f, 60);
                _levelText = VisualUtil.MakeText("LevelText", levelAnchor, font, new Vector3(0f, 0.02f, -0.1f), 0.62f, levelTextColor, TextAnchor.MiddleCenter, 61);
                var l = VisualUtil.MakeText("Prev", levelAnchor, font, new Vector3(-2.2f, 0f, -0.1f), 0.8f, Color.white, TextAnchor.MiddleCenter, 61);
                l.text = "<";
                var r = VisualUtil.MakeText("Next", levelAnchor, font, new Vector3(2.2f, 0f, -0.1f), 0.8f, Color.white, TextAnchor.MiddleCenter, 61);
                r.text = ">";
                AddButton(new Bounds(l.transform.position, new Vector3(0.9f, 0.9f, 10f)), prev);
                AddButton(new Bounds(r.transform.position, new Vector3(0.9f, 0.9f, 10f)), next);
            }
            if (settingsAnchor != null)
            {
                var s = settingsButton != null
                    ? VisualUtil.MakeSprite("RestartButton", settingsAnchor, settingsButton, Vector3.zero, 1.25f, 60)
                    : null;
                if (s == null)
                {
                    var t = VisualUtil.MakeText("Restart", settingsAnchor, font, Vector3.zero, 0.7f, Color.white);
                    t.text = "R";
                }
                AddButton(new Bounds(settingsAnchor.position, new Vector3(1.3f, 1.3f, 10f)), restart);
            }
            if (movesAnchor != null)
            {
                _movesShadow = VisualUtil.MakeText("MovesShadow", movesAnchor, font, new Vector3(0.04f, -0.05f, 0f), 1.0f, new Color(0.42f, 0.29f, 0.16f), TextAnchor.MiddleCenter, 61);
                _movesText = VisualUtil.MakeText("Moves", movesAnchor, font, new Vector3(0f, 0f, -0.1f), 1.0f, Color.white, TextAnchor.MiddleCenter, 62);
            }
        }

        void AddButton(Bounds b, Action a, bool popup = false) => _buttons.Add(new Button { Area = b, OnClick = a, Popup = popup });

        public void SetLevel(int number) { if (_levelText != null) _levelText.text = "Level " + number; }

        public void SetMoves(int moves)
        {
            if (_movesText != null) _movesText.text = moves.ToString();
            if (_movesShadow != null) _movesShadow.text = moves.ToString();
            if (_movesText != null) _movesText.color = moves <= 5 ? new Color(1f, 0.55f, 0.5f) : Color.white;
        }

        /// <summary>Goal row: a small copy of each item prefab plus a counter.</summary>
        public void BuildGoals(IList<GoalData> goals, ItemCatalog catalog)
        {
            if (_goalsRoot != null) Destroy(_goalsRoot.gameObject);
            _goalTexts.Clear();
            if (goalsAnchor == null) return;
            _goalsRoot = new GameObject("Goals").transform;
            _goalsRoot.SetParent(goalsAnchor, false);

            const float slot = 1.9f;
            float width = goals.Count * slot;
            if (whiteSprite != null)
            {
                var bg = VisualUtil.MakeSprite("GoalsBg", _goalsRoot, whiteSprite, new Vector3(0f, 0f, 1f), 0f, 55);
                bg.color = new Color(0.2f, 0.1f, 0.03f, 0.45f);
                var sz = whiteSprite.bounds.size;
                bg.transform.localScale = new Vector3((width + 0.3f) / sz.x, 1.15f / sz.y, 1f);
            }
            for (int i = 0; i < goals.Count; i++)
            {
                var g = goals[i];
                float x = -width * 0.5f + slot * (i + 0.5f);
                var e = catalog.Get(g.type);
                if (e != null && e.prefab != null)
                {
                    var icon = Instantiate(e.prefab, _goalsRoot);
                    VisualUtil.StripRuntimeComponents(icon);
                    icon.transform.localPosition = new Vector3(x - 0.45f, 0f, 0f);
                    icon.transform.localScale = Vector3.Scale(icon.transform.localScale, Vector3.one * 0.75f / Mathf.Max(1f, FootprintGuess(e)));
                    VisualUtil.ApplyOrientation(icon.transform, false);
                    if (e.colorByVariant) VisualUtil.ApplyAtlasTile(icon.transform, catalog, catalog.AtlasTileForVariant(FirstVariant(g.type)));
                    foreach (var anim in icon.GetComponentsInChildren<Animator>(true)) anim.enabled = false;
                }
                var t = VisualUtil.MakeText("Goal_" + g.type, _goalsRoot, font, new Vector3(x + 0.35f, 0f, -1f), 0.55f, Color.white, TextAnchor.MiddleCenter, 62);
                _goalTexts[g.type] = t;
            }
        }

        // Filled by GameController so the goal icon uses the level's block colour.
        public Func<int, int> VariantForType;
        int FirstVariant(int type) => VariantForType != null ? VariantForType(type) : 0;
        static float FootprintGuess(ItemCatalog.Entry e) => e.label.Contains("Container") ? 2.5f : (e.label.Contains("Stand") || e.label.Contains("Unbreakable") ? 1.6f : 1f);

        public void SetGoal(int type, int remaining)
        {
            if (_goalTexts.TryGetValue(type, out var t)) t.text = remaining > 0 ? remaining.ToString() : "✓";
        }

        // ---------------- Popup ----------------

        public void ShowPopup(string title, Color titleColor, string body, params (string label, Action action, bool main)[] buttons)
        {
            ClosePopup();
            _popupRoot = new GameObject("Popup").transform;
            _popupRoot.SetParent(transform, false);
            Vector3 c = _cam != null ? new Vector3(_cam.transform.position.x, _cam.transform.position.y, -30f) : new Vector3(0, 6, -30f);
            _popupRoot.position = c;

            if (whiteSprite != null)
            {
                var dim = VisualUtil.MakeSprite("Dim", _popupRoot, whiteSprite, Vector3.zero, 0f, 900);
                dim.color = new Color(0f, 0f, 0f, 0.6f);
                var sz = whiteSprite.bounds.size;
                dim.transform.localScale = new Vector3(60f / sz.x, 80f / sz.y, 1f);
            }
            var panel = VisualUtil.MakeSprite("Panel", _popupRoot, panelSprite != null ? panelSprite : whiteSprite, new Vector3(0, 0, -0.1f), 0f, 901);
            panel.color = panelColor;
            if (panel.sprite != null)
            {
                var sz = panel.sprite.bounds.size;
                panel.transform.localScale = new Vector3(10.5f / sz.x, 8.2f / sz.y, 1f);
            }
            var tt = VisualUtil.MakeText("Title", _popupRoot, font, new Vector3(0f, 2.6f, -0.2f), 1.0f, titleColor, TextAnchor.MiddleCenter, 902);
            tt.text = title;
            var bt = VisualUtil.MakeText("Body", _popupRoot, font, new Vector3(0f, 0.9f, -0.2f), 0.5f, new Color(0.35f, 0.23f, 0.12f), TextAnchor.MiddleCenter, 902);
            bt.text = body;

            float bw = 4.2f, gap = 0.4f;
            float total = buttons.Length * bw + (buttons.Length - 1) * gap;
            for (int i = 0; i < buttons.Length; i++)
            {
                var (label, action, main) = buttons[i];
                Vector3 p = new Vector3(-total * 0.5f + bw * (i + 0.5f) + gap * i, -2.2f, -0.2f);
                if (whiteSprite != null)
                {
                    var b = VisualUtil.MakeSprite("Btn_" + label, _popupRoot, whiteSprite, p, 0f, 902);
                    b.color = main ? mainButtonColor : altButtonColor;
                    var sz = whiteSprite.bounds.size;
                    b.transform.localScale = new Vector3(bw / sz.x, 1.5f / sz.y, 1f);
                }
                var lt = VisualUtil.MakeText("Label", _popupRoot, font, p + new Vector3(0, 0, -0.1f), 0.62f, Color.white, TextAnchor.MiddleCenter, 903);
                lt.text = label;
                AddButton(new Bounds(_popupRoot.TransformPoint(p), new Vector3(bw, 1.5f, 100f)), action, true);
            }
        }

        public void ClosePopup()
        {
            if (_popupRoot != null) Destroy(_popupRoot.gameObject);
            _popupRoot = null;
            _buttons.RemoveAll(b => b.Popup);
        }

        /// <summary>Returns true when the pointer hit a HUD button (and runs it).</summary>
        public bool HandleClick(Vector2 world)
        {
            bool popup = PopupOpen;
            for (int i = _buttons.Count - 1; i >= 0; i--)
            {
                var b = _buttons[i];
                if (popup && !b.Popup) continue;
                var area = b.Area;
                if (world.x >= area.min.x && world.x <= area.max.x && world.y >= area.min.y && world.y <= area.max.y)
                {
                    b.OnClick?.Invoke();
                    return true;
                }
            }
            return popup; // a click anywhere on an open popup is consumed
        }
    }
}
