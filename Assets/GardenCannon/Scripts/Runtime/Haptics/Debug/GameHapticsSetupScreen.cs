#if UNITY_EDITOR || DEVELOPMENT_BUILD || SONAT_CHEAT_RELEASE
using System;
using Sonat.Game.Haptics;
using Sonat.Game.Haptics.Lab;
using UnityEngine;
using UnityEngine.UIElements;

namespace GardenCannon
{
    [RequireComponent(typeof(UIDocument))]
    internal sealed class GameHapticsSetupScreen : MonoBehaviour
    {
        private static readonly Color k_Background = new(0.086f, 0.094f, 0.106f, 0.98f);
        private static readonly Color k_Text = new(0.9f, 0.9f, 0.9f);
        private static readonly Color k_Border = new(0.23f, 0.24f, 0.27f);

        private static GameHapticsSetupScreen s_Current;
        public static bool IsOpen => s_Current != null;
        private PanelSettings m_Settings;
        private Vector2Int m_ScreenSize;
        private GameHapticsSetupEditor m_Editor;
        private HapticsLabPlayer m_PreviewPlayer;

        public static GameHapticsSetupScreen Open(PanelSettings source, GameHapticsSetupEditor editor)
        {
            if (s_Current != null)
            {
                s_Current.m_Editor = editor;
                s_Current.Build(s_Current.GetComponent<UIDocument>().rootVisualElement);
                return s_Current;
            }

            var host = new GameObject("GameHapticsMapSetup");
            DontDestroyOnLoad(host);
            var settings = Instantiate(source);
            settings.name = "Game Haptics Map PanelSettings";
            settings.hideFlags = HideFlags.DontSave;
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = ScaleFor(Screen.width, Screen.height, Screen.dpi);
            host.AddComponent<UIDocument>().panelSettings = settings;
            var screen = host.AddComponent<GameHapticsSetupScreen>();
            screen.m_Settings = settings;
            screen.m_ScreenSize = new Vector2Int(Screen.width, Screen.height);
            screen.m_Editor = editor;
            screen.m_PreviewPlayer = new HapticsLabPlayer();
            screen.Build(host.GetComponent<UIDocument>().rootVisualElement);
            return screen;
        }

        private void Update()
        {
            if (m_Settings == null || (Screen.width == m_ScreenSize.x && Screen.height == m_ScreenSize.y)) return;
            m_ScreenSize = new Vector2Int(Screen.width, Screen.height);
            m_Settings.scale = ScaleFor(Screen.width, Screen.height, Screen.dpi);
        }

        private void OnDestroy()
        {
            if (s_Current == this) s_Current = null;
            m_PreviewPlayer?.Dispose();
            if (m_Settings != null) Destroy(m_Settings);
        }

        private void Build(VisualElement root)
        {
            s_Current = this;
            root.Clear();
            var page = new VisualElement();
            page.style.position = Position.Absolute;
            page.style.left = page.style.right = page.style.top = page.style.bottom = 0;
            page.style.backgroundColor = k_Background;
            page.style.paddingLeft = page.style.paddingRight = 10;

            var safeArea = Screen.safeArea;
            var scale = Screen.height > 0 ? root.resolvedStyle.height / Screen.height : 1f;
            page.style.paddingTop = Mathf.Max(0, Screen.height - safeArea.yMax) * scale;
            page.style.paddingBottom = safeArea.yMin * scale;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = 38;
            var title = new Label("Game Haptics Map");
            title.style.flexGrow = 1;
            title.style.color = k_Text;
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);
            header.Add(ActionButton("Đóng", () => Destroy(gameObject)));
            page.Add(header);

            var intro = new Label("Cấu hình các moment haptics riêng của Garden Cannon. Map tự lưu trên thiết bị; config package nằm trong Haptics Lab.");
            intro.style.color = new Color(0.65f, 0.68f, 0.72f);
            intro.style.whiteSpace = WhiteSpace.Normal;
            intro.style.marginBottom = 6;
            page.Add(intro);

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.Add(ActionButton("Thử moment đang chọn", () => m_Editor.PlaySelected()));
            page.Add(actions);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            StyleScroller(scroll.verticalScroller);
            scroll.Add(m_Editor.CreateView(
                (tuning, intensity) => m_PreviewPlayer.PlaySoundPreview(tuning, intensity, HapticView.AndroidComposition),
                HapticsLocalOverrides.Save,
                HapticsLocalOverrides.ResetMapToDefaults));
            page.Add(scroll);
            root.Add(page);
            root.RegisterCallback<GeometryChangedEvent>(_ => FitSafeArea(root, page));
        }

        private static void StyleScroller(Scroller scroller)
        {
            scroller.style.width = 8;
            scroller.style.marginLeft = 4;
            scroller.style.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 0.75f);
            scroller.style.borderLeftWidth = 0;
            scroller.lowButton.style.display = DisplayStyle.None;
            scroller.highButton.style.display = DisplayStyle.None;

            var slider = scroller.slider;
            slider.style.marginTop = slider.style.marginBottom = 0;
            slider.style.marginLeft = slider.style.marginRight = 0;
            slider.style.top = slider.style.bottom = 0;

            var tracker = slider.Q(className: BaseSlider<float>.trackerUssClassName);
            if (tracker != null)
            {
                tracker.style.backgroundColor = Color.clear;
                SetBorder(tracker, Color.clear, 0);
            }

            var thumb = slider.Q(className: BaseSlider<float>.draggerUssClassName);
            if (thumb != null)
            {
                thumb.style.width = 4;
                thumb.style.left = 2;
                thumb.style.backgroundColor = new Color(0.38f, 0.52f, 0.68f, 0.8f);
                SetBorder(thumb, Color.clear, 2);
            }
        }

        private static void FitSafeArea(VisualElement root, VisualElement page)
        {
            if (Screen.height <= 0 || root.layout.height <= 0) return;
            var scale = root.layout.height / Screen.height;
            var safe = Screen.safeArea;
            page.style.paddingTop = (Screen.height - safe.yMax) * scale;
            page.style.paddingBottom = safe.yMin * scale;
        }

        private static Button ActionButton(string text, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.style.height = 28;
            button.style.marginLeft = 3;
            button.style.marginRight = 3;
            button.style.marginTop = 2;
            button.style.marginBottom = 2;
            button.style.paddingLeft = button.style.paddingRight = 8;
            button.style.fontSize = 11;
            button.style.color = k_Text;
            button.style.backgroundColor = new Color(0.165f, 0.176f, 0.2f);
            button.style.borderTopWidth = button.style.borderBottomWidth =
                button.style.borderLeftWidth = button.style.borderRightWidth = 1;
            button.style.borderTopColor = button.style.borderBottomColor =
                button.style.borderLeftColor = button.style.borderRightColor = k_Border;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius =
                button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 6;
            return button;
        }

        private static void SetBorder(VisualElement element, Color color, float radius)
        {
            element.style.borderTopWidth = element.style.borderBottomWidth =
                element.style.borderLeftWidth = element.style.borderRightWidth = 1;
            element.style.borderTopColor = element.style.borderBottomColor =
                element.style.borderLeftColor = element.style.borderRightColor = color;
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius =
                element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = radius;
        }

        private static float ScaleFor(float width, float height, float dpi)
        {
            if (width <= 0f || height <= 0f) return 1f;
            var scale = Mathf.Min(width / 390f, height / 700f);
            if (dpi >= 100f && dpi <= 800f) scale = Mathf.Min(scale, dpi / 160f);
            return Mathf.Max(0.5f, scale);
        }
    }
}
#endif
