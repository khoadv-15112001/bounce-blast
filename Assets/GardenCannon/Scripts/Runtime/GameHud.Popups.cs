using System;
using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>Popup art from the original game UI (Assets/GardenCannon/Art/UI/Popup, cut from SpriteExport).</summary>
    [Serializable]
    public class PopupSkin
    {
        [Tooltip("Red popup body with the gold rim (9-sliced).")]
        public Sprite body;
        [Tooltip("Arched title ribbon on top of the body.")]
        public Sprite header;
        [Tooltip("Cream inner panel (9-sliced).")]
        public Sprite inside;
        public Sprite green, yellow, blue, red, purple;
        [Tooltip("Round red X button.")]
        public Sprite close;

        public bool Valid => body != null && inside != null && green != null && yellow != null;
    }

    /// <summary>
    /// Popups opened from their prefabs (Assets/GardenCannon/Prefabs/Popups, one per popup, edited by hand): settings,
    /// Well Done, out of moves, cheats, password / level prompts, haptics tools. Plus the win celebration.
    /// </summary>
    public partial class GameHud
    {
        [Header("Popups (prefabs set at runtime from GameAssets)")]
        public PopupPrefabs popupPrefabs = new PopupPrefabs();
        [Tooltip("Art used when a popup has to be built without its prefab.")]
        public PopupSkin skin = new PopupSkin();
        public Color popupTextColor = new Color(0.45f, 0.2f, 0.1f);
        public Color titleOutlineColor = new Color(0.42f, 0.05f, 0.12f);
        public Color buttonOutlineColor = new Color(0.2f, 0.12f, 0.05f, 0.9f);
        [Header("Win celebration")]
        [Tooltip("Width of the three celebrating characters on the celebration screen.")]
        public float celebrationWidth = 11f;
        [Tooltip("Screen height (popup space) of the centre of the characters' setup pose.")]
        public float celebrationY = -3f;

        /// <summary>The level plate was tapped 5 times in a row (cheat menu).</summary>
        public Action CheatRequested;
        /// <summary>True while a text field is typing: keyboard shortcuts of the game are off.</summary>
        public bool CaptureKeyboard => _pwRoot != null && _popupRoot == _pwRoot;

        readonly List<float> _plateTaps = new List<float>();

        PopupLayout.Kit Kit() => new PopupLayout.Kit
        {
            skin = skin, font = font, white = whiteSprite,
            text = popupTextColor, titleOutline = titleOutlineColor, buttonOutline = buttonOutlineColor,
        };

        // ---------------- opening a popup ----------------

        /// <summary>Open a popup from its prefab (built from the default layout when the prefab is missing).</summary>
        PopupView OpenView(GameObject prefab, Func<PopupLayout.Kit, GameObject> build, bool punch = true)
        {
            ClosePopupNow();
            GameObject go;
            if (prefab != null) go = Instantiate(prefab, transform, false);
            else
            {
                go = build(Kit());
                go.transform.SetParent(transform, false);
            }
            _popupRoot = go.transform;
            _popupRoot.position = uiCamera != null ? uiCamera.transform.position + uiCamera.transform.forward * 5f : new Vector3(0, 0, -5f);
            var view = go.GetComponent<PopupView>();
            if (view == null) view = go.AddComponent<PopupView>();
            Relayer(_popupRoot);
            if (punch && view.punch)
            {
                var baseScale = _popupRoot.localScale;
                _popupRoot.localScale = baseScale * 0.6f;
                Punch(_popupRoot);
                _baseScale[_popupRoot] = baseScale;
            }
            return view;
        }

        /// <summary>Hook a prefab button by id; a null action hides the button.</summary>
        void Bind(PopupView view, string id, Action action)
        {
            var b = view != null ? view.Button(id) : null;
            if (b == null) return;
            if (action == null) { b.gameObject.SetActive(false); return; }
            _buttons.Add(new Button { Target = b.transform, Size = b.HitSize, OnClick = action, Popup = true, Lossy = true });
        }

        static void SetText(TextMesh t, string text) { if (t != null) t.text = text; }

        // ---------------- settings ----------------

        /// <summary>Settings: Music / Sound / Vibration ON-OFF rows, Replay, Haptics Tools (null hides it), close X.</summary>
        public void ShowSettings(bool music, bool sound, bool vibration, Action toggleMusic, Action toggleSound, Action toggleVibration,
            Action replay, Action hapticsTools)
        {
            bool reopen = _popupRoot != null && _popupRoot.GetComponent<PopupView>() != null && _popupRoot.name.StartsWith(PopupLayout.Settings);
            var v = OpenView(popupPrefabs.settings, PopupLayout.BuildSettings, !reopen);
            v.Row("music")?.SetOn(music);
            v.Row("sound")?.SetOn(sound);
            v.Row("vibration")?.SetOn(vibration);
            Bind(v, "music", toggleMusic);
            Bind(v, "sound", toggleSound);
            Bind(v, "vibration", toggleVibration);
            Bind(v, "replay", replay);
            Bind(v, "haptics", hapticsTools);
            Bind(v, "close", ClosePopup);
        }

        /// <summary>Dev haptics tools; X = back.</summary>
        public void ShowHaptics(Action lab, Action map, Action reset, Action back)
        {
            var v = OpenView(popupPrefabs.haptics, PopupLayout.BuildHaptics);
            Bind(v, "lab", lab);
            Bind(v, "map", map);
            Bind(v, "reset", reset);
            Bind(v, "close", back);
        }

        // ---------------- win / lose ----------------

        /// <summary>
        /// Win celebration (before the Well Done popup): the three characters jump in over a light dim. The fireworks
        /// are played by the controller (FxPool). <paramref name="onDone"/> runs when the characters' animation ends.
        /// </summary>
        public void ShowWinCelebration(Action onDone)
        {
            ClosePopupNow();
            _popupRoot = new GameObject("WinCelebration").transform;
            _popupRoot.SetParent(transform, false);
            _popupRoot.position = uiCamera != null ? uiCamera.transform.position + uiCamera.transform.forward * 5f : new Vector3(0, 0, -5f);
            if (whiteSprite != null)
            {
                var sz = whiteSprite.bounds.size;
                var dim = VisualUtil.MakeSprite("Dim", _popupRoot, whiteSprite, Vector3.zero, 0f, 900);
                dim.color = new Color(0f, 0f, 0f, 0.35f);
                dim.transform.localScale = new Vector3(80f / sz.x, 120f / sz.y, 1f);
            }
            bool done = false;
            void Finish() { if (done) return; done = true; onDone?.Invoke(); }
            Spine.Unity.SkeletonAnimation sa = null;
            if (winSpine != null)
            {
                sa = AddSpine("WinCharacters", winSpine, "Idle", null, celebrationWidth, 907, out _);
                if (sa != null)
                {
                    PlaceSpine(sa, winSpine, new Vector3(0f, celebrationY, -0.45f));
                    sa.AnimationState.Complete += _ => Finish();
                }
            }
            Relayer(_popupRoot);
            if (sa == null) StartCoroutine(After(1.2f, Finish));
        }

        System.Collections.IEnumerator After(float seconds, Action a)
        {
            yield return new WaitForSeconds(seconds);
            a?.Invoke();
        }

        /// <summary>Well Done popup: Spine "Well Done" on the ribbon (prefab TitleAnchor), body text, Replay / Next.</summary>
        public void ShowWinPopup(string body, Action replay, Action next)
        {
            var v = OpenView(popupPrefabs.win, PopupLayout.BuildWin);
            SetText(v.bodyText, body);
            Bind(v, "replay", replay);
            Bind(v, "next", next);
            if (winTextSpine != null)
            {
                if (v.title != null) v.title.Text = "";
                var anchor = v.titleAnchor != null ? v.titleAnchor : _popupRoot;
                var sa = AddSpine("WellDone", winTextSpine, "Welldone", "Welldone_Idle", v.titleSpineWidth, 906, out _, anchor);
                if (sa != null) PlaceSpine(sa, winTextSpine, Vector3.zero);
            }
            else if (v.title != null && string.IsNullOrEmpty(v.title.Text)) v.title.Text = "Well Done!";
            Relayer(_popupRoot);
        }

        /// <summary>Out of moves popup.</summary>
        public void ShowLosePopup(string body, Action retry)
        {
            var v = OpenView(popupPrefabs.lose, PopupLayout.BuildLose);
            SetText(v.bodyText, body);
            Bind(v, "retry", retry);
        }

        // ---------------- cheats ----------------

        public void ShowCheats(Action win, Action lose, Action shots, Action boosters, Action level)
        {
            var v = OpenView(popupPrefabs.cheat, PopupLayout.BuildCheat);
            Bind(v, "win", win);
            Bind(v, "lose", lose);
            Bind(v, "shots", shots);
            Bind(v, "boosters", boosters);
            Bind(v, "level", level);
            Bind(v, "close", ClosePopup);
        }

        void OnLevelPlateTap()
        {
            float now = Time.unscaledTime;
            _plateTaps.RemoveAll(t => now - t > 2.5f);
            _plateTaps.Add(now);
            if (_plateTaps.Count < 5) return;
            _plateTaps.Clear();
            CheatRequested?.Invoke();
        }

        // ---------------- text prompts (password / level number) ----------------

        Transform _pwRoot;
        TextMesh _pwText, _pwError;
        string _pw = "";
        Func<string, bool> _pwCheck;
        Action<string> _pwOk;
        TouchScreenKeyboard _kb;
        bool _pwMasked, _pwNumeric;
        string _pwErrorText = "Wrong password";

        /// <summary>Password prompt (text hidden). Mobile: the system keyboard; Editor / desktop: the keyboard.</summary>
        public void ShowPasswordPrompt(Func<string, bool> check, Action onOk) =>
            ShowPrompt(popupPrefabs.password, PopupLayout.BuildPassword, null, true, false, check, _ => onOk?.Invoke(), "Wrong password");

        /// <summary>Level number prompt (digits only, number pad on phones).</summary>
        public void ShowLevelPrompt(string hint, Func<string, bool> check, Action<string> onOk, string errorText) =>
            ShowPrompt(popupPrefabs.levelInput, PopupLayout.BuildLevelInput, hint, false, true, check, onOk, errorText);

        void ShowPrompt(GameObject prefab, Func<PopupLayout.Kit, GameObject> build, string hint, bool masked, bool numeric,
            Func<string, bool> check, Action<string> onOk, string errorText)
        {
            var v = OpenView(prefab, build);
            _pwRoot = _popupRoot;
            _pw = "";
            _pwCheck = check;
            _pwOk = onOk;
            _pwMasked = masked;
            _pwNumeric = numeric;
            _pwErrorText = errorText;
            if (hint != null) SetText(v.hintText, hint);
            _pwText = v.fieldText;
            _pwError = v.errorText;
            SetText(_pwError, "");
            Bind(v, "field", OpenKeyboard);
            Bind(v, "ok", SubmitPassword);
            Bind(v, "cancel", ClosePassword);
            Bind(v, "close", ClosePassword);
            RefreshPassword();
            OpenKeyboard();
        }

        void OpenKeyboard()
        {
            if (!TouchScreenKeyboard.isSupported) return;
            _kb = TouchScreenKeyboard.Open(_pw, _pwNumeric ? TouchScreenKeyboardType.NumberPad : TouchScreenKeyboardType.Default,
                false, false, _pwMasked, false, _pwNumeric ? "Level" : "Password");
        }

        void RefreshPassword()
        {
            if (_pwText == null) return;
            _pwText.text = _pw.Length > 0 ? (_pwMasked ? new string('*', _pw.Length) : _pw) : "Tap to type";
            _pwText.color = _pw.Length > 0 ? popupTextColor : new Color(popupTextColor.r, popupTextColor.g, popupTextColor.b, 0.45f);
        }

        void SubmitPassword()
        {
            if (_pwCheck != null && _pwCheck(_pw))
            {
                var ok = _pwOk;
                string value = _pw;
                ClosePassword();
                ok?.Invoke(value);
                return;
            }
            _pw = "";
            RefreshPassword();
            SetText(_pwError, _pwErrorText);
        }

        void ClosePassword()
        {
            if (_kb != null) { _kb.active = false; _kb = null; }
            _pwCheck = null;
            _pwOk = null;
            if (_pwRoot != null && _popupRoot == _pwRoot) ClosePopup();
            _pwRoot = null;
        }

        string Filter(string s)
        {
            if (!_pwNumeric) return s;
            var b = new System.Text.StringBuilder();
            foreach (char c in s) if (char.IsDigit(c)) b.Append(c);
            return b.ToString();
        }

        /// <summary>Called from Update: typing into the prompt field.</summary>
        void UpdatePassword()
        {
            if (_pwRoot == null) return;
            if (_popupRoot != _pwRoot) { _pwRoot = null; _kb = null; return; }
            if (_kb != null)
            {
                if (_kb.text != _pw) { _pw = Filter(_kb.text ?? ""); RefreshPassword(); }
                if (_kb.status == TouchScreenKeyboard.Status.Done) { _kb = null; SubmitPassword(); return; }
                if (_kb.status == TouchScreenKeyboard.Status.Canceled || _kb.status == TouchScreenKeyboard.Status.LostFocus) _kb = null;
                return;
            }
            string s = Input.inputString;
            if (string.IsNullOrEmpty(s)) return;
            foreach (char c in s)
            {
                if (c == '\b') { if (_pw.Length > 0) _pw = _pw.Substring(0, _pw.Length - 1); }
                else if (c == '\n' || c == '\r') { SubmitPassword(); return; }
                else if (c >= ' ' && _pw.Length < 32 && (!_pwNumeric || char.IsDigit(c))) _pw += c;
            }
            RefreshPassword();
            SetText(_pwError, "");
        }
    }
}
