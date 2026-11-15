using TMPro;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>Settings row: label + ON/OFF button (art and text change with the state).</summary>
    public class UiToggleRow : MonoBehaviour
    {
        public string id;
        public TMP_Text label;
        public UiButton toggle;
        public Sprite onSprite, offSprite;
        public Material onTextMaterial, offTextMaterial;
        public string onText = "ON", offText = "OFF";

        public void SetOn(bool on)
        {
            if (toggle == null) return;
            var s = on ? onSprite : offSprite;
            if (toggle.art != null && s != null) toggle.art.sprite = s;
            if (toggle.label != null)
            {
                toggle.label.text = on ? onText : offText;
                var m = on ? onTextMaterial : offTextMaterial;
                if (m != null) toggle.label.fontSharedMaterial = m;
            }
        }
    }
}
