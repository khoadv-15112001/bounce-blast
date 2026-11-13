using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>A level item on the board: physics shape, HP, lane movement and its visual.</summary>
    public partial class BoardItem : MonoBehaviour
    {
        public ItemData Data { get; private set; }
        public ItemCatalog.Entry Entry { get; private set; }
        public ItemKind Kind => Entry != null ? Entry.kind : ItemKind.Indestructible;
        public int Hits { get; private set; }
        public int MaxHits { get; private set; }
        public bool Alive { get; private set; } = true;
        public bool IsMoving => _lane;
        public bool IsBreakable => Kind == ItemKind.Breakable || Kind == ItemKind.PassThrough;

        // Spinner state
        float _spinAngle = 45f;
        int _spinArms = 4;
        float _spinDir = 1f;
        Transform _spinVisual;
        Quaternion _spinVisualBase;
        public Rect Bounds => _bounds;

        public enum HitResult
        {
            None, Damaged, Opened, Hatched, Broken,
            /// <summary>Solid but the hit changed nothing (orb already on, burning fuse).</summary>
            Blocked,
            /// <summary>One part broke (crate, log); the item is still alive. See <see cref="LastPartPos"/>.</summary>
            PartBroken,
            /// <summary>An electric orb switched on. See <see cref="LastPartPos"/>.</summary>
            Activated,
            /// <summary>Brick wall: the fuse is lit, it explodes a moment later.</summary>
            Fused,
        }

        readonly List<Renderer> _rings = new List<Renderer>();
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static MaterialPropertyBlock _ringBlock;

        void UpdateRings()
        {
            if (_rings.Count == 0) return;
            if (_ringBlock == null) _ringBlock = new MaterialPropertyBlock();
            float fill = MaxHits > 0 ? Mathf.Clamp01(Hits / (float)MaxHits) : 1f;
            foreach (var r in _rings)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_ringBlock);
                _ringBlock.SetFloat(FillId, fill);
                r.SetPropertyBlock(_ringBlock);
            }
        }

        /// <summary>World position of the egg hatched by the last hit (egg box).</summary>
        public Vector3 LastHatchPos { get; private set; }
        /// <summary>True when the last hit hatched an egg (also on the final, breaking hit).</summary>
        public bool LastHitHatched { get; private set; }

        // Egg box state
        readonly List<Transform> _eggs = new List<Transform>();
        Transform _doors, _door1, _door2;
        Vector3 _door1Base, _door2Base;
        bool _doorsClosed;
        float _doorOpen = -1f;
        public Vector3 VisualCentre => transform.position + _visualOffset;
        public IReadOnlyList<Vector2Int> Offsets => _offsets;

        Board _board;
        ItemCatalog _catalog;
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
        PooledVisual _pooled;
        Vector3 _visualBaseScale;
        Vector3 _visualOffset;
        DigitLabel _label;                                     // HP number (sprite digits), created once per pooled item
        bool _labelVisible;
        float _punch;
        float _shake;
        readonly List<(Transform t, int n)> _stages = new List<(Transform, int)>();
        readonly Vector2[] _wedgeBuf = new Vector2[3];

        public void Init(Board board, ItemData data, ItemCatalog.Entry entry, ItemCatalog catalog, Font font)
        {
            _board = board;
            _catalog = catalog;
            Data = data;
            Entry = entry;
            _anchor = new Vector2(data.x, data.y);
            ResetState();

            _offsets.Clear();
            var cells = data.Cells();
            foreach (var c in cells) _offsets.Add(c - cells[0]);
            _singleRect = IsFullRectangle(_offsets);

            int damage = Mathf.Max(1, catalog.damagePerHit);
            MaxHits = data.hp > 0 ? Mathf.Max(1, Mathf.CeilToInt(data.hp / (float)damage)) : Mathf.Max(1, entry != null ? entry.defaultHits : 1);
            if (data.layers > 1 && data.hp <= 0) MaxHits *= data.layers;
            Hits = MaxHits;
            InitBehaviour();

            _lane = data.hasLane && data.laneMax > data.laneMin;
            if (_lane)
            {
                _laneVertical = data.variant == 1;
                AxisRange(_laneVertical, out int minOff, out int maxOff);
                _laneLo = data.laneMin - minOff;
                _laneHi = Mathf.Max(_laneLo, data.laneMax - maxOff);
                if (_laneVertical) _anchor.y = Mathf.Clamp(data.lanePos, _laneLo, _laneHi);
                else _anchor.x = Mathf.Clamp(data.lanePos, _laneLo, _laneHi);
            }

            BuildVisual(font);
            RebuildShape();
        }

        /// <summary>Clear everything left over from a previous use of this pooled object.</summary>
        void ResetState()
        {
            Alive = true;
            gameObject.SetActive(true);
            _laneDir = 1f;
            _punch = _shake = 0f;
            _intro = 1f; _introDelay = 0f;
            _stages.Clear();
            _wedge = null;
            _visual = null; _pooled = null;
            _spinAngle = 45f; _spinArms = 4; _spinDir = 1f; _spinVisual = null;
            _labelVisible = false;
            if (_label != null) _label.Hide();
            _eggs.Clear();
            _rings.Clear();
            _doors = _door1 = _door2 = null;
            _doorsClosed = false;
            _doorOpen = -1f;
            LastHitHatched = false;
            ResetBehaviour();
        }

        /// <summary>Give the visual back to the board pool and park this object.</summary>
        public void Recycle()
        {
            ReleaseParts();
            if (_pooled != null) _board.ReleaseVisual(_pooled);
            _pooled = null;
            _visual = null;
            if (_label != null) _label.Hide();
            Alive = false;
            gameObject.SetActive(false);
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

        void AxisRange(bool vertical, out int min, out int max)
        {
            min = int.MaxValue; max = int.MinValue;
            foreach (var o in _offsets)
            {
                int v = vertical ? o.y : o.x;
                min = Mathf.Min(min, v); max = Mathf.Max(max, v);
            }
        }

        // ------------------------------------------------------------------ visual

        void BuildVisual(Font font)
        {
            var e = Entry;
            var prefab = PickPrefab();
            if (e == null || prefab == null) return;
            if (UsesCellParts) { BuildCellParts(); return; }
            _pooled = _board.AcquireVisual(prefab, transform);
            var go = _pooled.gameObject;
            _visual = go.transform;

            float cell = _board.CellSize;
            float rot = e.rotationOffset + (e.rotateByVariant ? 180f - 90f * (((Data.variant % 4) + 4) % 4) : 0f) + _pickedRotation;
            _visual.localRotation = Quaternion.Euler(0f, 0f, rot) * Quaternion.Euler(e.modelEuler) * _visual.localRotation;
            _visualBaseScale = Vector3.Scale(_visual.localScale, Vector3.one * (cell * Mathf.Max(0.01f, e.visualScale)));
            _visual.localScale = _visualBaseScale;

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var o in _offsets) { min = Vector2.Min(min, o); max = Vector2.Max(max, o); }
            Vector2 centre = (min + max) * 0.5f;
            _visualOffset = new Vector3(centre.x * cell, centre.y * cell, 0f);
            _visual.localPosition = _visualOffset;

            if (Behaviour == ItemBehaviour.LogCutter) PlaceLogCutter();
            if (_lane || e.label.Contains("Barrier")) VisualUtil.ApplyOrientation(_visual, _laneVertical);
            if (e.colorByVariant) VisualUtil.ApplyAtlasTile(_visual, _catalog, _catalog.AtlasTileForVariant(Data.variant));

            if (!string.IsNullOrEmpty(e.stagePrefix) && !e.eggBox)
            {
                foreach (var t in _visual.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith(e.stagePrefix) && int.TryParse(t.name.Substring(e.stagePrefix.Length), out int n))
                        _stages.Add((t, n));
                UpdateStage();
            }

            if (e.fitFootprint && !BehaviourOwnsLayout)
            {
                transform.position = Vector3.zero;
                if (TryGetMeshBounds(go, out var b) && b.size.x > 1e-4f && b.size.y > 1e-4f)
                {
                    Vector2 foot = (max - min + Vector2.one) * cell * e.fitFill;
                    float k = Mathf.Min(foot.x / b.size.x, foot.y / b.size.y);
                    _visualBaseScale *= k;
                    _visual.localScale = _visualBaseScale;
                    TryGetMeshBounds(go, out b);
                    _visual.localPosition += new Vector3(_visualOffset.x - b.center.x, _visualOffset.y - b.center.y, 0f);
                    _visualOffset = new Vector3(_visual.localPosition.x, _visual.localPosition.y, _visualOffset.z);
                }
            }

            BuildBehaviourVisual();

            // Container progress ring (Spyke/ContainerRingFill): show remaining HP.
            foreach (var r in _visual.GetComponentsInChildren<Renderer>(true))
                if (r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name == "Spyke/ContainerRingFill")
                    _rings.Add(r);
            UpdateRings();

            if (e.eggBox)
            {
                foreach (var t in _visual.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Doors") _doors = t;
                    else if (t.name == "cover1") _door1 = t;
                    else if (t.name == "cover2") _door2 = t;
                    else if (t.name.Length == 5 && t.name.StartsWith("Egg0")) _eggs.Add(t);
                }
                _eggs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                _doorsClosed = _doors != null && _doors.gameObject.activeSelf;
                if (_door1 != null) _door1Base = _door1.localPosition;
                if (_door2 != null) _door2Base = _door2.localPosition;
                MaxHits = Hits = Mathf.Max(1, _eggs.Count + (_doorsClosed ? 1 : 0));
            }

            if (e.showHitCount)
            {
                // Centred on the raised square of the cube's top face (z = -0.63 cell, which the tilted camera shows
                // slightly higher than the cell centre). White digits with a dark outline all around, like the original.
                if (_label == null)
                {
                    var holder = new GameObject("HitsLabel");
                    holder.transform.SetParent(transform, false);
                    _label = holder.AddComponent<DigitLabel>();
                }
                // Sprites are depth-tested (unlike the old TextMesh), so sit the label just above the mesh's top face
                // (most negative z = towards the camera). On the top face the cell centre is also the visual centre.
                float topZ = TryGetMeshBounds(go, out var meshBounds)
                    ? meshBounds.min.z - transform.position.z
                    : -1.0f * cell;
                _labelBasePos = new Vector3(_visualOffset.x, _visualOffset.y + 0.02f * cell, topZ - 0.02f * cell);
                _label.transform.localPosition = _labelBasePos;
                _label.transform.localScale = Vector3.one;
                _label.Setup(cell * 0.46f, cell * 0.62f, 51);
                _labelVisible = true;
                SetHitLabel(Hits);
            }

            if (Kind == ItemKind.Spinner)
            {
                int v = ((Data.variant % 4) + 4) % 4;
                _spinArms = v < 2 ? 4 : 2;
                _spinDir = v % 2 == 0 ? 1f : -1f;
                foreach (var t in _visual.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Rotate") _spinVisual = t;
                    if (t.name == "FourArmsOnly") t.gameObject.SetActive(_spinArms == 4);
                }
                if (_spinVisual == null && e.sizePrefabs != null && e.sizePrefabs.Length > 0) _spinVisual = _visual; // new art: the whole model spins
                if (_spinVisual != null) _spinVisualBase = _spinVisual.rotation;
            }
        }

        /// <summary>Number on the block (DigitLabel shrinks 2-3 digits to stay inside the top square).</summary>
        void SetHitLabel(int value)
        {
            if (_label != null && _labelVisible) _label.SetValue(value);
        }

        void UpdateStage()
        {
            if (_stages.Count == 0) return;
            int maxStage = 0;
            foreach (var s in _stages) maxStage = Mathf.Max(maxStage, s.n);
            if (Entry != null && Entry.stagesCumulative)
            {
                foreach (var s in _stages) s.t.gameObject.SetActive(s.n <= Hits);
                return;
            }
            int show = Mathf.Clamp(Hits, 1, maxStage);
            foreach (var s in _stages) s.t.gameObject.SetActive(s.n == show);
        }

        /// <summary>Brick wall: pick the module whose rim matches the sides without a same-type neighbour.</summary>
        public void ApplyAutotile(int exposedMask)
        {
            if (_visual == null || _catalog == null || _catalog.autotileModules.Count == 0) return;
            MeshFilter tile = null;
            foreach (var mf in _visual.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name == "Tile") { tile = mf; break; }
            if (tile == null) return;

            ItemCatalog.AutotileModule best = null;
            int bestRot = 0, bestScore = int.MinValue;
            foreach (var m in _catalog.autotileModules)
            {
                if (m == null || m.mesh == null) continue;
                int mask = m.rimMask;
                for (int r = 0; r < 4; r++)
                {
                    int score = 4 - BitCount(mask ^ exposedMask);
                    if (score > bestScore) { bestScore = score; best = m; bestRot = r; }
                    mask = RotateMaskCcw(mask);
                }
            }
            if (best == null) return;
            tile.sharedMesh = best.mesh;
            // Rotate the tile's parent around the view axis (the tile itself carries the model's own rotation).
            var pivot = tile.transform.parent != null ? tile.transform.parent : tile.transform;
            pivot.localRotation = Quaternion.Euler(0f, 0f, 90f * bestRot);
        }

        /// <summary>Rim on the right becomes rim on top after a 90° counter-clockwise turn, etc. Bits: T=1, R=2, B=4, L=8.</summary>
        public static int RotateMaskCcw(int m)
        {
            int t = (m & 1) != 0 ? 1 : 0, r = (m & 2) != 0 ? 1 : 0, b = (m & 4) != 0 ? 1 : 0, l = (m & 8) != 0 ? 1 : 0;
            return (r * 1) | (b * 2) | (l * 4) | (t * 8);
        }

        static int BitCount(int v) { int c = 0; while (v != 0) { c += v & 1; v >>= 1; } return c; }

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

        // ------------------------------------------------------------------ shape

        void RebuildShape()
        {
            float cell = _board.CellSize;
            Vector2 a = _board.GridToWorldCorner(_anchor);
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
                var w = _wedgeBuf;
                switch (((Data.variant % 4) + 4) % 4)
                {
                    case 0: w[0] = bl; w[1] = tr; w[2] = tl; break; // right angle top-left
                    case 1: w[0] = br; w[1] = tr; w[2] = tl; break; // top-right
                    case 2: w[0] = bl; w[1] = br; w[2] = tr; break; // bottom-right
                    default: w[0] = bl; w[1] = br; w[2] = tl; break; // bottom-left
                }
                _wedge = w;
            }
        }

        float _intro = 1f;          // 0..1 pop-in progress
        float _introDelay;

        /// <summary>Pop the item in after a delay (level start).</summary>
        public void PlayIntro(float delay)
        {
            _intro = 0f;
            _introDelay = delay;
            ApplyIntroScale(0f);
        }

        void ApplyIntroScale(float k)
        {
            if (_label != null && _labelVisible) _label.transform.localScale = Vector3.one * k;
            if (_visual != null) _visual.localScale = _visualBaseScale * k;
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        public void Tick(float dt)
        {
            if (!Alive) return;
            if (_intro < 1f)
            {
                if (_introDelay > 0f) { _introDelay -= dt; return; }
                _intro = Mathf.Min(1f, _intro + dt / 0.3f);
                ApplyIntroScale(EaseOutBack(_intro));
                if (_intro < 1f) return;
            }
            if (_lane)
            {
                float speed = Entry != null ? Entry.laneSpeed : 1.5f;
                float v = (_laneVertical ? _anchor.y : _anchor.x) + _laneDir * speed * dt;
                if (v >= _laneHi) { v = _laneHi; _laneDir = -1f; }
                else if (v <= _laneLo) { v = _laneLo; _laneDir = 1f; }
                if (_laneVertical) _anchor.y = v; else _anchor.x = v;
                RebuildShape();
            }
            TickBehaviour(dt);
            if (Kind == ItemKind.Spinner)
            {
                _spinAngle += _spinDir * (Entry != null ? Entry.spinSpeed : 90f) * dt;
                if (_spinVisual != null) _spinVisual.rotation = Quaternion.AngleAxis(_spinAngle - 45f + (Entry != null ? Entry.spinVisualOffset : 0f), Vector3.forward) * _spinVisualBase;
            }
            if (_doorOpen >= 0f && _doorOpen < 1f)
            {
                // Lid halves slide out sideways, pop up a little, then disappear.
                _doorOpen = Mathf.Min(1f, _doorOpen + dt / 0.35f);
                float t = _doorOpen;
                float side = (1f - (1f - t) * (1f - t)) * 1.3f;
                float lift = Mathf.Sin(t * Mathf.PI) * 0.45f;
                if (_door1 != null) _door1.localPosition = _door1Base + new Vector3(side, 0f, -lift);
                if (_door2 != null) _door2.localPosition = _door2Base + new Vector3(-side, 0f, -lift);
                if (t >= 1f && _doors != null) _doors.gameObject.SetActive(false);
            }
            if (_visual != null)
            {
                _punch = Mathf.Max(0f, _punch - dt * 5f);
                _shake = Mathf.Max(0f, _shake - dt * 4f);
                // Squash on hit (wider + flatter), then settle.
                float k = Mathf.Sin(_punch * Mathf.PI);
                _visual.localScale = Vector3.Scale(_visualBaseScale, new Vector3(1f + 0.10f * k, 1f - 0.08f * k, 1f));
                Vector2 jitter = _shake > 0f ? Random.insideUnitCircle * (0.06f * _shake * _board.CellSize) : Vector2.zero;
                _visual.localPosition = _visualOffset + (Vector3)jitter;
                // The number squashes and shakes with its block.
                if (_label != null && _labelVisible)
                {
                    _label.transform.localScale = new Vector3(1f + 0.10f * k, 1f - 0.08f * k, 1f);
                    _label.transform.localPosition = _labelBasePos + (Vector3)jitter;
                }
            }
        }

        Vector3 _labelBasePos;

        /// <summary>Circle test against this item. Returns the deepest contact.</summary>
        public bool Collide(Vector2 p, float r, out Vector2 normal, out float pen)
        {
            normal = Vector2.zero;
            pen = 0f;
            if (!Alive || Shielded || Kind == ItemKind.Ignore) return false;
            if (p.x + r < _bounds.xMin || p.x - r > _bounds.xMax || p.y + r < _bounds.yMin || p.y - r > _bounds.yMax) return false;

            if (Kind == ItemKind.Wedge && _wedge != null)
                return Collision2D.CirclePolygon(p, r, _wedge, out normal, out pen);
            if (Kind == ItemKind.Spinner)
                return CollideSpinner(p, r, out normal, out pen);
            if ((Entry != null && Entry.shape == ItemShape.Circle) || Kind == ItemKind.Bumper)
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

        /// <summary>Arms = capsules from the centre, length = half the footprint, thickness 0.3 cell.</summary>
        bool CollideSpinner(Vector2 p, float r, out Vector2 normal, out float pen)
        {
            normal = Vector2.zero;
            pen = 0f;
            Vector2 c = _bounds.center;
            float len = Mathf.Min(_bounds.width, _bounds.height) * 0.5f * 0.95f;
            float half = _board.CellSize * 0.15f;
            bool hit = false;
            for (int i = 0; i < _spinArms; i++)
            {
                float a = (_spinAngle + i * 360f / _spinArms) * Mathf.Deg2Rad;
                Vector2 tip = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * len;
                Vector2 seg = tip - c;
                float t = Mathf.Clamp01(Vector2.Dot(p - c, seg) / seg.sqrMagnitude);
                Vector2 q = c + seg * t;
                Vector2 d = p - q;
                float dist = d.magnitude;
                float depth = r + half - dist;
                if (depth > pen)
                {
                    hit = true;
                    pen = depth;
                    normal = dist > 1e-5f ? d / dist : new Vector2(-seg.y, seg.x).normalized;
                }
            }
            return hit;
        }

        /// <summary>Surface velocity of a spinner at a point (adds a kick to the ball).</summary>
        public Vector2 SurfaceVelocity(Vector2 p)
        {
            if (Kind != ItemKind.Spinner) return Vector2.zero;
            Vector2 rel = p - _bounds.center;
            float w = _spinDir * (Entry != null ? Entry.spinSpeed : 90f) * Mathf.Deg2Rad;
            return new Vector2(-rel.y, rel.x) * w;
        }

        /// <summary>Apply one ball hit. Returns true when the item breaks.</summary>
        public bool Hit() => HitAt(VisualCentre) == HitResult.Broken;

        /// <summary>Apply one ball hit at <paramref name="point"/> (world).</summary>
        public HitResult HitAt(Vector2 point)
        {
            LastHitHatched = false;
            if (!Alive) return HitResult.None;
            _punch = 1f;
            _shake = 1f;
            PlayHitAnimation();
            if (!IsBreakable) return HitResult.None;
            if (TryBehaviourHit(point, out var special)) return special;

            if (Entry != null && Entry.eggBox)
            {
                if (_doorsClosed)
                {
                    _doorsClosed = false;
                    _doorOpen = 0f;
                    Hits--;
                    return Hits > 0 ? HitResult.Opened : BreakResult();
                }
                // Hatch the egg nearest to the ball.
                Transform best = null;
                float bestD = float.MaxValue;
                foreach (var egg in _eggs)
                {
                    if (egg == null || !egg.gameObject.activeSelf) continue;
                    float d = ((Vector2)egg.position - point).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = egg; }
                }
                if (best != null)
                {
                    LastHatchPos = best.position;
                    LastHitHatched = true;
                    best.gameObject.SetActive(false);
                }
                Hits--;
                return Hits > 0 ? HitResult.Hatched : BreakResult();
            }

            Hits--;
            SetHitLabel(Mathf.Max(0, Hits));
            UpdateRings();
            UpdateStage();
            UpdateTargetState();
            return Hits > 0 ? HitResult.Damaged : BreakResult();
        }

        HitResult BreakResult()
        {
            Break();
            return HitResult.Broken;
        }

        /// <summary>Original item animators (trigger "shake" / state "Shake"), looked up once per pooled visual.</summary>
        void PlayHitAnimation()
        {
            if (_pooled != null) _pooled.PlayShake();
        }

        void Break()
        {
            Alive = false;   // break effects are played (pooled) by GameController / FxPool
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
