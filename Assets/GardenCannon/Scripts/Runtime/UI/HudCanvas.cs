using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GardenCannon
{
    /// <summary>
    /// HUD canvas (Assets/GardenCannon/Prefabs/UI/HudCanvas): moves badge, settings button, booster buttons and the popup
    /// layer. Edit positions / sizes in the prefab; the game fills the numbers and hooks the buttons.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class HudCanvas : MonoBehaviour
    {
        public Canvas canvas;
        [Tooltip("Stretched over the device safe area.")]
        public RectTransform safeArea;
        [Header("Moves badge")]
        public RectTransform movesBadge;
        public TMP_Text movesTitle;
        public TMP_Text movesText;
        [Header("Settings")]
        public UiButton settingsButton;
        [Header("Boosters")]
        [Tooltip("Follows the cannon on screen (set by the game). The buttons are placed relative to it.")]
        public RectTransform boosters;
        [Header("Popups")]
        [Tooltip("Popups are opened under this (keep it the last child so they cover the HUD).")]
        public RectTransform popupLayer;

        public RectTransform Rect => (RectTransform)transform;
    }
}
