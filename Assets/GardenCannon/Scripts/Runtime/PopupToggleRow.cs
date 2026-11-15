using UnityEngine;

namespace GardenCannon
{
    /// <summary>A settings row: label + ON/OFF button.</summary>
    public class PopupToggleRow : MonoBehaviour
    {
        public string id;
        public TextMesh label;
        public PopupButton toggle;
        public Sprite onSprite, offSprite;
        public string onText = "ON", offText = "OFF";

        public void SetOn(bool on)
        {
            if (toggle == null) return;
            if (toggle.art != null && (on ? onSprite : offSprite) != null) toggle.art.sprite = on ? onSprite : offSprite;
            if (toggle.label != null) toggle.label.Text = on ? onText : offText;
        }
    }
}
