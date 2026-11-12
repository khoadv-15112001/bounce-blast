using System;
using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>How an item reacts to balls.</summary>
    public enum ItemKind
    {
        /// <summary>Takes damage from balls and disappears at 0 HP.</summary>
        Breakable,
        /// <summary>Solid, never breaks (stone).</summary>
        Indestructible,
        /// <summary>Solid right triangle (stone corner). Variant 0..3 = corner BL, BR, TR, TL.</summary>
        Wedge,
        /// <summary>Round, never breaks, kicks the ball away a bit faster.</summary>
        Bumper,
        /// <summary>Not solid. Ball passes through and damages it once per contact.</summary>
        PassThrough,
        /// <summary>Ignored by physics and goals (decoration / unknown).</summary>
        Ignore,
    }

    public enum ItemShape
    {
        /// <summary>Axis-aligned rectangles built from the footprint cells.</summary>
        Cells,
        /// <summary>Circle inscribed in the footprint bounds.</summary>
        Circle,
    }

    /// <summary>
    /// Maps level item type ids (from the original data) to behaviour + prefab.
    /// The mapping of the rarer ids was inferred from the data (footprints, goals, lanes),
    /// so every entry is editable in the inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Garden Cannon/Item Catalog", fileName = "ItemCatalog")]
    public class ItemCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string label = "Item";
            public int typeId;
            public ItemKind kind = ItemKind.Breakable;
            public ItemShape shape = ItemShape.Cells;
            [Tooltip("Hits needed when the level gives no HP.")]
            public int defaultHits = 1;
            [Tooltip("Visual prefab. The original item views are used as-is (their scripts are stubs).")]
            public GameObject prefab;
            [Tooltip("Uniform scale applied on top of the cell size.")]
            public float visualScale = 1f;
            [Tooltip("Scale (uniformly) and centre the prefab's meshes so they fill the item footprint. Use for multi-cell items.")]
            public bool fitFootprint;
            [Range(0.5f, 1.1f)] public float fitFill = 0.96f;
            [Tooltip("Extra Z rotation (degrees) applied to the prefab.")]
            public float rotationOffset;
            [Tooltip("Wedges: rotate the prefab to the variant's corner (0 TL, 1 TR, 2 BR, 3 BL). The mesh's own right angle is bottom-right.")]
            public bool rotateByVariant;
            [Tooltip("Children named <prefix>N show the remaining hits N (e.g. Wooden Box stage_1..stage_4). Empty = off.")]
            public string stagePrefix = "";
            [Tooltip("Pick the mesh of the child named 'Tile' from autotileModules by same-type neighbours (Brick Wall).")]
            public bool autotile;
            [Tooltip("Recolour BlockColorAtlas meshes using the item variant.")]
            public bool colorByVariant;
            [Tooltip("Show remaining hits as a number on the item.")]
            public bool showHitCount;
            [Tooltip("Lane speed in cells per second for moving items.")]
            public float laneSpeed = 1.5f;
            public GameObject breakFx;
            public AudioClip hitSfx;
            public AudioClip breakSfx;
            [Tooltip("Colour used for debug gizmos / goal fallback icon.")]
            public Color debugColor = Color.white;
        }

        public List<Entry> entries = new List<Entry>();

        [Tooltip("Version of the built-in defaults this catalog was generated from (Setup rebuilds older ones).")]
        public int defaultsVersion;

        /// <summary>Autotile module: a mesh and the sides (bits T=1, R=2, B=4, L=8) that show a brick rim at rotation 0.</summary>
        [Serializable]
        public class AutotileModule
        {
            public Mesh mesh;
            public int rimMask;
        }

        [Header("Brick wall autotile (filled by Setup from the original module meshes)")]
        public List<AutotileModule> autotileModules = new List<AutotileModule>();

        [Header("Damage")]
        [Tooltip("Level HP values are in damage units. Each ball hit removes this many.")]
        public int damagePerHit = 5;

        [Header("Block colours (BlockColorAtlas: 1 tile on the first row, then 3 rows of 8)")]
        [Tooltip("Variant -> atlas tile. Tile 0 = top-left yellow, tiles 1..24 = rows 2..4 left to right.")]
        public int[] variantToAtlasTile = DefaultVariantTiles();
        [Tooltip("Centre of the block mesh UVs inside the atlas (filled by Garden Cannon > Setup). Negative = unknown, read from the mesh at runtime (editor only) or assume tile 0.")]
        public Vector2 atlasBaseUvCenter = new Vector2(-1f, -1f);
        [Tooltip("The block mesh UVs cover the whole 0..1 range (one tile layout); the tile is picked by scaling UVs into the atlas.")]
        public bool atlasMeshFullUv = true;
        public int atlasColumns = 8;
        public int atlasRows = 4;

        Dictionary<int, Entry> _lookup;

        public Entry Get(int typeId)
        {
            if (_lookup == null || _lookup.Count != entries.Count)
            {
                _lookup = new Dictionary<int, Entry>();
                foreach (var e in entries)
                    if (e != null) _lookup[e.typeId] = e;
            }
            return _lookup.TryGetValue(typeId, out var entry) ? entry : null;
        }

        public int AtlasTileForVariant(int variant)
        {
            if (variantToAtlasTile != null && variant >= 0 && variant < variantToAtlasTile.Length)
                return variantToAtlasTile[variant];
            return Mathf.Clamp(variant, 0, 24);
        }

        public static int[] DefaultVariantTiles()
        {
            // Variant 0 (no colour in data) -> blue. Others are 1-based tile numbers.
            var t = new int[32];
            t[0] = 17;
            for (int i = 1; i < t.Length; i++) t[i] = Mathf.Clamp(i - 1, 0, 24);
            return t;
        }

        void OnValidate() => _lookup = null;
    }
}
