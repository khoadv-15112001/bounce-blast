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
        public float cannonScale = 1f;
        [Tooltip("Centre of the white plate for the moves counter, in BG model units (Unity mesh space).")]
        public Vector3 movesPlateLocal = new Vector3(3.81f, 0f, -0.065f);
        [Tooltip("Moves number size on the plate (1 = original size, which overflowed the plate).")]
        public float movesScale = 0.78f;
        [Tooltip("Extra shift of the moves number on its plate (world units).")]
        public Vector2 movesOffset = new Vector2(0f, -0.06f);
        [Tooltip("Lawn stops this far above the water line (the river's top edge).")]
        public float lawnAboveWater = 0.2f;
        [Tooltip("Drawn just after the lawn sprite (GrassTop is -8) and before every other sprite.")]
        public int lawnSortingOrder = -7;

        Board _board;
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
                if (hud != null && hud.movesAnchor != null)
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
        }

        void SwapBottom(CannonView cannon)
        {
            // Hide the old bottom sprite (keep its children, e.g. the moves counter).
            foreach (var sr in FindObjectsOfType<SpriteRenderer>(true))
                if (sr.gameObject.name == "Bottom" && sr.sprite != null && sr.sprite.name.Contains("GrassBottom")) sr.enabled = false;
            Vector3 pivot = cannon != null ? cannon.transform.position : new Vector3(0f, -10.54f, 0f);
            _bg = Instantiate(bgPrefab, transform, false);
            _bg.name = "BG3D";
            VisualUtil.StripRuntimeComponents(_bg);
            _bg.transform.position = new Vector3(pivot.x, pivot.y, 0f) + bgOffset;
            _bg.transform.rotation = Quaternion.Euler(modelEuler);
        }

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
                go.AddComponent<MeshFilter>().sharedMesh = _lawnMesh = new Mesh { name = "Lawn3D" };
                _lawn = go.AddComponent<MeshRenderer>();
                // Transparent queue + sorting order so it sits on the lawn sprite but under every other sprite.
                _lawnMaterial = new Material(src.sharedMaterial) { name = "Lawn3D", renderQueue = 3000 };
                _lawn.sharedMaterial = _lawnMaterial;
                _lawn.sortingOrder = lawnSortingOrder;
                _lawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _lawn.receiveShadows = false;
            }
            float cell = _board.CellSize;
            float left = _board.Left, right = _board.Right, top = _board.Top;
            float bottom = _board.WaterLine + lawnAboveWater;
            // The texture holds 2x2 checker squares: 1 UV unit = 2 cells, aligned to the grid's top-left corner.
            float u1 = (right - left) / (2f * cell), v0 = -(top - bottom) / (2f * cell);
            _lawnMesh.Clear();
            _lawnMesh.vertices = new[]
            {
                new Vector3(left, bottom, 0f), new Vector3(right, bottom, 0f), new Vector3(right, top, 0f), new Vector3(left, top, 0f),
            };
            _lawnMesh.uv = new[] { new Vector2(0f, v0), new Vector2(u1, v0), new Vector2(u1, 0f), new Vector2(0f, 0f) };
            _lawnMesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            _lawnMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };   // clockwise seen from the camera (-z)
            _lawnMesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (_lawnMesh != null) Destroy(_lawnMesh);
            if (_lawnMaterial != null) Destroy(_lawnMaterial);
        }
    }
}
