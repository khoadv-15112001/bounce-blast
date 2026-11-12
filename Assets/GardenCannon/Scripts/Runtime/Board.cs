using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Owns the level grid inside the lawn. World units: the lawn is <see cref="lawnWidth"/> wide,
    /// centred on x = 0, and its bottom edge (the water line) is at y = 0.
    /// </summary>
    public class Board : MonoBehaviour
    {
        [Header("Lawn (world units)")]
        public float lawnWidth = 9f;
        public float lawnTop = 13.19f;
        [Tooltip("Space kept between the lawn top and the first grid row.")]
        public float topMargin = 0f;

        public float CellSize { get; private set; } = 1f;
        public LevelData Level { get; private set; }
        public readonly List<BoardItem> Items = new List<BoardItem>();

        public float Left => -lawnWidth * 0.5f;
        public float Right => lawnWidth * 0.5f;
        public float Top => lawnTop;
        public float WaterLine => 0f;

        Vector2 _gridOrigin; // world position of the bottom-left corner of cell (0,0)

        public Vector2 GridToWorldCorner(Vector2 grid) => _gridOrigin + grid * CellSize;
        public Vector2 GridToWorldCentre(Vector2 grid) => _gridOrigin + (grid + new Vector2(0.5f, 0.5f)) * CellSize;

        public void Build(LevelData level, ItemCatalog catalog, Font font)
        {
            Clear();
            Level = level;
            CellSize = lawnWidth / Mathf.Max(1, level.width);
            float gridHeight = level.height * CellSize;
            float bottom = Mathf.Max(0.3f, lawnTop - topMargin - gridHeight);
            _gridOrigin = new Vector2(Left, bottom);

            var missing = new HashSet<int>();
            foreach (var data in level.items)
            {
                var entry = catalog.Get(data.type);
                if (entry == null) missing.Add(data.type);
                if (entry != null && entry.kind == ItemKind.Ignore && entry.prefab == null) continue;

                var go = new GameObject($"Item_{data.type}_{data.x}_{data.y}");
                go.transform.SetParent(transform, false);
                var item = go.AddComponent<BoardItem>();
                item.Init(this, data, entry ?? FallbackEntry(data.type), catalog, font);
                Items.Add(item);
            }
            foreach (var t in missing)
                Debug.LogWarning($"[GardenCannon] Level {level.id}: item type {t} is not in the ItemCatalog, treated as indestructible.");
        }

        static ItemCatalog.Entry FallbackEntry(int type) => new ItemCatalog.Entry
        {
            label = "Unknown " + type, typeId = type, kind = ItemKind.Indestructible, debugColor = Color.magenta,
        };

        public void Clear()
        {
            foreach (var it in Items)
                if (it != null) Destroy(it.gameObject);
            Items.Clear();
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < Items.Count; i++) Items[i].Tick(dt);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(new Vector3(0, lawnTop * 0.5f), new Vector3(lawnWidth, lawnTop));
        }
    }
}
