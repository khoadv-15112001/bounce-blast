using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Level grid on the lawn. Defaults match the original Game scene: GridView top-left at (-5.708, 9.614),
    /// 9 columns of 1.28 units (12-column boards shrink the cell to keep the same width).
    /// Grid y counts rows from the bottom (converted data).
    /// </summary>
    public class Board : MonoBehaviour
    {
        [Header("Grid (world units, original GridView)")]
        public float gridLeft = -5.708f;
        public float gridTop = 9.614f;
        public float gridWidth = 11.52f;
        [Tooltip("Balls falling below this line drop into the water.")]
        public float waterLine = -7.2f;
        [Tooltip("How far below the water line a ball keeps falling before it splashes (towards the middle of the river).")]
        public float splashDepth = 1.0f;

        public float CellSize { get; private set; } = 1.28f;
        public LevelData Level { get; private set; }
        public readonly List<BoardItem> Items = new List<BoardItem>();

        public float Left => gridLeft;
        public float Right => gridLeft + gridWidth;
        public float Top => gridTop;
        public float WaterLine => waterLine;
        /// <summary>Y where a falling ball hits the water and splashes (a bit inside the river, not on its edge).</summary>
        public float SplashLine => waterLine - Mathf.Max(0f, splashDepth);

        Vector2 _gridOrigin; // world position of the bottom-left corner of cell (0,0)

        /// <summary>Obstacle events raised during <see cref="Tick"/> (brick wall explosion, orbs switching off).</summary>
        public System.Action<BoardItem, BoardItem.ItemEvent> ItemEvent;
        public void RaiseItemEvent(BoardItem item, BoardItem.ItemEvent ev) => ItemEvent?.Invoke(item, ev);

        // Containers and the grid cells they cover (they never move).
        readonly List<(BoardItem item, HashSet<Vector2Int> cells)> _containers = new List<(BoardItem, HashSet<Vector2Int>)>();

        public Vector2 GridToWorldCorner(Vector2 grid) => _gridOrigin + grid * CellSize;
        public Vector2 GridToWorldCentre(Vector2 grid) => _gridOrigin + (grid + new Vector2(0.5f, 0.5f)) * CellSize;

        public void Build(LevelData level, ItemCatalog catalog, Font font)
        {
            Clear();
            Level = level;
            CellSize = gridWidth / Mathf.Max(1, level.width);
            _gridOrigin = new Vector2(gridLeft, gridTop - level.height * CellSize);

            var missing = new HashSet<int>();
            foreach (var data in level.items)
            {
                var entry = catalog.Get(data.type);
                if (entry == null) missing.Add(data.type);
                if (entry != null && entry.kind == ItemKind.Ignore && entry.prefab == null) continue;

                var item = AcquireItem();
#if UNITY_EDITOR
                item.name = $"Item_{data.type}_{data.x}_{data.y}";
#endif
                item.Init(this, data, entry ?? FallbackEntry(data.type), catalog, font);
                Items.Add(item);
            }
            foreach (var t in missing)
                Debug.LogWarning($"[GardenCannon] Level {level.id}: item type {t} is not in the ItemCatalog, treated as indestructible.");

            ApplyAutotiles();

            _containers.Clear();
            foreach (var it in Items)
                if (it.Behaviour == ItemBehaviour.Container)
                    _containers.Add((it, new HashSet<Vector2Int>(it.Data.Cells())));
            UpdateShields();

            // Level intro: items pop in row by row from the top.
            foreach (var it in Items)
                it.PlayIntro((level.height - 1 - it.Data.y) * 0.035f + it.Data.x * 0.01f);
        }

        /// <summary>Duration of the intro pop-in for the current level.</summary>
        public float IntroDuration => Level == null ? 0f : Level.height * 0.035f + Level.width * 0.01f + 0.3f;

        /// <summary>Brick walls connect to same-type neighbours; exposed sides get the brick rim.</summary>
        /// <summary>Re-pick brick wall modules after walls were destroyed (open sides get the rim).</summary>
        public void RefreshAutotiles() => ApplyAutotiles();

        void ApplyAutotiles()
        {
            var byType = new Dictionary<int, HashSet<Vector2Int>>();
            foreach (var it in Items)
            {
                if (it.Entry == null || !it.Entry.autotile || !it.Alive) continue;
                if (!byType.TryGetValue(it.Data.type, out var set)) byType[it.Data.type] = set = new HashSet<Vector2Int>();
                foreach (var c in it.Data.Cells()) set.Add(c);
            }
            foreach (var it in Items)
            {
                if (it.Entry == null || !it.Entry.autotile || !it.Alive) continue;
                var set = byType[it.Data.type];
                var c = new Vector2Int(it.Data.x, it.Data.y);
                int mask = 0;
                if (!set.Contains(c + Vector2Int.up)) mask |= 1;
                if (!set.Contains(c + Vector2Int.right)) mask |= 2;
                if (!set.Contains(c + Vector2Int.down)) mask |= 4;
                if (!set.Contains(c + Vector2Int.left)) mask |= 8;
                it.ApplyAutotile(mask);
            }
        }

        static ItemCatalog.Entry FallbackEntry(int type) => new ItemCatalog.Entry
        {
            label = "Unknown " + type, typeId = type, kind = ItemKind.Indestructible, debugColor = Color.magenta,
        };

        public void Clear()
        {
            foreach (var it in Items)
            {
                if (it == null) continue;
                it.Recycle();
                _itemPool.Push(it);
            }
            Items.Clear();
        }

        // ------------------------------------------------------------------ pooling

        readonly Stack<BoardItem> _itemPool = new Stack<BoardItem>();
        readonly Dictionary<GameObject, Stack<PooledVisual>> _visualPool = new Dictionary<GameObject, Stack<PooledVisual>>();
        Transform _poolRoot;

        Transform PoolRoot
        {
            get
            {
                if (_poolRoot == null)
                {
                    _poolRoot = new GameObject("Pool").transform;
                    _poolRoot.SetParent(transform, false);
                    _poolRoot.gameObject.SetActive(false);
                }
                return _poolRoot;
            }
        }

        BoardItem AcquireItem()
        {
            BoardItem item = _itemPool.Count > 0 ? _itemPool.Pop() : null;
            if (item == null)
            {
                var go = new GameObject("Item");
                go.transform.SetParent(transform, false);
                item = go.AddComponent<BoardItem>();
            }
            return item;
        }

        /// <summary>Reuse an instance of <paramref name="prefab"/> (or create one), reset to the prefab's state.</summary>
        public PooledVisual AcquireVisual(GameObject prefab, Transform parent)
        {
            if (!_visualPool.TryGetValue(prefab, out var stack)) _visualPool[prefab] = stack = new Stack<PooledVisual>();
            PooledVisual v = null;
            while (stack.Count > 0 && v == null) v = stack.Pop();
            if (v == null)
            {
                var go = Instantiate(prefab, parent);
                go.name = prefab.name;
                VisualUtil.StripRuntimeComponents(go);
                v = go.AddComponent<PooledVisual>();
                v.Capture(prefab);
                return v;
            }
            v.transform.SetParent(parent, false);
            v.Restore();
            return v;
        }

        public void ReleaseVisual(PooledVisual v)
        {
            if (v == null) return;
            v.transform.SetParent(PoolRoot, false);
            if (!_visualPool.TryGetValue(v.Prefab, out var stack)) _visualPool[v.Prefab] = stack = new Stack<PooledVisual>();
            stack.Push(v);
        }

        /// <summary>Create instances ahead of time (e.g. during the splash / first frame) to avoid hitches.</summary>
        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null) return;
            var tmp = new List<PooledVisual>(count);
            for (int i = 0; i < count; i++) tmp.Add(AcquireVisual(prefab, PoolRoot));
            foreach (var v in tmp) ReleaseVisual(v);
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < Items.Count; i++) Items[i].Tick(dt);
            UpdateShields();
        }

        /// <summary>Items under a live container are hidden and can't be hit; they pop out when it breaks.</summary>
        void UpdateShields()
        {
            if (_containers.Count == 0) return;
            foreach (var it in Items)
            {
                if (!it.Alive || it.Behaviour == ItemBehaviour.Container) continue;
                var c = it.CentreCell;
                bool covered = false;
                foreach (var (box, cells) in _containers)
                    if (box.Alive && cells.Contains(c)) { covered = true; break; }
                it.SetShielded(covered);
            }
        }

        /// <summary>Live, reachable items whose bounds overlap <paramref name="r"/> (rocket blast area).</summary>
        public void ItemsInRect(Rect r, List<BoardItem> result)
        {
            result.Clear();
            foreach (var it in Items)
            {
                if (!it.Alive || it.Shielded || it.Kind == ItemKind.Ignore) continue;
                var b = it.Bounds;
                if (b.xMax > r.xMin + 0.01f && b.xMin < r.xMax - 0.01f && b.yMax > r.yMin + 0.01f && b.yMin < r.yMax - 0.01f) result.Add(it);
            }
        }

        /// <summary>Centre of the grid cell under a world point (clamped to the board).</summary>
        public Vector2 CellCentreAt(Vector2 world)
        {
            Vector2 g = (world - _gridOrigin) / CellSize;
            int cx = Mathf.Clamp(Mathf.FloorToInt(g.x), 0, Mathf.Max(0, (Level != null ? Level.width : 9) - 1));
            int cy = Mathf.Clamp(Mathf.FloorToInt(g.y), 0, Mathf.Max(0, (Level != null ? Level.height : 12) - 1));
            return GridToWorldCentre(new Vector2(cx, cy));
        }

        /// <summary>True when a world point is over the board grid.</summary>
        public bool IsOverBoard(Vector2 world) =>
            world.x >= Left && world.x <= Right && world.y <= Top && world.y >= Top - (Level != null ? Level.height : 12) * CellSize;

        /// <summary>Live, reachable items whose shape comes within <paramref name="radius"/> of <paramref name="p"/>.</summary>
        public void ItemsNear(Vector2 p, float radius, List<BoardItem> result, BoardItem except = null)
        {
            result.Clear();
            foreach (var it in Items)
            {
                if (it == except || !it.Alive || it.Shielded || it.Kind == ItemKind.Ignore) continue;
                var b = it.Bounds;
                float dx = Mathf.Max(b.xMin - p.x, 0f, p.x - b.xMax);
                float dy = Mathf.Max(b.yMin - p.y, 0f, p.y - b.yMax);
                if (dx * dx + dy * dy <= radius * radius) result.Add(it);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.6f);
            float h = gridTop - waterLine;
            Gizmos.DrawWireCube(new Vector3(gridLeft + gridWidth * 0.5f, waterLine + h * 0.5f), new Vector3(gridWidth, h));
            Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
            for (int i = 0; i <= 9; i++)
                Gizmos.DrawLine(new Vector3(gridLeft + i * 1.28f, gridTop), new Vector3(gridLeft + i * 1.28f, gridTop - 12 * 1.28f));
            for (int j = 0; j <= 12; j++)
                Gizmos.DrawLine(new Vector3(gridLeft, gridTop - j * 1.28f), new Vector3(gridLeft + gridWidth, gridTop - j * 1.28f));
        }
    }
}
