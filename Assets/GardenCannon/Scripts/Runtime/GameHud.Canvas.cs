using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GardenCannon
{
    /// <summary>HUD canvas: moves badge, settings button, boosters and the popup layer (uGUI + TextMeshPro).</summary>
    public partial class GameHud
    {
        [Header("HUD canvas (set at runtime from GameAssets)")]
        [Tooltip("Assets/GardenCannon/Prefabs/UI/HudCanvas: edit positions / sizes there.")]
        public GameObject hudCanvasPrefab;
        public UiTheme theme = new UiTheme();
        [Tooltip("Used when the HUD canvas has to be built without its prefab.")]
        public Sprite boosterPlateSprite, boosterBubbleSprite;
        public Func<BoosterId, Sprite> boosterIcon;

        public HudCanvas Canvas { get; private set; }

        UiLayout.Kit UiKit() => new UiLayout.Kit
        {
            theme = theme, skin = skin, white = whiteSprite, badge = movesBadgeSprite,
            boosterPlate = boosterPlateSprite, boosterBubble = boosterBubbleSprite, boosterIcon = boosterIcon,
            bodyColor = popupTextColor,
        };

        /// <summary>Create the HUD canvas (from its prefab, or the default layout) and an EventSystem.</summary>
        public HudCanvas EnsureCanvas()
        {
            if (Canvas != null) return Canvas;
            var go = hudCanvasPrefab != null ? Instantiate(hudCanvasPrefab) : UiLayout.BuildHud(UiKit());
            go.name = "HudCanvas";
            Canvas = go.GetComponent<HudCanvas>();
            if (Canvas == null) { Destroy(go); return null; }
            var c = Canvas.canvas != null ? Canvas.canvas : go.GetComponent<UnityEngine.Canvas>();
            Canvas.canvas = c;
            c.renderMode = RenderMode.ScreenSpaceCamera;
            if (uiCamera != null) c.worldCamera = uiCamera;
            c.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            // Drawn by the UI camera.
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
            if (EventSystem.current == null && FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(null, false);
            }
            UiButton.AnyClicked = () => ButtonClicked?.Invoke();
            PreloadPopups();
            return Canvas;
        }

        /// <summary>Canvas HUD instead of the world settings button / moves badge.</summary>
        void InitCanvasHud(Action openSettings)
        {
            if (Canvas.settingsButton != null) Canvas.settingsButton.onClick = openSettings;
            if (Canvas.movesTitle != null) Canvas.movesTitle.text = movesBadgeTitle;
            // The scene's world settings button is replaced by the canvas one.
            if (settingsAnchor != null) settingsAnchor.gameObject.SetActive(false);
        }

        /// <summary>True when the pointer / a touch is over a canvas element (the game ignores that tap).</summary>
        public static bool PointerOverUi()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (es.IsPointerOverGameObject()) return true;
            for (int i = 0; i < Input.touchCount; i++)
                if (es.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;
            return false;
        }
    }
}
