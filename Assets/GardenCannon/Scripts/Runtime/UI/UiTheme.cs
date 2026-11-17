using System;
using TMPro;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>TextMeshPro font + materials (Assets/GardenCannon/Fonts/ENG) used by the HUD canvas and the popups.</summary>
    [Serializable]
    public class UiTheme
    {
        public TMP_FontAsset font;
        [Tooltip("Popup titles on the red ribbon, hints on the red body.")]
        public Material titleMaterial;
        [Tooltip("Texts on the cream panels.")]
        public Material bodyMaterial;
        [Tooltip("Labels of the green / yellow / blue / red buttons.")]
        public Material greenText, yellowText, blueText, redText;
        [Tooltip("Moves badge (purple).")]
        public Material purpleText;
        [Tooltip("Settings button art (top right).")]
        public Sprite settingsButton;
        [Tooltip("Spine in the canvas (Well Done title): straight-alpha + CanvasGroup material.")]
        public Material skeletonGraphicMaterial;

        public bool Valid => font != null;
    }

}
