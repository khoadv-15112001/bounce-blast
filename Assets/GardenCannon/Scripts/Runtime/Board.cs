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

            // Level intro: items pop in row by row from the top.
            foreach (var it in Items)
                it.PlayIntro((level.height - 1 - it.Data.y) * 0.035f + it.Data.x * 0.01f);
        }

        /// <summary>Duration of the intro pop-in for the current level.</summary>
        public float IntroDuration => Level == null ? 0f : Level.height * 0.035f + Level.width * 0.01f + 0.3f;

        /// <summary>Brick walls connect to same-type neighbours; exposed sides get the brick rim.</summary>
        void ApplyAutotiles()
        {
            var byType = new Dictionary<int, HashSet<Vector2Int>>();
            foreach (var it in Items)
            {
                if (it.Entry == null || !it.Entry.autotile) continue;
                if (!byType.TryGetValue(it.Data.type, out var set)) byType[it.Data.type] = set = new HashSet<Vector2Int>();
                foreach (var c in it.Data.Cells()) set.Add(c);
            }
            foreach (var it in Items)
            {
                if (it.Entry == null || !it.Entry.autotile) continue;
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
