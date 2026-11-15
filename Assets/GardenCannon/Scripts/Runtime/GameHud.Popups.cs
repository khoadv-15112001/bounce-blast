using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

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
    /// Canvas popups opened from their prefabs (Assets/GardenCannon/Prefabs/UI/Popups, one per popup, edited by hand):
    /// settings, Well Done, out of moves, cheats, password / level prompts, haptics tools. Plus the win celebration.
    /// </summary>
    public partial class GameHud
    {
        [Header("Popups")]
        [Tooltip("Popup art (also used to build a popup without its prefab).")]
        public PopupSkin skin = new PopupSkin();
        public Color popupTextColor = new Color(0.45f, 0.2f, 0.1f);
        [Header("Win celebration")]
        [Tooltip("Width of the three celebrating characters on the celebration screen.")]
        public float celebrationWidth = 11f;
        [Tooltip("Screen height (UI camera space) of the centre of the characters' setup pose.")]
        public float celebrationY = 2f;
        [Tooltip("Darkness of the screen behind the celebration (0..1).")]
        public float celebrationDim = 0.9f;

        /// <summary>The level plate was tapped 5 times in a row (cheat menu).</summary>
        public Action CheatRequested;
        /// <summary>True while a text field is typing: keyboard shortcuts of the game are off.</summary>
        public bool CaptureKeyboard => _input != null && _input.isFocused;

        readonly List<float> _plateTaps = new List<float>();

        // ---------------- popup prefabs (Addressables) ----------------

        readonly Dictionary<string, GameObject> _popupPrefabs = new Dictionary<string, GameObject>();
        readonly Dictionary<string, AsyncOperationHandle<GameObject>> _popupLoads = new Dictionary<string, AsyncOperationHandle<GameObject>>();
        readonly HashSet<string> _popupMissing = new HashSet<string>();
        int _openToken;

        /// <summary>Start loading every popup prefab (so they open at once later).</summary>
        void PreloadPopups()
        {
            foreach (var name in UiLayout.Popups) LoadPopup(name, null);
        }

        static bool HasAddress(string key)
        {
            foreach (var locator in Addressables.ResourceLocators)
                if (locator.Locate(key, typeof(GameObject), out _)) return true;
            return false;
        }

        /// <summary>Popup prefab from Addressables (GardenCannon/UI/&lt;name&gt;); null when it is not addressable.</summary>
        void LoadPopup(string name, Action<GameObject> done)
        {
            if (_popupPrefabs.TryGetValue(name, out var cached)) { done?.Invoke(cached); return; }
            if (_popupMissing.Contains(name)) { done?.Invoke(null); return; }
            if (!_popupLoads.TryGetValue(name, out var handle))
            {
                string key = UiLayout.Address(name);
                if (!HasAddress(key))
                {
                    _popupMissing.Add(name);
                    Debug.LogWarning($"[GardenCannon] Popup {key} is not addressable: using the default layout.");
                    done?.Invoke(null);
                    return;
                }
                handle = Addressables.LoadAssetAsync<GameObject>(key);
                _popupLoads[name] = handle;
                handle.Completed += h =>
                {
                    if (h.Status == AsyncOperationStatus.Succeeded && h.Result != null) _popupPrefabs[name] = h.Result;
                    else _popupMissing.Add(name);
                };
            }
            if (done == null) return;
            if (handle.IsDone) { done(_popupPrefabs.TryGetValue(name, out var r) ? r : null); return; }
            handle.Completed += h => done(h.Status == AsyncOperationStatus.Succeeded ? h.Result : null);
        }

        void OnDestroy()
        {
            foreach (var h in _popupLoads.Values) if (h.IsValid()) Addressables.Release(h);
            _popupLoads.Clear();
            _popupPrefabs.Clear();
        }

        // ---------------- opening a popup ----------------

        /// <summary>
        /// Open popup <paramref name="name"/>: its prefab is loaded through Addressables (default layout when missing),
        /// then <paramref name="onOpen"/> fills it. A newer open / a close cancels a popup still loading.
        /// </summary>
        void Open(string name, Func<UiLayout.Kit, GameObject> build, Action<UiPopup> onOpen, bool punch = true)
        {
            int token = ++_openToken;
            LoadPopup(name, prefab =>
            {
                if (token != _openToken || this == null) return;
                var v = OpenView(prefab, build, punch);
                if (v == null) return;
                v.name = name;
                onOpen?.Invoke(v);
            });
        }

        /// <summary>Open a canvas popup from its prefab (built from the default layout when the prefab is missing).</summary>
        UiPopup OpenView(GameObject prefab, Func<UiLayout.Kit, GameObject> build, bool punch = true)
        {
            ClosePopupNow();
            if (EnsureCanvas() == null) return null;
            var parent = Canvas.popupLayer != null ? Canvas.popupLayer : Canvas.Rect;
            GameObject go;
            if (prefab != null) go = Instantiate(prefab, parent, false);
            else
            {
                go = build(UiKit());
                go.transform.SetParent(parent, false);
            }
            var rt = (RectTransform)go.transform;
            UiLayout.Stretch(rt);
            rt.SetAsLastSibling();
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
            _popupRoot = go.transform;
            var view = go.GetComponent<UiPopup>();
            if (view == null) view = go.AddComponent<UiPopup>();
            view.Group.alpha = 1f;
            if (punch && view.punch && view.panel != null)
            {
                var baseScale = view.panel.localScale;
                view.panel.localScale = baseScale * 0.6f;
                Punch(view.panel);
                _baseScale[view.panel] = baseScale;
            }
            return view;
        }

        /// <summary>Hook a prefab button by id; a null action hides the button.</summary>
        static void Bind(UiPopup view, string id, Action action)
        {
            var b = view != null ? view.Button(id) : null;
            if (b == null) return;
            if (action == null) { b.gameObject.SetActive(false); return; }
            b.onClick = action;
        }

        static void SetText(TMP_Text t, string text) { if (t != null) t.text = text; }

        /// <summary>Canvas popup close: fade the whole popup, shrink the panel.</summary>
        System.Collections.IEnumerator CloseUi(UiPopup p)
        {
            var group = p.Group;
            group.blocksRaycasts = false;
            var panel = p.panel;
            Vector3 start = panel != null ? panel.localScale : Vector3.one;
            if (panel != null && _baseScale.TryGetValue(panel, out var b)) { start = b; _punch.Remove(panel); _baseScale.Remove(panel); }
            float a0 = group.alpha, t = 0f, dur = Mathf.Max(0.01f, popupCloseTime);
            while (t < 1f && p != null)
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / dur);
                const float c1 = 1.70158f, c3 = c1 + 1f;
                float e = c3 * t * t * t - c1 * t * t;
                if (panel != null) panel.localScale = start * Mathf.Max(0f, 1f - 0.45f * e);
                group.alpha = a0 * (1f - t * t);
                yield return null;
            }
            if (p != null) Destroy(p.gameObject);
        }

        // ---------------- settings ----------------

        /// <summary>Settings: Music / Sound / Vibration ON-OFF rows, Replay, Haptics Tools (null hides it), close X.</summary>
        public void ShowSettings(bool music, bool sound, bool vibration, Action toggleMusic, Action toggleSound, Action toggleVibration,
            Action replay, Action hapticsTools)
        {
            bool reopen = _popupRoot != null && _popupRoot.name.StartsWith(UiLayout.Settings);
            Open(UiLayout.Settings, UiLayout.BuildSettings, v =>
            {
                v.Row("music")?.SetOn(music);
                v.Row("sound")?.SetOn(sound);
                v.Row("vibration")?.SetOn(vibration);
                Bind(v, "music", toggleMusic);
                Bind(v, "sound", toggleSound);
                Bind(v, "vibration", toggleVibration);
                Bind(v, "replay", replay);
                Bind(v, "haptics", hapticsTools);
                Bind(v, "close", ClosePopup);
            }, !reopen);
        }

        /// <summary>Dev haptics tools; X = back.</summary>
        public void ShowHaptics(Action lab, Action map, Action reset, Action back)
        {
            Open(UiLayout.Haptics, UiLayout.BuildHaptics, v =>
            {
                Bind(v, "lab", lab);
                Bind(v, "map", map);
                Bind(v, "reset", reset);
                Bind(v, "close", back);
            });
        }

        // ---------------- win / lose ----------------

        /// <summary>
        /// Win celebration (before the Well Done popup): the three characters jump in over a light dim (world, UI camera,
        /// drawn over the HUD canvas). The fireworks are played by the controller. <paramref name="onDone"/> runs when the
        /// characters' animation ends.
        /// </summary>
        public void ShowWinCelebration(Action onDone)
        {
            ClosePopupNow();
            _popupRoot = new GameObject("WinCelebration").transform;
            _popupRoot.SetParent(transform, false);
            _popupRoot.position = uiCamera != null ? uiCamera.transform.position + uiCamera.transform.forward * 4.5f : new Vector3(0, 0, -5f);
            if (whiteSprite != null)
            {
                var sz = whiteSprite.bounds.size;
                var dim = VisualUtil.MakeSprite("Dim", _popupRoot, whiteSprite, Vector3.zero, 0f, 900);
                dim.color = new Color(0f, 0f, 0f, celebrationDim);
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
            Open(UiLayout.Win, UiLayout.BuildWin, v => FillWin(v, body, replay, next));
        }

        void FillWin(UiPopup v, string body, Action replay, Action next)
        {
            SetText(v.bodyText, body);
            Bind(v, "replay", replay);
            Bind(v, "next", next);
            if (winTextSpine != null && v.titleAnchor != null)
            {
                SetText(v.title, "");
                var sg = Spine.Unity.SkeletonGraphic.NewSkeletonGraphicGameObject(winTextSpine, v.titleAnchor, theme.skeletonGraphicMaterial);
                sg.name = "WellDone";
                sg.gameObject.layer = gameObject.layer;
                sg.raycastTarget = false;
                sg.Initialize(false);
                var data = winTextSpine.GetSkeletonData(true);
                if (data != null && data.Width > 0f && sg.AnimationState != null)
                {
                    // SkeletonGraphic: 1 skeleton pixel = 1 canvas unit (reference pixels per unit 100 x scale 0.01).
                    float k = v.titleSpineWidth / data.Width;
                    var rt = sg.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.localScale = Vector3.one * k;
                    rt.sizeDelta = new Vector2(data.Width, data.Height);
                    rt.anchoredPosition = -new Vector2(data.X + data.Width * 0.5f, data.Y + data.Height * 0.5f) * k;
                    sg.AnimationState.SetAnimation(0, "Welldone", false);
                    if (data.FindAnimation("Welldone_Idle") != null) sg.AnimationState.AddAnimation(0, "Welldone_Idle", true, 0f);
                }
            }
            else if (v.title != null && string.IsNullOrEmpty(v.title.text)) v.title.text = "Well Done!";
        }

        /// <summary>Out of moves popup.</summary>
        public void ShowLosePopup(string body, Action retry)
        {
            Open(UiLayout.Lose, UiLayout.BuildLose, v =>
            {
                SetText(v.bodyText, body);
                Bind(v, "retry", retry);
            });
        }

        // ---------------- cheats ----------------

        public void ShowCheats(Action win, Action lose, Action shots, Action boosters, Action level)
        {
            Open(UiLayout.Cheat, UiLayout.BuildCheat, v =>
            {
                Bind(v, "win", win);
                Bind(v, "lose", lose);
                Bind(v, "shots", shots);
                Bind(v, "boosters", boosters);
                Bind(v, "level", level);
                Bind(v, "close", ClosePopup);
            });
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

        UiPopup _promptView;
        TMP_InputField _input;
        Func<string, bool> _check;
        Action<string> _ok;
        string _errorText;

        /// <summary>Password prompt (hidden text).</summary>
        public void ShowPasswordPrompt(Func<string, bool> check, Action onOk) =>
            ShowPrompt(UiLayout.Password, UiLayout.BuildPassword, null, TMP_InputField.ContentType.Password, check, _ => onOk?.Invoke(), "Wrong password");

        /// <summary>Level number prompt (digits only, number pad on phones).</summary>
        public void ShowLevelPrompt(string hint, Func<string, bool> check, Action<string> onOk, string errorText) =>
            ShowPrompt(UiLayout.LevelInput, UiLayout.BuildLevelInput, hint, TMP_InputField.ContentType.IntegerNumber, check, onOk, errorText);

        void ShowPrompt(string popup, Func<UiLayout.Kit, GameObject> build, string hint, TMP_InputField.ContentType type,
            Func<string, bool> check, Action<string> onOk, string errorText)
        {
            Open(popup, build, v => FillPrompt(v, hint, type, check, onOk, errorText));
        }

        void FillPrompt(UiPopup v, string hint, TMP_InputField.ContentType type, Func<string, bool> check, Action<string> onOk, string errorText)
        {
            _promptView = v;
            _check = check;
            _ok = onOk;
            _errorText = errorText;
            if (hint != null) SetText(v.hintText, hint);
            SetText(v.errorText, "");
            _input = v.input;
            if (_input != null)
            {
                _input.contentType = type;
                _input.text = "";
                _input.onSubmit.RemoveAllListeners();
                _input.onSubmit.AddListener(_ => SubmitPrompt());
                _input.onValueChanged.RemoveAllListeners();
                _input.onValueChanged.AddListener(_ => SetText(v.errorText, ""));
                _input.ActivateInputField();
            }
            Bind(v, "ok", SubmitPrompt);
            Bind(v, "cancel", ClosePrompt);
            Bind(v, "close", ClosePrompt);
        }

        void SubmitPrompt()
        {
            if (_promptView == null || _popupRoot != _promptView.transform) return;
            string text = _input != null ? _input.text : "";
            if (_check != null && _check(text))
            {
                var ok = _ok;
                ClosePrompt();
                ok?.Invoke(text);
                return;
            }
            if (_input != null) { _input.text = ""; _input.ActivateInputField(); }
            SetText(_promptView.errorText, _errorText);
        }

        void ClosePrompt()
        {
            _check = null;
            _ok = null;
            if (_input != null) _input.DeactivateInputField();
            _input = null;
            if (_promptView != null && _popupRoot == _promptView.transform) ClosePopup();
            _promptView = null;
        }

        /// <summary>Called from Update (kept for the world HUD: nothing to do with TMP_InputField).</summary>
        void UpdatePassword() { }
    }
}
