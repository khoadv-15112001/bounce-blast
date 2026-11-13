using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Row of balls waiting on the right of the cannon (one per ball left to shoot, up to <see cref="maxVisible"/>).
    /// On each shot the front ball rolls into the cannon, the others roll one slot closer and a new one rolls in at the
    /// back while balls remain.
    /// </summary>
    public class AmmoBelt : MonoBehaviour
    {
        [Tooltip("Ball model (same as the shot balls).")]
        public GameObject ballPrefab;
        [Tooltip("Cannon the balls roll into.")]
        public Transform cannon;
        [Tooltip("First slot relative to the cannon pivot (world units).")]
        public Vector3 firstSlot = new Vector3(2.35f, -0.17f, -0.35f);
        [Tooltip("Distance between two balls.")]
        public float spacing = 0.66f;
        [Tooltip("Ball diameter (world units).")]
        public float ballSize = 0.58f;
        public int maxVisible = 15;
        [Tooltip("Seconds for the front ball to roll into the cannon.")]
        public float loadTime = 0.12f;
        [Tooltip("Seconds for the row to roll one slot.")]
        public float shiftTime = 0.14f;

        class Slot
        {
            public Transform t;
            public float x;          // current offset along the belt (slot units, 0 = front)
            public float from, to;   // tween
            public float timer, duration;
            public float scale = 1f, scaleFrom, scaleTo = 1f;
            public bool loading;     // rolling into the cannon
            public float delay;
        }

        readonly List<Slot> _row = new List<Slot>();
        readonly List<Slot> _loading = new List<Slot>();
        readonly Stack<Transform> _pool = new Stack<Transform>();
        float _meshSize = -1f;
        int _queued;   // balls not shown yet (balls left beyond the visible ones)

        Vector3 Pivot => cannon != null ? cannon.position : new Vector3(0f, -10.54f, 0f);
        Vector3 SlotPos(float x) => Pivot + firstSlot + Vector3.right * (spacing * x);

        /// <summary>Level start: show <paramref name="balls"/> waiting balls (up to maxVisible), popping in one after the other.</summary>
        public void Fill(int balls)
        {
            foreach (var s in _row) Release(s.t);
            foreach (var s in _loading) Release(s.t);
            _row.Clear();
            _loading.Clear();
            int shown = Mathf.Clamp(balls, 0, maxVisible);
            _queued = Mathf.Max(0, balls - shown);
            for (int i = 0; i < shown; i++)
            {
                var s = NewSlot(i);
                s.scale = 0f; s.scaleFrom = 0f; s.scaleTo = 1f;
                s.timer = 0f; s.duration = 0.25f; s.delay = 0.03f * i;
                _row.Add(s);
            }
        }

        /// <summary>A shot was fired: <paramref name="ballsLeft"/> = balls still to shoot after this one.</summary>
        public void Feed(int ballsLeft)
        {
            if (_row.Count > 0)
            {
                var front = _row[0];
                _row.RemoveAt(0);
                front.loading = true;
                front.timer = 0f; front.delay = 0f; front.duration = loadTime;
                front.scaleFrom = front.scale; front.scaleTo = 0.2f;
                _loading.Add(front);
            }
            for (int i = 0; i < _row.Count; i++)
                Tween(_row[i], i, shiftTime, 0.012f * i);
            // A new ball rolls in at the back while more balls are left than are visible.
            int wanted = Mathf.Clamp(ballsLeft, 0, maxVisible);
            if (_row.Count < wanted && _queued > 0)
            {
                _queued--;
                var s = NewSlot(_row.Count + 1);
                s.scale = 0f; s.scaleFrom = 0f; s.scaleTo = 1f;
                Tween(s, _row.Count, shiftTime, 0.012f * _row.Count);
                _row.Add(s);
            }
        }

        void Tween(Slot s, float target, float duration, float delay)
        {
            s.from = s.x; s.to = target;
            s.timer = 0f; s.duration = duration; s.delay = delay;
            s.scaleFrom = s.scale;
        }

        Slot NewSlot(float x)
        {
            var s = new Slot { t = Acquire(), x = x, from = x, to = x };
            Place(s);
            return s;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _row.Count; i++) Step(_row[i], dt);
            for (int i = _loading.Count - 1; i >= 0; i--)
            {
                var s = _loading[i];
                s.timer += dt;
                float k = Mathf.Clamp01(s.timer / Mathf.Max(0.01f, s.duration));
                // Roll from the front slot into the cannon mouth (a little hop on the way).
                Vector3 a = SlotPos(0f), b = Pivot + new Vector3(0f, 0.2f, firstSlot.z);
                Vector3 p = Vector3.Lerp(a, b, k * k);
                p.y += Mathf.Sin(k * Mathf.PI) * 0.15f;
                s.t.position = p;
                s.t.localScale = Vector3.one * (Scale() * Mathf.Lerp(s.scaleFrom, s.scaleTo, k));
                s.t.rotation = Quaternion.AngleAxis(-dt * 900f, Vector3.up) * s.t.rotation;
                if (k >= 1f) { Release(s.t); _loading.RemoveAt(i); }
            }
        }

        void Step(Slot s, float dt)
        {
            if (s.delay > 0f) { s.delay -= dt; Place(s); return; }
            if (s.timer < s.duration)
            {
                s.timer += dt;
                float k = Mathf.Clamp01(s.timer / Mathf.Max(0.01f, s.duration));
                float e = 1f - (1f - k) * (1f - k);
                float prev = s.x;
                s.x = Mathf.Lerp(s.from, s.to, e);
                s.scale = Mathf.Lerp(s.scaleFrom, s.scaleTo, e);
                // Rolling: turn by the travelled distance.
                float rolled = (s.x - prev) * spacing / Mathf.Max(0.01f, ballSize * 0.5f) * Mathf.Rad2Deg;
                s.t.rotation = Quaternion.AngleAxis(-rolled, Vector3.up) * s.t.rotation;
            }
            Place(s);
        }

        void Place(Slot s)
        {
            s.t.position = SlotPos(s.x);
            s.t.localScale = Vector3.one * (Scale() * s.scale);
        }

        float Scale()
        {
            if (_meshSize < 0f)
            {
                _meshSize = 1f;
                if (ballPrefab != null)
                {
                    float size = 0f;
                    foreach (var mf in ballPrefab.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.sharedMesh != null) { var b = mf.sharedMesh.bounds.size; size = Mathf.Max(size, b.x, b.y, b.z); }
                    if (size > 1e-4f) _meshSize = size;
                }
            }
            return ballSize / _meshSize;
        }

        Transform Acquire()
        {
            Transform t = _pool.Count > 0 ? _pool.Pop() : null;
            if (t == null)
            {
                if (ballPrefab == null) return new GameObject("AmmoBall").transform;
                var go = Instantiate(ballPrefab, transform);
                go.name = "AmmoBall";
                VisualUtil.StripRuntimeComponents(go);
                foreach (var tr in go.GetComponentsInChildren<TrailRenderer>(true)) tr.enabled = false;
                t = go.transform;
            }
            t.gameObject.SetActive(true);
            t.rotation = Quaternion.identity;
            return t;
        }

        void Release(Transform t)
        {
            if (t == null) return;
            t.gameObject.SetActive(false);
            _pool.Push(t);
        }
    }
}
