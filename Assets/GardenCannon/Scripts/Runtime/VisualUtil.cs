using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    public static class VisualUtil
    {
        static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");
        static MaterialPropertyBlock _mpb;

        // Average colour of each BlockColorAtlas tile (0 = yellow, then rows of 8), used for break particles.
        static readonly Color[] TileColors =
        {
            new Color(.95f,.85f,.10f),
            new Color(.95f,.50f,.05f), new Color(1f,.50f,.60f), new Color(1f,.25f,.50f), new Color(.40f,.10f,.90f),
            new Color(.75f,.05f,.95f), new Color(.85f,.15f,.12f), new Color(.95f,.72f,.47f), new Color(.92f,.88f,.87f),
            new Color(.43f,.25f,.10f), new Color(.10f,.55f,.05f), new Color(.10f,.35f,.10f), new Color(.10f,.55f,.20f),
            new Color(.60f,.85f,.20f), new Color(.25f,.28f,.35f), new Color(.55f,.57f,.68f), new Color(.95f,.42f,.05f),
            new Color(.05f,.20f,.85f), new Color(.12f,.12f,.13f), new Color(.00f,.05f,.55f), new Color(.30f,.80f,1f),
            new Color(.35f,.92f,.75f), new Color(.30f,.00f,.60f), new Color(.50f,.12f,.10f), new Color(.28f,.18f,.10f),
        };

        public static Color AtlasTileColor(int tile) => TileColors[Mathf.Clamp(tile, 0, TileColors.Length - 1)];

        /// <summary>Atlas tile index -> (column, row counted from the top).</summary>
        public static Vector2Int TileToColRow(int tile, int columns)
        {
            if (tile <= 0) return Vector2Int.zero;
            int k = tile - 1;
            return new Vector2Int(k % columns, 1 + k / columns);
        }

        /// <summary>
        /// Recolour every BlockColorAtlas mesh under <paramref name="root"/> by shifting its UVs to another atlas tile.
        /// </summary>
        public static void ApplyAtlasTile(Transform root, ItemCatalog catalog, int tile)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            int cols = Mathf.Max(1, catalog.atlasColumns), rows = Mathf.Max(1, catalog.atlasRows);
            var target = TileToColRow(tile, cols);
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mat = mr.sharedMaterial;
                if (mat == null || !mat.HasProperty("_MainTex") || mat.mainTexture == null) continue;
                if (!mat.mainTexture.name.Contains("BlockColorAtlas")) continue;

                if (catalog.atlasMeshFullUv)
                {
                    // Mesh UVs span 0..1 = one tile: squeeze them into the wanted tile.
                    mr.GetPropertyBlock(_mpb);
                    _mpb.SetVector(MainTexSt, new Vector4(1f / cols, 1f / rows, target.x / (float)cols, 1f - (target.y + 1f) / rows));
                    mr.SetPropertyBlock(_mpb);
                    continue;
                }

                Vector2 baseUv = catalog.atlasBaseUvCenter;
                if (baseUv.x < 0f)
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    baseUv = mf != null ? MeshUvCenter(mf.sharedMesh) : new Vector2(-1f, -1f);
                    if (baseUv.x < 0f) baseUv = new Vector2(0.5f / cols, 1f - 0.5f / rows); // assume tile 0
                }
                int baseCol = Mathf.FloorToInt(baseUv.x * cols);
                int baseRow = Mathf.FloorToInt((1f - baseUv.y) * rows);
                Vector2 scale = mat.mainTextureScale;
                Vector2 offset = mat.mainTextureOffset + new Vector2((target.x - baseCol) / (float)cols, -(target.y - baseRow) / (float)rows);
                mr.GetPropertyBlock(_mpb);
                _mpb.SetVector(MainTexSt, new Vector4(scale.x, scale.y, offset.x, offset.y));
                mr.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>Recolour BlockColorAtlas renderers (e.g. mesh particles of the block break effect) to one atlas tile.</summary>
        public static void ApplyAtlasTileToRenderers(Renderer[] renderers, ItemCatalog catalog, int tile)
        {
            if (renderers == null || catalog == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            int cols = Mathf.Max(1, catalog.atlasColumns), rows = Mathf.Max(1, catalog.atlasRows);
            var target = TileToColRow(tile, cols);
            var st = new Vector4(1f / cols, 1f / rows, target.x / (float)cols, 1f - (target.y + 1f) / rows);
            foreach (var r in renderers)
            {
                var mat = r != null ? r.sharedMaterial : null;
                if (mat == null || !mat.HasProperty("_MainTex") || mat.mainTexture == null) continue;
                if (!mat.mainTexture.name.Contains("BlockColorAtlas")) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetVector(MainTexSt, st);
                r.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>Centre of the mesh UV bounds (wrapped into 0..1), or (-1,-1) when unreadable.</summary>
        public static Vector2 MeshUvCenter(Mesh mesh)
        {
            if (mesh == null) return new Vector2(-1f, -1f);
#if !UNITY_EDITOR
            if (!mesh.isReadable) return new Vector2(-1f, -1f);
#endif
            var uvs = new List<Vector2>();
            try { mesh.GetUVs(0, uvs); }
            catch { return new Vector2(-1f, -1f); }
            if (uvs.Count == 0) return new Vector2(-1f, -1f);
            Vector2 min = uvs[0], max = uvs[0];
            foreach (var uv in uvs) { min = Vector2.Min(min, uv); max = Vector2.Max(max, uv); }
            Vector2 c = (min + max) * 0.5f;
            return new Vector2(Mathf.Repeat(c.x, 1f), Mathf.Repeat(c.y, 1f));
        }

        /// <summary>
        /// The original item views keep both orientations as inactive children
        /// ("..._horizontal" / "..._vertical"). Enable the one we need.
        /// </summary>
        public static void ApplyOrientation(Transform root, bool vertical)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.EndsWith("_horizontal")) t.gameObject.SetActive(!vertical);
                else if (n.EndsWith("_vertical")) t.gameObject.SetActive(vertical);
            }
        }

        /// <summary>Stop the original (stub) behaviours and 2D physics on a visual so they cannot interfere.</summary>
        public static void StripRuntimeComponents(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>(true)) rb.simulated = false;
        }

        public static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, Vector3 localPos, float worldWidth, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            if (sprite != null && worldWidth > 0f)
            {
                float w = sprite.bounds.size.x;
                if (w > 0f) go.transform.localScale = Vector3.one * (worldWidth / w);
            }
            return sr;
        }

        public static TextMesh MakeText(string name, Transform parent, Font font, Vector3 localPos, float height, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, int order = 100)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            // TextMesh line height ~= fontSize * characterSize / 10 world units.
            tm.characterSize = height / (tm.fontSize * 0.1f);
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sortingOrder = order;
            return tm;
        }
    }
}
