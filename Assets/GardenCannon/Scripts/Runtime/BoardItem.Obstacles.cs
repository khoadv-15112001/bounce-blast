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
                var pv = _board.AcquireVisual(e.prefab, _partRoot);
                var t = pv.transform;
                t.localPosition = new Vector3((o.x - centre.x) * cell, (o.y - centre.y) * cell, 0f);
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

        static void UpdatePartStage(Part p, int maxHits)
        {
            if (p.stages.Count == 0) return;
            int maxStage = 0;
            foreach (var s in p.stages) maxStage = Mathf.Max(maxStage, s.n);
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
