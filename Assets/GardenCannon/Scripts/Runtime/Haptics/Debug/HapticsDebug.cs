#if UNITY_EDITOR || DEVELOPMENT_BUILD || SONAT_CHEAT_RELEASE
using Sonat.Game.Haptics.Lab;
using UnityEngine;
using UIToolkitPanelSettings = UnityEngine.UIElements.PanelSettings;
using UnityEngine.UIElements;

namespace GardenCannon
{
    /// <summary>
    /// Device-side haptics tools, opened from Settings in the Editor and in development builds: the package Haptics Lab
    /// (try platform APIs, LIVE-tune each HapticType, edit HapticsConfig) and the Game Haptics Map (turn each game
    /// moment on/off, pick its type / preset / strength). Everything changed there is saved on the device
    /// (<see cref="HapticsLocalOverrides"/>).
    /// </summary>
    public static class HapticsDebug
    {
        /// <summary>True in builds that carry the tools.</summary>
        public const bool Available = true;

        private const string ThemeResource = "HapticsDebugTheme";
        private static UIToolkitPanelSettings s_Panel;
        private static readonly GameHapticsSetupEditor s_MapEditor = new GameHapticsSetupEditor();

        public static void OpenLab() =>
            HapticsLabScreen.Open(Panel(), HapticsLocalOverrides.Save, ResetPackageConfig);

        public static void OpenMap() => GameHapticsSetupScreen.Open(Panel(), s_MapEditor);

        /// <summary>
        /// True when <paramref name="screenPos"/> (Input.mousePosition) is on the Lab or the map screen, so the game
        /// ignores that touch. The collapsed Lab bar only covers its own corner, so play goes on around it.
        /// </summary>
        public static bool BlocksPointer(Vector2 screenPos)
        {
            if (HapticsLabScreen.Current == null && !GameHapticsSetupScreen.IsOpen) return false;
            foreach (var doc in Object.FindObjectsOfType<UIDocument>())
            {
                var root = doc.rootVisualElement;
                var panel = root?.panel;
                if (panel == null) continue;
                var point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
                var picked = panel.Pick(point);
                if (picked != null && picked != root) return true;
            }
            return false;
        }

        /// <summary>Restores the shipped HapticsConfig and GameHapticsMap and forgets the device tuning.</summary>
        public static void ResetAll()
        {
            HapticsLocalOverrides.ResetToDefaults();
            HapticsLabScreen.ResetLiveTuneAfterExternalConfigReset();
        }

        private static void ResetPackageConfig()
        {
            HapticsLocalOverrides.ResetConfigToDefaults();
            HapticsLabScreen.ResetLiveTuneAfterExternalConfigReset();
        }

        /// <summary>The panel the screens copy: default runtime theme, drawn above the game.</summary>
        private static UIToolkitPanelSettings Panel()
        {
            if (s_Panel != null) return s_Panel;
            s_Panel = ScriptableObject.CreateInstance<UIToolkitPanelSettings>();
            s_Panel.name = "HapticsDebug PanelSettings";
            s_Panel.hideFlags = HideFlags.DontSave;
            s_Panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemeResource);
            if (s_Panel.themeStyleSheet == null)
                Debug.LogWarning($"[Haptics] Resources/{ThemeResource}.tss is missing; the haptics screens have no default theme.");
            s_Panel.sortingOrder = 30000;
            return s_Panel;
        }
    }
}
#else
namespace GardenCannon
{
    public static class HapticsDebug
    {
        public const bool Available = false;
        public static void OpenLab() { }
        public static void OpenMap() { }
        public static void ResetAll() { }
        public static bool BlocksPointer(UnityEngine.Vector2 screenPos) => false;
    }
}
#endif
