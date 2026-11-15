using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Root of a popup prefab (Assets/GardenCannon/Prefabs/Popups). Move / resize anything inside by hand: the game only
    /// fills the texts and hooks the buttons by their id.
    /// </summary>
    public class PopupView : MonoBehaviour
    {
        [Tooltip("Popup body (sliced sprite).")]
        public SpriteRenderer body;
        [Tooltip("Title on the ribbon.")]
        public OutlinedLabel title;
        [Tooltip("Where the Spine title (Well Done) is centred.")]
        public Transform titleAnchor;
        [Tooltip("Width of the Spine title.")]
        public float titleSpineWidth = 7.6f;
        [Tooltip("Main text (level complete, moves left...).")]
        public TextMesh bodyText;
        [Tooltip("Prompts: hint above the field.")]
        public TextMesh hintText;
        [Tooltip("Prompts: the typed text.")]
        public TextMesh fieldText;
        [Tooltip("Prompts: error message.")]
        public TextMesh errorText;
        [Tooltip("Pop-in animation when it opens.")]
        public bool punch = true;

        public PopupButton Button(string id)
        {
            foreach (var b in GetComponentsInChildren<PopupButton>(true)) if (b.id == id) return b;
            return null;
        }

        public PopupToggleRow Row(string id)
        {
            foreach (var r in GetComponentsInChildren<PopupToggleRow>(true)) if (r.id == id) return r;
            return null;
        }
    }
}
