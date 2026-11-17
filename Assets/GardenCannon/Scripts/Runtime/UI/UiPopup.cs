using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GardenCannon
{
    /// <summary>
    /// Root of a canvas popup prefab (Assets/GardenCannon/Prefabs/UI/Popups). Move / resize anything inside by hand:
    /// the game only fills the texts and hooks the buttons by their id.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UiPopup : MonoBehaviour
    {
        [Tooltip("Full-screen dark backdrop (also blocks taps on the game).")]
        public Image dim;
        [Tooltip("Everything but the backdrop: scaled by the open / close animation.")]
        public RectTransform panel;
        public Image body;
        public TMP_Text title;
        [Tooltip("Where the Spine title (Well Done) is centred.")]
        public RectTransform titleAnchor;
        [Tooltip("Width of the Spine title (canvas units).")]
        public float titleSpineWidth = 930f;
        [Tooltip("Main text (level complete, moves left...).")]
        public TMP_Text bodyText;
        [Tooltip("Prompts: hint above the field.")]
        public TMP_Text hintText;
        [Tooltip("Prompts: the text field.")]
        public TMP_InputField input;
        [Tooltip("Prompts: error message.")]
        public TMP_Text errorText;
        [Tooltip("Pop-in animation when it opens.")]
        public bool punch = true;

        CanvasGroup _group;
        public CanvasGroup Group => _group != null ? _group : (_group = GetComponent<CanvasGroup>());

        public UiButton Button(string id)
        {
            foreach (var b in GetComponentsInChildren<UiButton>(true)) if (b.id == id) return b;
            return null;
        }

        public UiToggleRow Row(string id)
        {
            foreach (var r in GetComponentsInChildren<UiToggleRow>(true)) if (r.id == id) return r;
            return null;
        }
    }
}
