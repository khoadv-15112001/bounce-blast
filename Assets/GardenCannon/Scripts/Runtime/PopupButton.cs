using UnityEngine;

namespace GardenCannon
{
    /// <summary>A tappable button of a popup prefab. <see cref="id"/> tells the game what it does.</summary>
    public class PopupButton : MonoBehaviour
    {
        [Tooltip("What the button does (set by the game): e.g. replay, next, close, ok, cancel, win, lose...")]
        public string id;
        public SpriteRenderer art;
        public OutlinedLabel label;
        [Tooltip("Tap area in local units. Zero = the size of the art.")]
        public Vector2 hitSize;

        /// <summary>Tap area in this transform's local units.</summary>
        public Vector2 HitSize
        {
            get
            {
                if (hitSize.x > 0f && hitSize.y > 0f) return hitSize;
                if (art == null || art.sprite == null) return new Vector2(2f, 1.2f);
                Vector2 s = art.drawMode != SpriteDrawMode.Simple ? art.size : (Vector2)art.sprite.bounds.size;
                var t = art.transform;
                return new Vector2(s.x * Mathf.Abs(t.localScale.x), s.y * Mathf.Abs(t.localScale.y));
            }
        }
    }
}
