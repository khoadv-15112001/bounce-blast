using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>A level item on the board: physics shape, HP, lane movement and its visual.</summary>
    public class BoardItem : MonoBehaviour
    {
        public ItemData Data { get; private set; }
        public ItemCatalog.Entry Entry { get; private set; }
        public ItemKind Kind => Entry != null ? Entry.kind : ItemKind.Indestructible;
        public int Hits { get; private set; }
        public int MaxHits { get; private set; }
        public bool Alive { get; private set; } = true;
        public bool IsMoving => _lane;
        public bool IsSolid => Alive && Kind != ItemKind.PassThrough && Kind != ItemKind.Ignore;
        public bool IsBreakable => Kind == ItemKind.Breakable || Kind == ItemKind.PassThrough;

        Board _board;
        readonly List<Vector2Int> _offsets = new List<Vector2Int>(); // relative to anchor
        Vector2 _anchor;            // grid coords (float while moving)
        bool _singleRect;
        readonly List<Rect> _rects = new List<Rect>();
        Rect _bounds;               // world
        Vector2[] _wedge;           // world, CCW
        Vector2 _circleCentre;
        float _circleRadius;

        bool _lane, _laneVertical;
        float _laneLo, _laneHi, _laneDir = 1f;

        Transform _visual;
        Vector3 _visualBaseScale;
        Vector3 _visualOffset;
        TextMesh _hitText;
        float _punch;
        float _flash;

        public Rect Bounds => _bounds;

        public void Init(Board board, ItemData data, ItemCatalog.Entry entry, ItemCatalog catalog, Font font)
        {
            _board = board;
            Data = data;
            Entry = entry;
            _anchor = new Vector2(data.x, data.y);

            _offsets.Clear();
            var cells = data.Cells();
            foreach (var c in cells) _offsets.Add(c - cells[0]);
            _singleRect = IsFullRectangle(_offsets);

            int damage = Mathf.Max(1, catalog.damagePerHit);
            MaxHits = data.hp > 0 ? Mathf.Max(1, Mathf.CeilToInt(data.hp / (float)damage)) : Mathf.Max(1, entry != null ? entry.defaultHits : 1);
            if (data.layers > 1 && data.hp <= 0) MaxHits *= data.layers;
            Hits = MaxHits;

            _lane = data.hasLane && data.laneMax > data.laneMin;
            if (_lane)
            {
                _laneVertical = data.variant == 1;
                int length = LengthAlongAxis(_laneVertical);
                _laneLo = data.laneMin;
                _laneHi = Mathf.Max(_laneLo, data.laneMax - (length - 1));
                float start = data.lanePos;
                if (_laneVertical) _anchor.y = Mathf.Clamp(start, _laneLo, _laneHi);
                else _anchor.x = Mathf.Clamp(start, _laneLo, _laneHi);
            }

            BuildVisual(catalog, font);
            RebuildShape();
        }

        static bool IsFullRectangle(List<Vector2Int> cells)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            var set = new HashSet<Vector2Int>();
            foreach (var c in cells)
            {
                set.Add(c);
                minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
            }
            return set.Count == (maxX - minX + 1) * (maxY - minY + 1);
        }

        int LengthAlongAxis(bool vertical)
        {
            int min = int.MaxValue, max = int.MinValue;
            foreach (var o in _offsets)
            {
                int v = vertical ? o.y : o.x;
                min = Mathf.Min(min, v); max = Mathf.Max(max, v);
            }
            return max - min + 1;
        }

        void BuildVisual(ItemCatalog catalog, Font font)
        {
            var e = Entry;
            if (e == null || e.prefab == null) return;
            var go = Instantiate(e.prefab, transform);
            go.name = e.prefab.name;
            VisualUtil.StripRuntimeComponents(go);
            _visual = go.transform;

            float cell = _board.CellSize;
            float rot = e.rotationOffset + (e.rotateByVariant ? Data.variant * 90f : 0f);
            _visual.localRotation = Quaternion.Euler(0f, 0f, rot) * _visual.localRotation;
            _visualBaseScale = Vector3.Scale(_visual.localScale, Vector3.one * (cell * Mathf.Max(0.01f, e.visualScale)));
            _visual.localScale = _visualBaseScale;

            // Centre the visual on the footprint bounds.
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var o in _offsets) { min = Vector2.Min(min, o); max = Vector2.Max(max, o); }
            Vector2 centre = (min + max) * 0.5f;
            _visualOffset = new Vector3(centre.x * cell, centre.y * cell, 0f);
            _visual.localPosition = _visualOffset;

            if (_lane || e.label.Contains("Barrier")) VisualUtil.ApplyOrientation(_visual, _laneVertical);
            if (e.colorByVariant) VisualUtil.ApplyAtlasTile(_visual, catalog, catalog.AtlasTileForVariant(Data.variant));

            if (e.fitFootprint)
            {
                // Bounds are measured with the item parked at its anchor so they are in a known frame.
                transform.position = Vector3.zero;
                if (TryGetMeshBounds(go, out var b) && b.size.x > 1e-4f && b.size.y > 1e-4f)
                {
                    Vector2 foot = (max - min + Vector2.one) * cell * e.fitFill;
                    float k = Mathf.Min(foot.x / b.size.x, foot.y / b.size.y);
                    _visualBaseScale *= k;
                    _visual.localScale = _visualBaseScale;
                    TryGetMeshBounds(go, out b);
                    Vector3 wantCentre = _visualOffset;
                    _visual.localPosition += new Vector3(wantCentre.x - b.center.x, wantCentre.y - b.center.y, 0f);
                    _visualOffset = new Vector3(_visual.localPosition.x, _visual.localPosition.y, _visualOffset.z);
                }
            }

            if (e.showHitCount)
            {
                _hitText = VisualUtil.MakeText("Hits", transform, font, _visualOffset + new Vector3(0f, 0f, -3f), cell * 0.8f, Color.white, TextAnchor.MiddleCenter, 50);
                _hitText.text = Hits.ToString();
            }
        }

        static bool TryGetMeshBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (r.GetComponent<TextMesh>() != null) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (any) return true;
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        /// <summary>World position of the anchor cell's bottom-left corner.</summary>
        Vector2 AnchorWorld() => _board.GridToWorldCorner(_anchor);

        void RebuildShape()
        {
            float cell = _board.CellSize;
            Vector2 a = AnchorWorld();
            transform.position = new Vector3(a.x + cell * 0.5f, a.y + cell * 0.5f, 0f);

            _rects.Clear();
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var o in _offsets)
            {
                var r = new Rect(a.x + o.x * cell, a.y + o.y * cell, cell, cell);
                if (!_singleRect) _rects.Add(r);
                minX = Mathf.Min(minX, r.xMin); minY = Mathf.Min(minY, r.yMin);
                maxX = Mathf.Max(maxX, r.xMax); maxY = Mathf.Max(maxY, r.yMax);
            }
            _bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            if (_singleRect) _rects.Add(_bounds);

            _circleCentre = _bounds.center;
            _circleRadius = Mathf.Min(_bounds.width, _bounds.height) * 0.42f;

            if (Kind == ItemKind.Wedge)
            {
                Vector2 bl = new Vector2(_bounds.xMin, _bounds.yMin), br = new Vector2(_bounds.xMax, _bounds.yMin);
                Vector2 tr = new Vector2(_bounds.xMax, _bounds.yMax), tl = new Vector2(_bounds.xMin, _bounds.yMax);
                switch (((Data.variant % 4) + 4) % 4)
                {
                    case 0: _wedge = new[] { bl, br, tl }; break; // right angle bottom-left
                    case 1: _wedge = new[] { bl, br, tr }; break; // bottom-right
                    case 2: _wedge = new[] { br, tr, tl }; break; // top-right
                    default: _wedge = new[] { bl, tr, tl }; break; // top-left
                }
            }
        }

        public void Tick(float dt)
        {
            if (!Alive) return;
            if (_lane)
            {
                float speed = Entry != null ? Entry.laneSpeed : 1.5f;
                float v = (_laneVertical ? _anchor.y : _anchor.x) + _laneDir * speed * dt;
                if (v >= _laneHi) { v = _laneHi; _laneDir = -1f; }
                else if (v <= _laneLo) { v = _laneLo; _laneDir = 1f; }
                if (_laneVertical) _anchor.y = v; else _anchor.x = v;
                RebuildShape();
            }
            if (_visual != null)
            {
                _punch = Mathf.Max(0f, _punch - dt * 6f);
                float s = 1f + Mathf.Sin(_punch * Mathf.PI) * 0.12f;
                _visual.localScale = _visualBaseScale * s;
            }
            _flash = Mathf.Max(0f, _flash - dt);
        }

        /// <summary>Circle test against this item. Returns the deepest contact.</summary>
        public bool Collide(Vector2 p, float r, out Vector2 normal, out float pen)
        {
            normal = Vector2.zero;
            pen = 0f;
            if (!Alive || Kind == ItemKind.Ignore) return false;
            // Cheap reject.
            if (p.x + r < _bounds.xMin || p.x - r > _bounds.xMax || p.y + r < _bounds.yMin || p.y - r > _bounds.yMax) return false;

            if (Kind == ItemKind.Wedge && _wedge != null)
                return Collision2D.CirclePolygon(p, r, _wedge, out normal, out pen);
            if (Entry != null && Entry.shape == ItemShape.Circle || Kind == ItemKind.Bumper)
                return Collision2D.CircleCircle(p, r, _circleCentre, _circleRadius, out normal, out pen);

            bool hit = false;
            foreach (var rect in _rects)
            {
                if (Collision2D.CircleRect(p, r, rect, out var n, out var d) && d > pen)
                {
                    hit = true; normal = n; pen = d;
                }
            }
            return hit;
        }

        /// <summary>Apply one ball hit. Returns true when the item breaks.</summary>
        public bool Hit()
        {
            if (!Alive) return false;
            _punch = 1f;
            if (!IsBreakable) return false;
            Hits--;
            if (_hitText != null) _hitText.text = Mathf.Max(0, Hits).ToString();
            if (Hits > 0) return false;
            Break();
            return true;
        }

        void Break()
        {
            Alive = false;
            if (Entry != null && Entry.breakFx != null)
            {
                var fx = Instantiate(Entry.breakFx, transform.position + _visualOffset + new Vector3(0, 0, -1f), Quaternion.identity);
                fx.transform.localScale *= _board.CellSize;
                Destroy(fx, 3f);
            }
            gameObject.SetActive(false);
        }

        void OnDrawGizmos()
        {
            if (!Alive || _board == null) return;
            Gizmos.color = Entry != null ? Entry.debugColor : Color.magenta;
            if (_wedge != null)
            {
                for (int i = 0; i < _wedge.Length; i++) Gizmos.DrawLine(_wedge[i], _wedge[(i + 1) % _wedge.Length]);
                return;
            }
            foreach (var r in _rects) Gizmos.DrawWireCube(r.center, r.size);
        }
    }
}
