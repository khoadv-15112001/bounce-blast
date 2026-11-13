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
        /// <summary>Unbreakable rotating arms (twirler). Variant 0/1 = 4 arms, 2/3 = 2 arms; even = counter-clockwise.</summary>
        Spinner,
        /// <summary>Not solid. Ball passes through and damages it once per contact.</summary>
        PassThrough,
        /// <summary>Ignored by physics and goals (decoration / unknown).</summary>
        Ignore,
    }

    /// <summary>
    /// Special obstacle logic on top of <see cref="ItemKind"/>. The original scripts were not recovered, so these rules
    /// are guesses from the prefabs, sounds and level data; every number is editable in the ItemCatalog.
    /// </summary>
    public enum ItemBehaviour
    {
        Default,
        /// <summary>Yellow crate over other items: they are hidden and can't be hit until its HP badge reaches 0.</summary>
        Container,
        /// <summary>Hit 1 cracks, hit 2 exposes the dynamite, hit 3 lights the fuse: it explodes and hits everything around.</summary>
        BrickWall,
        /// <summary>Machine at one end of a row of logs (variant 0 right, 1 left, 2 bottom, 3 top). Each hit feeds and cuts one log; the machine breaks after the last one.</summary>
        LogCutter,
        /// <summary>One orb per cell. A hit switches an orb on; when all are on they discharge and the item breaks. Orbs switch off again if the chain stalls.</summary>
        ElectricOrb,
        /// <summary>One crate per cell, each with its own hits; the item is done when every crate is broken.</summary>
        WoodenBox,
        /// <summary>The target shows its broken mesh after the first hit (Target Stand / Moving Target).</summary>
        TargetStand,
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
            [Tooltip("Stages are cumulative: child N stays visible while N <= hits left (Egg Box eggs).")]
            public bool stagesCumulative;
            [Tooltip("Egg box: first hit opens the lid (child 'Doors'), every following hit hatches the egg nearest to the ball (children Egg01..Egg09) and a bird flies away. Hits = 1 + eggs.")]
            public bool eggBox;
            [Tooltip("Egg box: sound when the lid opens.")]
            public AudioClip openSfx;
            [Tooltip("Spinner: degrees per second.")]
            public float spinSpeed = 90f;
            [Tooltip("Pick the mesh of the child named 'Tile' from autotileModules by same-type neighbours (Brick Wall).")]
            public bool autotile;
            [Tooltip("Recolour BlockColorAtlas meshes using the item variant.")]
            public bool colorByVariant;
            [Tooltip("Show remaining hits as a number on the item.")]
            public bool showHitCount;
            [Tooltip("Lane speed in cells per second for moving items.")]
            public float laneSpeed = 1.5f;
            [Tooltip("Original effect played when the item breaks (Assets/GardenCannon/VFX).")]
            public GameObject breakFx;
            [Tooltip("Original effect played when a hit doesn't break the item (e.g. Container).")]
            public GameObject hitFx;
            [Tooltip("Egg box: effect when the lid opens.")]
            public GameObject openFx;
            [Tooltip("Egg box: effect when an egg hatches.")]
            public GameObject hatchFx;
            public AudioClip hitSfx;
            public AudioClip breakSfx;
            [Tooltip("Optional second sound on break (e.g. egg box bird flying away).")]
            public AudioClip extraBreakSfx;
            [Header("3D art (Assets/3D)")]
            [Tooltip("Rotation applied to the model before anything else. The new Y-up models need (-90, 0, 0) so their top faces the camera.")]
            public Vector3 modelEuler;
            [Tooltip("Blocks: one prefab per colour; the one closest to the variant's palette colour is used (overrides Prefab).")]
            public ColorPrefab[] colorPrefabs = new ColorPrefab[0];
            [Tooltip("Prefabs per size, picked by the name suffix: _N = length / arms (Slider_3, Spinner_4), _WxH = footprint (Wooden_Crate_2x3).")]
            public GameObject[] sizePrefabs = new GameObject[0];
            [Tooltip("Spinner: extra angle between the model's arms and the physics arms (new art: 45).")]
            public float spinVisualOffset;

            [Header("Obstacle behaviour (guessed rules, tweak freely)")]
            public ItemBehaviour behaviour = ItemBehaviour.Default;
            [Tooltip("Wooden Box: hits per crate (cell).")]
            public int cellHits = 2;
            [Tooltip("Brick Wall: seconds the fuse burns before the dynamite explodes.")]
            public float fuseTime = 0.7f;
            [Tooltip("Brick Wall: explosion radius in cells (1.5 = the 8 neighbours).")]
            public float blastRadius = 1.5f;
            [Tooltip("Electric Orb: seconds without a new orb switching on before all orbs switch off again (0 = never).")]
            public float orbResetTime = 8f;
            [Tooltip("Sound when one part breaks (a crate, a log being cut).")]
            public AudioClip partBreakSfx;
            [Tooltip("Effect when one part breaks (a crate).")]
            public GameObject partFx;
            [Tooltip("Extra sound (Electric Orb: orbs switching off).")]
            public AudioClip altSfx;
            [Tooltip("Colour used for debug gizmos / goal fallback icon.")]
            public Color debugColor = Color.white;
        }

        [Serializable]
        public class ColorPrefab
        {
            public string name;
            public Color color = Color.white;
            public GameObject prefab;
        }

        public List<Entry> entries = new List<Entry>();

        [Tooltip("Version of the built-in defaults this catalog was generated from (Setup rebuilds older ones).")]
        public int defaultsVersion;
        [Tooltip("Version of the obstacle behaviour defaults applied to the entries (editor auto-fix).")]
        public int behaviourVersion;

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
        public int damagePerHit = 1;

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

        /// <summary>
        /// Palette order of the original game, checked against the gameplay video (v0 blue, v1 black, v13 slate,
        /// v15 orange, v22 red, v23 beige, v25 yellow): the palette starts at the BOTTOM-left atlas tile and goes
        /// row by row upwards (index 16 is unused, 17..24 = second row from the top, 25 = yellow).
        /// Returned values are tile numbers counted from the top (0 = yellow, 1..8 = 2nd row, 9..16 = 3rd, 17..24 = bottom).
        /// </summary>
        public static int[] DefaultVariantTiles()
        {
            var t = new int[32];
            for (int v = 0; v < t.Length; v++)
            {
                if (v < 8) t[v] = 17 + v;               // bottom row: blue, black, navy, light blue, mint, purple, maroon, dark brown
                else if (v < 16) t[v] = v + 1;          // 3rd row: brown, green, dark green, green, lime, slate, grey, orange
                else if (v == 16) t[v] = 1;             // unused in the data, orange
                else if (v <= 24) t[v] = v - 16;        // 2nd row: orange, pink, hot pink, indigo, purple, red, beige, white
                else t[v] = 0;                          // 25+: yellow
            }
            return t;
        }

        void OnValidate() => _lookup = null;
    }
}
