using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Swaps the sprite scenery for the art team's 3D models (Assets/3D) at runtime, without touching the scene:
    ///  - Cannon: the 3D cannon is parented under the aiming "Rail" (the round base hides the rotation), sprites hidden.
    ///  - BG: wooden fence + brick floor below the river; the model's origin is the cannon platform centre.
    ///  - Grass: checker lawn (1 square = 1 cell) laid over the board, rebuilt for each level size.
    /// The art is Y-up with -Z towards the top of the screen, hence <see cref="modelEuler"/> = (90, 180, 0).
    /// </summary>
    public class SceneArt3D : MonoBehaviour
    {
        public GameObject cannonPrefab;
        public GameObject bgPrefab;
        public GameObject grassPrefab;

        [Tooltip("Turns the Y-up models so their top faces the camera and their -Z points up the screen.")]
        public Vector3 modelEuler = new Vector3(90f, 180f, 0f);
        [Tooltip("BG position relative to the cannon pivot.")]
        public Vector3 bgOffset = Vector3.zero;
        [Tooltip("BG mesh without the white moves plate (optional).")]
        public Mesh bgMesh;
        [Tooltip("Size of the 3D cannon (the muzzle distance of CannonView follows).")]
        public float cannonScale = 0.85f;
        [Tooltip("Centre of the white plate for the moves counter, in BG model units (Unity mesh space).")]
        public Vector3 movesPlateLocal = new Vector3(3.81f, 0f, -0.065f);
        [Tooltip("Moves number size on the plate (1 = original size, which overflowed the plate).")]
        public float movesScale = 0.78f;
        [Tooltip("Extra shift of the moves number on its plate (world units).")]
        public Vector2 movesOffset = new Vector2(0f, -0.06f);
        [Tooltip("Lawn stops this far above the water line (the river's top edge).")]
        public float lawnAboveWater = -0.05f;
        [Tooltip("How far the lawn reaches under the frame of the GrassTop picture (its lawn area is transparent).")]
        public float lawnUnderFrame = 0.4f;
        [Tooltip("Drawn just after the lawn sprite (GrassTop is -8) and before every other sprite.")]
        public int lawnSortingOrder = -9;

        Board _board;
        float _muzzleBase = -1f;
        GameObject _cannon, _bg;
        MeshRenderer _lawn;
        Mesh _lawnMesh;
        Material _lawnMaterial;

        public void Apply(Board board, CannonView cannon, GameHud hud = null)
        {
            _board = board;
            if (cannon != null && cannonPrefab != null && _cannon == null) SwapCannon(cannon);
            if (bgPrefab != null && _bg == null)
            {
                SwapBottom(cannon);
                if (hud != null && hud.movesAnchor != null && !hud.movesBadgeTopLeft)
                {
                    // Put the moves number on the plate of the new BG (slightly in front of it).
                    Vector3 p = _bg.transform.TransformPoint(movesPlateLocal);
                    hud.movesAnchor.position = new Vector3(p.x + movesOffset.x, p.y + movesOffset.y, -0.05f);
                    hud.movesAnchor.localScale = Vector3.one * movesScale;
                }
            }
            RebuildLawn();
        }

        void SwapCannon(CannonView cannon)
        {
            var rail = cannon.barrel != null ? cannon.barrel : cannon.transform;
            foreach (var sr in rail.GetComponentsInChildren<SpriteRenderer>(true)) sr.enabled = false;
            _cannon = Instantiate(cannonPrefab, rail, false);
            _cannon.name = "Cannon3D";
            VisualUtil.StripRuntimeComponents(_cannon);
            _cannon.transform.localPosition = Vector3.zero;
            _cannon.transform.localRotation = Quaternion.Euler(modelEuler);
            _cannon.transform.localScale = Vector3.one * cannonScale;
            // Balls leave from the tip of the (smaller) barrel.
            if (_muzzleBase < 0f) _muzzleBase = cannon.muzzleDistance;
            cannon.muzzleDistance = _muzzleBase * cannonScale;
        }

        void SwapBottom(CannonView cannon)
        {
            // Hide the old bottom sprite (keep its children, e.g. the moves counter).
            foreach (var sr in FindObjectsOfType<SpriteRenderer>(true))
                if (sr.gameObject.name == "Bottom" &&
                    ((sr.transform.parent != null && sr.transform.parent.name == "GrassTheme") || (sr.sprite != null && sr.sprite.name.Contains("GrassBottom"))))
                    sr.enabled = false;   // its sprite may still be loading (first frame): match the object, not the sprite
            Vector3 pivot = cannon != null ? cannon.transform.position : new Vector3(0f, -10.54f, 0f);
            _bg = Instantiate(bgPrefab, transform, false);
            _bg.name = "BG3D";
            VisualUtil.StripRuntimeComponents(_bg);
            _bg.transform.position = new Vector3(pivot.x, pivot.y, 0f) + bgOffset;
            _bg.transform.rotation = Quaternion.Euler(modelEuler);
            if (bgMesh != null)
            {
                // Same BG without the old moves plate: the fence boards under it show through.
                var mf = _bg.GetComponentInChildren<MeshFilter>(true);
                if (mf != null) mf.sharedMesh = bgMesh;
            }
        }


        /// <summary>
        /// The moves number moved to the top-left badge: put fence boards (the bg.png plank band, same material and
        /// lighting as the BG) over the white plate it used to sit on, so the fence runs through.
        /// </summary>
        /// <summary>Checker lawn over the board, from its top down to the river. Called after each level build.</summary>
        public void RebuildLawn()
        {
            if (grassPrefab == null || _board == null) return;
            var src = grassPrefab.GetComponentInChildren<MeshRenderer>(true);
            if (src == null || src.sharedMaterial == null) return;
            if (_lawn == null)
            {
                var go = new GameObject("Lawn3D");
                go.transform.SetParent(transform, false);
                // A hair behind the GrassTop picture (z = 0): same depth made the frame's edge fight with the lawn.
                go.transform.position = new Vector3(0f, 0f, 0.02f);
                go.AddComponent<MeshFilter>().sharedMesh = _lawnMesh = new Mesh { name = "Lawn3D" };
                _lawn = go.AddComponent<MeshRenderer>();
                // Transparent queue + sorting order: after the river (-11), under the GrassTop picture (-8) whose lawn
                // area is transparent, so the frame's edge lies on top of the checker (no seam).
                _lawnMaterial = new Material(src.sharedMaterial) { name = "Lawn3D", renderQueue = 3000 };
                _lawn.sharedMaterial = _lawnMaterial;
                _lawn.sortingOrder = lawnSortingOrder;
                _lawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _lawn.receiveShadows = false;
            }
            int cols = Mathf.Max(1, Mathf.RoundToInt((_board.Right - _board.Left) / _board.CellSize));
            BuildLawnMesh(_lawnMesh, _board.Left, _board.Right, _board.Top, _board.WaterLine + lawnAboveWater, cols, lawnUnderFrame);
        }

        /// <summary>
        /// Checker lawn under the GrassTop picture (whose lawn is cut out): the board area with 1 UV unit = 2 cells
        /// from the grid's top-left corner, plus a margin under the frame where the outer squares just continue (no new
        /// square starting in the sliver between the grid and the frame). Bottom rows continue normally to the river.
        /// </summary>
        public static void BuildLawnMesh(Mesh mesh, float left, float right, float top, float bottom, int cols, float margin)
        {
            float cell = (right - left) / cols;
            const float eps = 0.002f;
            float[] xs = { left - margin, left, right, right + margin };
            float[] ys = { top + margin, top, bottom };
            float U(float x) => Mathf.Clamp((x - left) / (2f * cell), eps, cols / 2f - eps);
            float V(float y) => -Mathf.Max(eps, (top - y) / (2f * cell));   // 0 at the grid top, negative downwards
            var verts = new Vector3[xs.Length * ys.Length];
            var uvs = new Vector2[verts.Length];
            var normals = new Vector3[verts.Length];
            for (int j = 0; j < ys.Length; j++)
                for (int i = 0; i < xs.Length; i++)
                {
                    int n = j * xs.Length + i;
                    verts[n] = new Vector3(xs[i], ys[j], 0f);
                    uvs[n] = new Vector2(U(xs[i]), V(ys[j]));
                    normals[n] = Vector3.back;
                }
            var tris = new System.Collections.Generic.List<int>();
            for (int j = 0; j < ys.Length - 1; j++)
                for (int i = 0; i < xs.Length - 1; i++)
                {
                    int tl = j * xs.Length + i, tr = tl + 1, bl = tl + xs.Length, br = bl + 1;
                    // clockwise seen from the camera (-z)
                    tris.Add(bl); tris.Add(tl); tris.Add(tr);
                    tris.Add(bl); tris.Add(tr); tris.Add(br);
                }
            mesh.Clear();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (_lawnMesh != null) Destroy(_lawnMesh);
            if (_lawnMaterial != null) Destroy(_lawnMaterial);
        }
    }
}
