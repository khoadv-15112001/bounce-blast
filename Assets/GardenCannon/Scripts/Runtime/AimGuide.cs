using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>Dotted trajectory with bigger markers at bounce points (like the original aim guide).</summary>
    public class AimGuide : MonoBehaviour
    {
        public Sprite dotSprite;
        [Tooltip("Dot diameter in cells.")] public float dotSize = 0.2f;
        [Tooltip("Bounce marker diameter in cells.")] public float bounceSize = 0.38f;
        [Tooltip("Distance between dots in cells.")] public float spacing = 0.45f;
        [Tooltip("Max path length in cells.")] public float maxLength = 32f;
        public int maxBounces = 3;
        public Color color = Color.white;
        public int sortingOrder = 40;

        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        readonly List<Vector2> _dots = new List<Vector2>();
        readonly List<Vector2> _bounces = new List<Vector2>();

        public void Show(BallSimulator sim, Vector2 start, Vector2 dir)
        {
            float cell = sim.Board != null ? sim.Board.CellSize : 1f;
            sim.Predict(start, dir, maxBounces, maxLength * cell, spacing * cell, _dots, _bounces);
            int used = 0;
            float clearR = bounceSize * cell * 0.6f;
            for (int i = 0; i < _dots.Count; i++)
            {
                bool nearBounce = false;
                foreach (var b in _bounces)
                    if ((b - _dots[i]).sqrMagnitude < clearR * clearR) { nearBounce = true; break; }
                if (nearBounce) continue;
                float fade = Mathf.Lerp(1f, 0.45f, i / (float)Mathf.Max(1, _dots.Count));
                Place(used++, _dots[i], dotSize * cell, fade);
            }
            foreach (var b in _bounces) Place(used++, b, bounceSize * cell, 0.95f);
            for (int i = used; i < _pool.Count; i++) _pool[i].enabled = false;
        }

        public void Hide()
        {
            foreach (var sr in _pool) sr.enabled = false;
        }

        void Place(int index, Vector2 pos, float size, float alpha)
        {
            while (_pool.Count <= index)
            {
                var sr = VisualUtil.MakeSprite("AimDot", transform, dotSprite, Vector3.zero, 0f, sortingOrder);
                _pool.Add(sr);
            }
            var s = _pool[index];
            s.enabled = true;
            s.transform.position = new Vector3(pos.x, pos.y, -0.25f);
            float w = dotSprite != null ? dotSprite.bounds.size.x : 1f;
            s.transform.localScale = Vector3.one * (size / Mathf.Max(0.0001f, w));
            var c = color;
            c.a *= alpha;
            s.color = c;
        }
    }
}
