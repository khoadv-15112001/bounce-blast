using UnityEngine;

namespace GardenCannon
{
    /// <summary>Text with a soft outline: the main TextMesh plus the offset copies behind it (all get the same text).</summary>
    public class OutlinedLabel : MonoBehaviour
    {
        public TextMesh main;
        public TextMesh[] outline = new TextMesh[0];

        public string Text
        {
            get => main != null ? main.text : "";
            set
            {
                if (main != null) main.text = value;
                if (outline != null) foreach (var o in outline) if (o != null) o.text = value;
            }
        }
    }
}
