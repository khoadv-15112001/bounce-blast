using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GardenCannon
{
    /// <summary>One booster button on the HUD canvas (plate, icon, selection glow, count bubble).</summary>
    public class UiBoosterButton : MonoBehaviour
    {
        public BoosterId booster;
        public UiButton button;
        public Image plate, icon, glow, bubble;
        public TMP_Text count, label;
    }
}
