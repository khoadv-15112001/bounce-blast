using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Obstacle rules (see <see cref="ItemBehaviour"/>). The original game scripts were not recovered, so the rules are
    /// guesses from the prefabs (child names / states), the sounds and the level data. Numbers live in the ItemCatalog.
    /// </summary>
    public partial class BoardItem
    {
        public enum ItemEvent
        {
            /// <summary>Brick wall dynamite exploded (the wall is gone; neighbours must be hit).</summary>
            Exploded,
            /// <summary>Electric orbs switched off because the chain stalled.</summary>
            OrbsReset,
        }

        public ItemBehaviour Behaviour => Entry != null ? Entry.behaviour : ItemBehaviour.Default;

        /// <summary>Inside a live container: hidden and ignored by balls and explosions.</summary>
        public bool Shielded { get; private set; }

        /// <summary>World position of the part touched by the last hit (crate, log, orb).</summary>
        public Vector3 LastPartPos { get; private set; }

        bool UsesCellParts => Behaviour == ItemBehaviour.ElectricOrb || Behaviour == ItemBehaviour.WoodenBox;
        bool BehaviourOwnsLayout => UsesCellParts || Behaviour == ItemBehaviour.LogCutter || Behaviour == ItemBehaviour.Container;

        void InitBehaviour()
        {
            var e = Entry;
            if (e == null) return;
            switch (e.behaviour)
            {
                case ItemBehaviour.LogCutter:
                    SetupLogLine();
                    MaxHits = Hits = Mathf.Max(1, _lineCells.Count - 1);
                    break;
                case ItemBehaviour.ElectricOrb:
                    MaxHits = Hits = Mathf.Max(1, _offsets.Count);
                    break;
                case ItemBehaviour.WoodenBox:
                    MaxHits = Hits = Mathf.Max(1, _offsets.Count * Mathf.Max(1, e.cellHits));
                    break;
            }
        }

        void ResetBehaviour()
        {
            ReleaseParts();
            HideShadows();
            _hingeDoors = false;
            Shielded = false;
            if (_label != null) _label.gameObject.SetActive(true);
            _fuse = -1f;
            _bwCracked = _bwExposed = _bwFuse = null;
            _target = _targetBroken = null;
            _logs.Clear();
            _logsParent = null; _logTemplate = null; _gears = null;
            _feed = 0f;
            _orbTimer = 0f;
        }

        void BuildBehaviourVisual()
        {
            switch (Behaviour)
            {
                case ItemBehaviour.Container: BuildContainer(); break;
                case ItemBehaviour.BrickWall: FindBrickWallParts(); break;
                case ItemBehaviour.TargetStand: FindTargetParts(); break;
                case ItemBehaviour.LogCutter: BuildLogs(); break;
            }
        }

        void TickBehaviour(float dt)
        {
            switch (Behaviour)
            {
                case ItemBehaviour.BrickWall: TickFuse(dt); break;
                case ItemBehaviour.LogCutter: TickFeed(dt); break;
                case ItemBehaviour.ElectricOrb: TickOrbs(dt); break;
            }
        }

        bool TryBehaviourHit(Vector2 point, out HitResult result)
        {
            result = HitResult.None;
            switch (Behaviour)
            {
                case ItemBehaviour.BrickWall: result = HitBrickWall(); return true;
                case ItemBehaviour.LogCutter: result = HitLogCutter(); return true;
                case ItemBehaviour.ElectricOrb: result = HitOrb(point); return true;
                case ItemBehaviour.WoodenBox: result = HitCrate(point); return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ container shield

        /// <summary>Hide / reveal an item covered by a container (revealed items pop in).</summary>
        public void SetShielded(bool shielded)
        {
            if (Shielded == shielded) return;
            Shielded = shielded;
            if (_visual != null) _visual.gameObject.SetActive(!shielded);
            if (_label != null && _labelVisible) _label.gameObject.SetActive(!shielded);
            foreach (var s in _shadows) if (s != null && s.enabled) s.gameObject.SetActive(!shielded);
            if (!shielded) PlayIntro(0f);
        }

        /// <summary>Grid cell under the item's centre (used to test whether a container covers it).</summary>
        public Vector2Int CentreCell
        {
            get
            {
                Vector2 c = _bounds.center;
                Vector2 g = (c - _board.GridToWorldCorner(Vector2.zero)) / _board.CellSize;
                return new Vector2Int(Mathf.FloorToInt(g.x), Mathf.FloorToInt(g.y));
            }
        }

        // ------------------------------------------------------------------ container visual

        const float ContainerHeight = 0.95f;   // cells, towards the camera
        Mesh _panelMesh, _frameMesh;

        /// <summary>
        /// The Panels / Frame meshes of the original were generated at runtime (ContainerBlockerGeometry): build a box
        /// over the footprint (yellow panels) with a metal rim on the open edges, and put the HP badge on top.
        /// </summary>
        void BuildContainer()
        {
            MeshFilter panels = null, frame = null;
            Transform badge = null;
            foreach (var t in _visual.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Panels") panels = t.GetComponent<MeshFilter>();
                else if (t.name == "Frame") frame = t.GetComponent<MeshFilter>();
                else if (t.name == "Badge") badge = t;
            }
            if (panels == null && frame == null) { FitCrate(); return; }
            // Visual units = cells (the visual root is scaled by the cell size), origin = footprint centre.
            Vector2 centre = new Vector2(_visualOffset.x, _visualOffset.y) / _board.CellSize;
            var set = new HashSet<Vector2Int>(_offsets);
            if (panels != null)
            {
                if (_panelMesh == null) _panelMesh = new Mesh { name = "ContainerPanels" };
                BuildBoxMesh(_panelMesh, set, centre, ContainerHeight, 0f, 0f);
                panels.sharedMesh = _panelMesh;
            }
            if (frame != null)
            {
                if (_frameMesh == null) _frameMesh = new Mesh { name = "ContainerFrame" };
                BuildBoxMesh(_frameMesh, set, centre, ContainerHeight + 0.04f, 0.11f, 0.22f);
                frame.sharedMesh = _frameMesh;
            }
            // The badge sat 1.65 above the ground in the original layout: put it on our lid.
            if (badge != null) badge.localPosition = new Vector3(0f, 0f, 1.65f - ContainerHeight - 0.06f);
            // Inside items must not poke out of the lid: they are hidden while shielded (see Board).
        }

        /// <summary>New crate art: turn the crate to the footprint's orientation and stretch it over the rectangle.</summary>
        void FitCrate()
        {
            float cell = _board.CellSize;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var o in _offsets) { min = Vector2.Min(min, o); max = Vector2.Max(max, o); }
            Vector2 size = (max - min + Vector2.one) * cell * 0.98f;
            if (!TryGetMeshBounds(_visual.gameObject, out var b) || b.size.x < 1e-4f || b.size.y < 1e-4f) return;
            if (Mathf.Abs(size.x - size.y) > 0.01f && (b.size.x > b.size.y) != (size.x > size.y))
            {
                _visual.localRotation = Quaternion.Euler(0f, 0f, 90f) * _visual.localRotation;
                TryGetMeshBounds(_visual.gameObject, out b);
            }
            float sx = size.x / b.size.x, sy = size.y / b.size.y, sz = Mathf.Min(1.25f, Mathf.Sqrt(sx * sy));
            // Scale along the local axes that map to world x / y / z.
            var s = _visual.localScale;
            var q = _visual.rotation;
            Vector3 ax = q * Vector3.right, ay = q * Vector3.up, az = q * Vector3.forward;
            float Pick(Vector3 d) => Mathf.Abs(d.x) > 0.7f ? sx : Mathf.Abs(d.y) > 0.7f ? sy : sz;
            _visualBaseScale = new Vector3(s.x * Pick(ax), s.y * Pick(ay), s.z * Pick(az));
            _visual.localScale = _visualBaseScale;
            TryGetMeshBounds(_visual.gameObject, out b);
            Vector3 want = transform.position + _visualOffset;
            _visual.position += new Vector3(want.x - b.center.x, want.y - b.center.y, 0f);
            _visualOffset = _visual.localPosition;
        }

        /// <summary>
        /// <paramref name="rim"/> = 0: lid + side walls of the footprint. rim &gt; 0: only a band of that width along the
        /// open edges on top plus the upper <paramref name="rimDrop"/> of the side walls (metal frame).
        /// </summary>
        static void BuildBoxMesh(Mesh mesh, HashSet<Vector2Int> cells, Vector2 centre, float h, float rim, float rimDrop)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            Vector2 uvMetal = new Vector2(0.55f, 0.45f);
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
                uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
                // Unity front faces: Cross(b - a, c - a) points along the normal.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f) { tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i); tri.Add(i + 2); tri.Add(i + 3); }
                else { tri.Add(i); tri.Add(i + 2); tri.Add(i + 1); tri.Add(i); tri.Add(i + 3); tri.Add(i + 2); }
            }
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            foreach (var c in cells)
            {
                float x = c.x - centre.x, y = c.y - centre.y;
                float x0 = x - 0.5f, x1 = x + 0.5f, y0 = y - 0.5f, y1 = y + 0.5f;
                if (rim <= 0f)
                {
                    Quad(new Vector3(x0, y0, -h), new Vector3(x1, y0, -h), new Vector3(x1, y1, -h), new Vector3(x0, y1, -h), Vector3.back,
                        new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                }
                foreach (var d in dirs)
                {
                    if (cells.Contains(c + d)) continue;
                    // Edge endpoints (p, q) on this side and the inward direction.
                    Vector3 p, q, inward;
                    if (d == Vector2Int.up) { p = new Vector3(x0, y1, 0); q = new Vector3(x1, y1, 0); inward = Vector3.down; }
                    else if (d == Vector2Int.right) { p = new Vector3(x1, y0, 0); q = new Vector3(x1, y1, 0); inward = Vector3.left; }
                    else if (d == Vector2Int.down) { p = new Vector3(x0, y0, 0); q = new Vector3(x1, y0, 0); inward = Vector3.up; }
                    else { p = new Vector3(x0, y0, 0); q = new Vector3(x0, y1, 0); inward = Vector3.right; }
                    Vector3 outN = -inward;
                    float zTop = -h, zBottom = rim > 0f ? -h + rimDrop : 0f;
                    // Side wall (or the upper band of it for the frame), pushed out a hair for the frame.
                    Vector3 push = rim > 0f ? outN * 0.012f : Vector3.zero;
                    Quad(p + push + Vector3.forward * zBottom, q + push + Vector3.forward * zBottom, q + push + Vector3.forward * zTop, p + push + Vector3.forward * zTop, outN,
                        rim > 0f ? uvMetal : new Vector2(0, 0.2f), rim > 0f ? uvMetal : new Vector2(1, 0.2f), rim > 0f ? uvMetal : new Vector2(1, 0), rim > 0f ? uvMetal : new Vector2(0, 0));
                    if (rim > 0f)
                    {
                        // Band on the lid along this open edge.
                        Vector3 a = p + Vector3.forward * zTop, b = q + Vector3.forward * zTop;
                        Quad(a, b, b + inward * rim, a + inward * rim, Vector3.back, uvMetal, uvMetal, uvMetal, uvMetal);
                    }
                }
            }
            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
        }

        // ------------------------------------------------------------------ brick wall

        Transform _bwCracked, _bwExposed, _bwFuse;
        float _fuse = -1f;

        void FindBrickWallParts()
        {
            foreach (var t in _visual.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Cracked") _bwCracked = t;
                else if (t.name == "Exposed") _bwExposed = t;
                else if (t.name == "Fuse") _bwFuse = t;
            }
        }

        HitResult HitBrickWall()
        {
            if (_fuse >= 0f) return HitResult.Blocked;
            Hits = Mathf.Max(0, Hits - 1);
            // MaxHits 3: 2 left = cracked, 1 left = dynamite exposed, 0 = fuse burning.
            if (_bwCracked != null) _bwCracked.gameObject.SetActive(Hits < MaxHits && Hits > 0);
            if (_bwExposed != null) _bwExposed.gameObject.SetActive(Hits <= 1);
            if (Hits > 0) return HitResult.Damaged;
            if (_bwFuse != null) _bwFuse.gameObject.SetActive(true);
            _fuse = Mathf.Max(0.05f, Entry != null ? Entry.fuseTime : 0.7f);
            return HitResult.Fused;
        }

        /// <summary>Light the fuse from outside (e.g. a neighbouring explosion with 1 hit left).</summary>
        public bool IsFuseBurning => _fuse >= 0f;

        void TickFuse(float dt)
        {
            if (_fuse < 0f) return;
            _shake = Mathf.Max(_shake, 0.35f);   // trembles while the fuse burns
            _fuse -= dt;
            if (_fuse > 0f) return;
            _fuse = -1f;
            Break();
            _board.RaiseItemEvent(this, ItemEvent.Exploded);
        }

        // ------------------------------------------------------------------ target stand

        Transform _target, _targetBroken;

        void FindTargetParts()
        {
            foreach (var mf in _visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.name == "Target") _target = mf.transform;
                else if (mf.name == "TargetBroken") _targetBroken = mf.transform;
            }
            UpdateTargetState();
        }

        void UpdateTargetState()
        {
            if (Behaviour != ItemBehaviour.TargetStand) return;
            bool broken = Hits < MaxHits;
            if (_target != null) _target.gameObject.SetActive(!broken || _targetBroken == null);
            if (_targetBroken != null) _targetBroken.gameObject.SetActive(broken);
        }

        // ------------------------------------------------------------------ log cutter

        readonly List<Vector2Int> _lineCells = new List<Vector2Int>(); // machine first, then logs outwards
        readonly List<Transform> _logs = new List<Transform>();       // nearest to the machine first
        Vector2Int _logDir;
        Transform _logsParent, _logTemplate, _gears;
        float _logStep, _feed;

        /// <summary>Variant 0 = machine on the right, 1 = left, 2 = bottom, 3 = top; logs run away from it.</summary>
        void SetupLogLine()
        {
            _lineCells.Clear();
            int v = ((Data.variant % 4) + 4) % 4;
            _logDir = v == 0 ? Vector2Int.left : v == 1 ? Vector2Int.right : v == 2 ? Vector2Int.up : Vector2Int.down;
            // Machine = the footprint cell furthest against the log direction.
            Vector2Int machine = _offsets[0];
            foreach (var o in _offsets)
                if (Vector2.Dot(o, _logDir) < Vector2.Dot(machine, _logDir)) machine = o;
            var set = new HashSet<Vector2Int>(_offsets);
            for (var c = machine; set.Contains(c); c += _logDir) _lineCells.Add(c);
            // Odd footprints: keep whatever is not on the line as plain cells of the machine row.
            if (_lineCells.Count == 0) _lineCells.Add(machine);
        }

        void PlaceLogCutter()
        {
            float cell = _board.CellSize;
            var m = _lineCells[0];
            _visualOffset = new Vector3(m.x * cell, m.y * cell, 0f);
            _visual.localPosition = _visualOffset;
            float angle = Mathf.Atan2(_logDir.y, _logDir.x) * Mathf.Rad2Deg;
            _visual.localRotation = Quaternion.Euler(0f, 0f, angle) * _visual.localRotation;
        }

        void BuildLogs()
        {
            foreach (var t in _visual.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Logs") _logsParent = t;
                else if (t.name == "Gears") _gears = t;
            }
            if (_logsParent == null) return;
            // Clones from a previous use of this pooled visual.
            for (int i = _logsParent.childCount - 1; i >= 0; i--)
            {
                var c = _logsParent.GetChild(i);
                if (c.name == "LogX") { c.gameObject.SetActive(false); Destroy(c.gameObject); }
            }
            _logTemplate = _logsParent.Find("Log");
            if (_logTemplate == null) return;
            // Local units of the Logs transform per grid cell (the template sits inside the machine).
            float worldPerUnit = _logsParent.TransformVector(Vector3.right).magnitude;
            _logStep = worldPerUnit > 1e-5f ? _board.CellSize / worldPerUnit : 1f;
            _logs.Clear();
            for (int i = 1; i < _lineCells.Count; i++)
            {
                var log = Instantiate(_logTemplate, _logsParent);
                log.name = "LogX";
                log.localPosition = _logTemplate.localPosition + Vector3.right * (_logStep * i);
                _logs.Add(log);
            }
        }

        HitResult HitLogCutter()
        {
            if (_logs.Count > 0)
            {
                var cut = _logs[0];
                LastPartPos = cut.position;
                _logs.RemoveAt(0);
                cut.gameObject.SetActive(false);
                Destroy(cut.gameObject);
                _feed = 1f;   // the rest slide one cell towards the machine
            }
            else LastPartPos = VisualCentre;
            if (_lineCells.Count > 1)
            {
                _lineCells.RemoveAt(_lineCells.Count - 1);
                SetCells(_lineCells);
            }
            Hits = Mathf.Max(0, Hits - 1);
            if (Hits > 0) return HitResult.PartBroken;
            return BreakResult();
        }

        void TickFeed(float dt)
        {
            if (_feed <= 0f || _logTemplate == null) return;
            _feed = Mathf.Max(0f, _feed - dt / 0.22f);
            float ease = _feed * _feed;
            for (int i = 0; i < _logs.Count; i++)
                if (_logs[i] != null) _logs[i].localPosition = _logTemplate.localPosition + Vector3.right * (_logStep * (i + 1 + ease));
            if (_gears != null) _gears.Rotate(0f, 0f, 900f * dt, Space.Self);
        }

        // ------------------------------------------------------------------ per-cell parts (orbs, crates)

        class Part
        {
            public Vector2Int off;
            public PooledVisual visual;
            public int hits;
            public bool active;
            public Transform fxActive;
            public SpriteRenderer shadow;
            public readonly List<(Transform t, int n)> stages = new List<(Transform, int)>();
        }

        readonly List<Part> _parts = new List<Part>();
        Transform _partRoot;
        float _orbTimer;

        void ReleaseParts()
        {
            foreach (var p in _parts)
                if (p.visual != null) _board.ReleaseVisual(p.visual);
            _parts.Clear();
        }

        void BuildCellParts()
        {
            var e = Entry;
            float cell = _board.CellSize;
            if (_partRoot == null)
            {
                _partRoot = new GameObject("Parts").transform;
                _partRoot.SetParent(transform, false);
            }
            _partRoot.gameObject.SetActive(true);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var o in _offsets) { min = Vector2.Min(min, o); max = Vector2.Max(max, o); }
            Vector2 centre = (min + max) * 0.5f;
            _visualOffset = new Vector3(centre.x * cell, centre.y * cell, 0f);
            _visual = _partRoot;
            _visual.localPosition = _visualOffset;
            _visual.localRotation = Quaternion.identity;
            _visualBaseScale = Vector3.one;
            _visual.localScale = Vector3.one;

            int hitsPerCell = Behaviour == ItemBehaviour.WoodenBox ? Mathf.Max(1, e.cellHits) : 1;
            foreach (var o in _offsets)
            {
                var pv = _board.AcquireVisual(PickPrefab(), _partRoot);
                var t = pv.transform;
                t.localPosition = new Vector3((o.x - centre.x) * cell, (o.y - centre.y) * cell, 0f);
                t.localRotation = Quaternion.Euler(e.modelEuler) * t.localRotation;
                t.localScale = Vector3.Scale(t.localScale, Vector3.one * (cell * Mathf.Max(0.01f, e.visualScale)));
                var part = new Part { off = o, visual = pv, hits = hitsPerCell };
                foreach (var c in t.GetComponentsInChildren<Transform>(true))
                {
                    if (c.name == "FX_Active") part.fxActive = c;
                    else if (!string.IsNullOrEmpty(e.stagePrefix) && c.name.StartsWith(e.stagePrefix) &&
                             int.TryParse(c.name.Substring(e.stagePrefix.Length), out int n))
                        part.stages.Add((c, n));
                }
                if (part.fxActive != null) part.fxActive.gameObject.SetActive(false);
                UpdatePartStage(part, hitsPerCell);
                if (e.castShadow) part.shadow = ShowShadow(_parts.Count, new Vector2(o.x * cell, o.y * cell), new Vector2(cell, cell));
                _parts.Add(part);
            }
        }

        Part NearestPart(Vector2 point, bool aliveOnly)
        {
            Part best = null;
            float bestD = float.MaxValue;
            foreach (var p in _parts)
            {
                if (aliveOnly && p.hits <= 0) continue;
                float d = ((Vector2)p.visual.transform.position - point).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        void UpdatePartStage(Part p, int maxHits)
        {
            if (p.stages.Count == 0) return;
            int maxStage = 0;
            foreach (var s in p.stages) maxStage = Mathf.Max(maxStage, s.n);
            if (Entry != null && Entry.stagesCumulative)
            {
                // New wooden block: planks "Wood 1..3" come off as the crate takes hits.
                int keep = maxHits <= 1 ? maxStage : Mathf.RoundToInt(maxStage * (p.hits - 1) / (float)(maxHits - 1));
                foreach (var s in p.stages) s.t.gameObject.SetActive(s.n <= keep);
                return;
            }
            int show = Mathf.Clamp(Mathf.CeilToInt(maxStage * p.hits / (float)Mathf.Max(1, maxHits)), 1, maxStage);
            foreach (var s in p.stages) s.t.gameObject.SetActive(s.n == show);
        }

        HitResult HitCrate(Vector2 point)
        {
            var p = NearestPart(point, true);
            if (p == null) return BreakResult();
            int perCell = Mathf.Max(1, Entry.cellHits);
            p.hits--;
            Hits = Mathf.Max(0, Hits - 1);
            LastPartPos = p.visual.transform.position;
            if (p.hits > 0)
            {
                UpdatePartStage(p, perCell);
                return HitResult.Damaged;
            }
            p.visual.gameObject.SetActive(false);
            if (p.shadow != null) { p.shadow.enabled = false; p.shadow.gameObject.SetActive(false); }
            var alive = new List<Vector2Int>();
            foreach (var q in _parts) if (q.hits > 0) alive.Add(q.off);
            if (alive.Count == 0) return BreakResult();
            SetCells(alive);
            return HitResult.PartBroken;
        }

        HitResult HitOrb(Vector2 point)
        {
            var p = NearestPart(point, false);
            if (p == null) return HitResult.Blocked;
            LastPartPos = p.visual.transform.position;
            if (p.active) return HitResult.Blocked;
            p.active = true;
            if (p.fxActive != null) p.fxActive.gameObject.SetActive(true);
            Hits = Mathf.Max(0, Hits - 1);
            _orbTimer = Entry != null ? Entry.orbResetTime : 0f;
            return Hits > 0 ? HitResult.Activated : BreakResult();
        }

        void TickOrbs(float dt)
        {
            if (Hits >= MaxHits || Hits <= 0 || Entry == null || Entry.orbResetTime <= 0f) return;
            _orbTimer -= dt;
            if (_orbTimer > 0f) return;
            foreach (var p in _parts)
            {
                p.active = false;
                if (p.fxActive != null) p.fxActive.gameObject.SetActive(false);
            }
            Hits = MaxHits;
            _board.RaiseItemEvent(this, ItemEvent.OrbsReset);
        }

        /// <summary>World positions of the orbs (discharge effect).</summary>
        public void PartPositions(List<Vector3> result)
        {
            result.Clear();
            foreach (var p in _parts) if (p.visual != null) result.Add(p.visual.transform.position);
        }

        // ------------------------------------------------------------------ 3D art prefab picking

        float _pickedRotation;   // extra Z rotation for the picked prefab (vertical sliders)

        /// <summary>
        /// Prefab for this item: by colour (blocks), by size suffix (sliders, spinners, crates) or the entry's prefab.
        /// </summary>
        GameObject PickPrefab()
        {
            _pickedRotation = 0f;
            var e = Entry;
            if (e == null) return null;
            if (e.colorPrefabs != null && e.colorPrefabs.Length > 0)
            {
                Color want = VisualUtil.AtlasTileColor(_catalog.AtlasTileForVariant(Data.variant));
                GameObject best = null;
                float bestD = float.MaxValue;
                foreach (var cp in e.colorPrefabs)
                {
                    if (cp == null || cp.prefab == null) continue;
                    float d = ColorDistance(want, cp.color);
                    if (d < bestD) { bestD = d; best = cp.prefab; }
                }
                if (best != null) return best;
            }
            if (e.sizePrefabs != null && e.sizePrefabs.Length > 0)
            {
                Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue), max = new Vector2Int(int.MinValue, int.MinValue);
                foreach (var o in _offsets) { min = Vector2Int.Min(min, o); max = Vector2Int.Max(max, o); }
                int w = max.x - min.x + 1, h = max.y - min.y + 1;
                GameObject best = null;
                float bestScore = float.MaxValue;
                foreach (var p in e.sizePrefabs)
                {
                    if (p == null) continue;
                    float score;
                    if (TryParseSize(p.name, out int a, out int bb))
                    {
                        // Footprint WxH: compare the aspect ratio (either orientation).
                        float want = Mathf.Log(Mathf.Max(w, h) / (float)Mathf.Min(w, h));
                        float have = Mathf.Log(Mathf.Max(a, bb) / (float)Mathf.Min(a, bb));
                        score = Mathf.Abs(want - have) + 0.01f * Mathf.Abs(Mathf.Max(a, bb) - Mathf.Max(w, h));
                    }
                    else if (TryParseSuffix(p.name, out int n))
                    {
                        int key = Kind == ItemKind.Spinner ? ((((Data.variant % 4) + 4) % 4) < 2 ? 4 : 2) : Mathf.Max(w, h);
                        score = Mathf.Abs(n - key);
                    }
                    else continue;
                    if (score < bestScore) { bestScore = score; best = p; }
                }
                if (best != null)
                {
                    // Bars are modelled horizontally: turn them for vertical footprints.
                    if (Kind != ItemKind.Spinner && Behaviour != ItemBehaviour.Container && h > w) _pickedRotation = 90f;
                    return best;
                }
            }
            return e.prefab;
        }

        static float ColorDistance(Color a, Color b)
        {
            // Weighted RGB distance plus a hue term so oranges / browns don't all collapse onto one colour.
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            Color.RGBToHSV(a, out float ha, out float sa, out float va);
            Color.RGBToHSV(b, out float hb, out float sb, out float vb);
            float dh = Mathf.Abs(ha - hb); dh = Mathf.Min(dh, 1f - dh);
            float hueW = Mathf.Min(sa, sb);   // hue only matters for saturated colours
            return 2f * dr * dr + 4f * dg * dg + 3f * db * db + 6f * hueW * dh * dh + 0.5f * (sa - sb) * (sa - sb);
        }

        static bool TryParseSize(string name, out int a, out int b)
        {
            a = b = 0;
            int us = name.LastIndexOf('_');
            if (us < 0) return false;
            var parts = name.Substring(us + 1).Split('x');
            return parts.Length == 2 && int.TryParse(parts[0], out a) && int.TryParse(parts[1], out b) && a > 0 && b > 0;
        }

        static bool TryParseSuffix(string name, out int n)
        {
            n = 0;
            int us = name.LastIndexOf('_');
            return us >= 0 && int.TryParse(name.Substring(us + 1), out n);
        }

        // ------------------------------------------------------------------ egg box slots (balloons)

        /// <summary>Body material of the slot model hatched by the last hit (the released balloon uses the same colour).</summary>
        public Material LastHatchMaterial { get; private set; }

        const string SlotName = "SlotModel";

        /// <summary>
        /// Replace the egg meshes of every slot by the entry's eggPrefab (balloon, string hidden), coloured per slot.
        /// The models are kept on the pooled visual and reused.
        /// </summary>
        void SetupEggSlots()
        {
            var e = Entry;
            LastHatchMaterial = null;
            if (e == null || e.eggPrefab == null) return;
            float cell = _board.CellSize;
            for (int i = 0; i < _eggs.Count; i++)
            {
                var slot = _eggs[i];
                slot.gameObject.SetActive(true);
                Transform model = null;
                for (int c = 0; c < slot.childCount; c++)
                {
                    var ch = slot.GetChild(c);
                    if (ch.name == SlotName) model = ch;
                    else ch.gameObject.SetActive(false);   // original egg mesh + its shadow
                }
                if (model == null)
                {
                    model = Instantiate(e.eggPrefab, slot, false).transform;
                    model.name = SlotName;
                    VisualUtil.StripRuntimeComponents(model.gameObject);
                    HideStrings(model);
                }
                model.gameObject.SetActive(true);
                // Stands out of the tray, leaning towards the camera; height = eggSize cells.
                model.rotation = Quaternion.Euler(-55f, 0f, 0f);
                float h = BodyHeight(e.eggPrefab);
                float parentScale = Mathf.Max(1e-4f, slot.lossyScale.x);
                model.localScale = Vector3.one * (e.eggSize * cell / Mathf.Max(1e-4f, h) / parentScale);
                model.localPosition = Vector3.zero;
                if (e.eggMaterials != null && e.eggMaterials.Length > 0)
                {
                    var body = model.GetComponent<Renderer>();
                    var m = e.eggMaterials[(i + Data.x * 3 + Data.y * 5) % e.eggMaterials.Length];
                    if (body != null && m != null) body.sharedMaterial = m;
                }
            }
        }

        static Material SlotMaterial(Transform slot)
        {
            var model = slot.Find(SlotName);
            var r = model != null ? model.GetComponent<Renderer>() : null;
            return r != null ? r.sharedMaterial : null;
        }

        /// <summary>The balloon model carries a long "string" child: not wanted in the game.</summary>
        public static void HideStrings(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name.ToLowerInvariant().Contains("string")) t.gameObject.SetActive(false);
        }

        /// <summary>Height of the prefab's own (root) mesh at scale 1.</summary>
        static float BodyHeight(GameObject prefab)
        {
            var mf = prefab.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size.y : 1f;
        }

        // ------------------------------------------------------------------ balloon box

        bool _hingeDoors;
        Quaternion _door1Rot, _door2Rot;

        /// <summary>Lid half on the left of the box opens counter-clockwise, the right one clockwise (model space).</summary>
        static float HingeSign(Transform door) => door.localPosition.x < 0f ? 1f : -1f;

        /// <summary>
        /// The balloon box has no egg slots: make a 3x3 grid inside the tray (model space, 1 unit = 1 cell, box = 2x2),
        /// named like the original Egg01..Egg09 so the egg box logic is unchanged. Kept on the pooled visual.
        /// </summary>
        void CreateEggSlots()
        {
            const float step = 0.6f, height = 0.28f;
            int n = 1;
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++, n++)
                {
                    var slot = new GameObject("Egg0" + n).transform;
                    slot.SetParent(_visual, false);
                    // -Z of the art is the top of the screen.
                    slot.localPosition = new Vector3((col - 1) * step, height, (row - 1) * step);
                    _eggs.Add(slot);
                }
        }

        // ------------------------------------------------------------------ ground shadows

        readonly List<SpriteRenderer> _shadows = new List<SpriteRenderer>();
        static Sprite _shadowSprite;
        const int ShadowOrder = -6;   // over the 3D lawn (-7), under every other sprite
        static readonly Color ShadowColor = new Color(0f, 0.06f, 0f, 0.42f);

        /// <summary>One soft shadow under the whole footprint (or per crate for cell parts).</summary>
        void BuildShadow()
        {
            if (Entry == null || !Entry.castShadow || UsesCellParts) return;
            float cell = _board.CellSize;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var o in _offsets) { min = Vector2.Min(min, o); max = Vector2.Max(max, o); }
            Vector2 centre = (min + max) * 0.5f * cell;
            Vector2 size = (max - min + Vector2.one) * cell;
            ShowShadow(0, centre, size);
        }

        SpriteRenderer ShowShadow(int index, Vector2 centre, Vector2 size)
        {
            while (_shadows.Count <= index)
            {
                var go = new GameObject("Shadow");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ShadowSprite();
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.sortingOrder = ShadowOrder;
                sr.color = ShadowColor;
                _shadows.Add(sr);
            }
            var s = _shadows[index];
            float cell = _board.CellSize;
            s.gameObject.SetActive(true);
            s.enabled = true;
            // Short contact shadow: the same size as the item, peeking out a little below it.
            // A hair in front of the 3D lawn (both at z = 0 fought for depth: striped shadows in the game camera).
            s.transform.localPosition = new Vector3(centre.x, centre.y - 0.1f * cell, -0.03f);
            s.transform.localRotation = Quaternion.identity;
            s.transform.localScale = Vector3.one;
            s.size = new Vector2(size.x * 0.96f, size.y * 0.96f);
            return s;
        }

        void HideShadows()
        {
            foreach (var s in _shadows)
                if (s != null) { s.enabled = false; s.gameObject.SetActive(false); }
        }

        void ScaleShadows(float k)
        {
            foreach (var s in _shadows)
                if (s != null && s.enabled) s.transform.localScale = Vector3.one * k;
        }

        /// <summary>Soft rounded rectangle, 9-sliced so it stretches over any footprint (generated once).</summary>
        static Sprite ShadowSprite()
        {
            if (_shadowSprite != null) return _shadowSprite;
            const int size = 64, border = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "ItemShadow" };
            var px = new Color32[size * size];
            const float radius = 12f, soft = 9f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Signed distance to a rounded square, positive outside.
                    float qx = Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius - 2f);
                    float qy = Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius - 2f);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float a = Mathf.Clamp01(0.5f - outside / soft);
                    a = a * a * (3f - 2f * a);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _shadowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            _shadowSprite.name = "ItemShadow";
            return _shadowSprite;
        }

        // ------------------------------------------------------------------ shared

        /// <summary>Replace the collision cells (parts broke off).</summary>
        void SetCells(List<Vector2Int> cells)
        {
            _offsets.Clear();
            _offsets.AddRange(cells);
            _singleRect = IsFullRectangle(_offsets);
            RebuildShape();
        }
    }
}
