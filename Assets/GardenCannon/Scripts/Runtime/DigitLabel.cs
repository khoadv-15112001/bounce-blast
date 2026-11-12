using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Number drawn with sprites from one digit atlas (Art/Digits.png: white digits with a baked brown outline).
    /// 1-3 SpriteRenderers sharing one texture batch together, far cheaper than TextMesh (+ outline copies).
    /// </summary>
    public class DigitLabel : MonoBehaviour
    {
        /// <summary>Digit sprites 0..9 (set once from GameAssets).</summary>
        public static Sprite[] Digits;
        /// <summary>Material drawn without depth test (GardenCannon/SpriteOverlay), so the bouncing block never hides it.</summary>
        public static Material OverlayMaterial;
        static bool _warned;

        // Visible glyph width of each digit in the 96 px atlas cell (measured when the atlas was generated).
        static readonly float[] GlyphPx = { 83, 59, 71, 73, 78, 72, 76, 76, 78, 76 };
        const float CellPx = 96f, OverlapPx = 12f;

        readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>(3);
        float _height = 0.5f, _maxWidth = 1f;
        int _order = 51;
        int _value = int.MinValue;

        public void Setup(float height, float maxWidth, int sortingOrder)
        {
            _height = height;
            _maxWidth = maxWidth;
            _order = sortingOrder;
            _value = int.MinValue;
        }

        public void SetValue(int value)
        {
            if (value == _value) return;
            if (Digits == null || Digits.Length < 10 || Digits[0] == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogError("[GardenCannon] GameAssets.digits is empty: block numbers can't be drawn. Let Unity recompile (auto-fix) or run Garden Cannon > Update Catalog + Assets + Addressables.");
                }
                return;
            }
            _value = value;

            // Digits without allocating a string.
            int n = 0;
            int v = Mathf.Max(0, value);
            Span3 d = default;
            do { d[n++] = v % 10; v /= 10; } while (v > 0 && n < 3);

            while (_renderers.Count < n)
            {
                var go = new GameObject("Digit");
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = _order;
                if (OverlayMaterial != null) sr.sharedMaterial = OverlayMaterial;
                _renderers.Add(sr);
            }

            var sample = Digits[0];
            float spriteH = sample.bounds.size.y;
            float scale = _height / Mathf.Max(1e-4f, spriteH);
            float unitPerPx = sample.bounds.size.x / CellPx * scale;

            float total = 0f;
            for (int i = n - 1; i >= 0; i--) total += (GlyphPx[d[i]] - OverlapPx) * unitPerPx;
            total += OverlapPx * unitPerPx;
            float fit = total > _maxWidth ? _maxWidth / total : 1f;

            float x = -total * 0.5f * fit;
            int slot = 0;
            for (int i = n - 1; i >= 0; i--, slot++)
            {
                var sr = _renderers[slot];
                sr.enabled = true;
                sr.sprite = Digits[d[i]];
                sr.sortingOrder = _order;
                float w = GlyphPx[d[i]] * unitPerPx * fit;
                sr.transform.localPosition = new Vector3(x + w * 0.5f, 0f, 0f);
                sr.transform.localScale = new Vector3(scale * fit, scale * fit, 1f);
                x += (GlyphPx[d[i]] - OverlapPx) * unitPerPx * fit;
            }
            for (int i = n; i < _renderers.Count; i++) _renderers[i].enabled = false;
        }

        public void Hide()
        {
            _value = int.MinValue;
            foreach (var r in _renderers) r.enabled = false;
        }

        /// <summary>Up to 3 digits on the stack (no array allocation).</summary>
        struct Span3
        {
            int a, b, c;
            public int this[int i]
            {
                get => i == 0 ? a : i == 1 ? b : c;
                set { if (i == 0) a = value; else if (i == 1) b = value; else c = value; }
            }
        }
    }
}
