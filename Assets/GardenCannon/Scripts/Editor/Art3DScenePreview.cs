using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GardenCannon.EditorTools
{
    /// <summary>
    /// Shows the 3D scenery (cannon, BG, checker lawn) in the GardenCannon scene outside Play mode, like
    /// <see cref="SceneArt3D"/> does at runtime. Nothing is saved into the scene: the preview objects are DontSave and the
    /// replaced sprites are only hidden with Renderer.forceRenderingOff (not serialized).
    /// </summary>
    [InitializeOnLoad]
    public static class Art3DScenePreview
    {
        const string ScenePath = "Assets/GardenCannon/Scenes/GardenCannon.unity";
        const string Elements = "Assets/3D/Prefab/Elements/";
        const string RootName = "__Art3DPreview";
        static readonly Vector3 ModelEuler = new Vector3(90f, 180f, 0f);

        static readonly List<GameObject> _spawned = new List<GameObject>();
        static readonly List<Renderer> _hidden = new List<Renderer>();
        static Material _lawnMat;
        static Mesh _lawnMesh;

        static Art3DScenePreview()
        {
            EditorSceneManager.sceneOpened += (s, m) => Refresh();
            EditorSceneManager.sceneClosing += (s, r) => Clear();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Clear();
                else if (state == PlayModeStateChange.EnteredEditMode) Refresh();
            };
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.delayCall += Refresh;
        }

        [MenuItem("Garden Cannon/Refresh 3D Scene Preview", priority = 60)]
        static void Refresh()
        {
            Clear();
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) return;

            var cannonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Elements + "Cannon.prefab");
            var bgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Elements + "BG.prefab");
            var grassPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Elements + "grass.prefab");
            if (cannonPrefab == null && bgPrefab == null && grassPrefab == null) return;

            CannonView cannon = null;
            Board board = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (cannon == null) cannon = root.GetComponentInChildren<CannonView>(true);
                if (board == null) board = root.GetComponentInChildren<Board>(true);
            }

            var previewRoot = Spawn(new GameObject(RootName), scene);

            if (cannon != null && cannonPrefab != null)
            {
                var rail = cannon.barrel != null ? cannon.barrel : cannon.transform;
                foreach (var sr in rail.GetComponentsInChildren<SpriteRenderer>(true)) Hide(sr);
                var c = Spawn((GameObject)Object.Instantiate(cannonPrefab), scene);
                c.transform.SetParent(previewRoot.transform, false);
                c.transform.position = rail.position;
                c.transform.rotation = rail.rotation * Quaternion.Euler(ModelEuler);
            }

            Vector3 pivot = cannon != null ? cannon.transform.position : new Vector3(0f, -10.54f, 0f);
            if (bgPrefab != null)
            {
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                        if (sr.gameObject.name == "Bottom" && sr.sprite != null && sr.sprite.name.Contains("GrassBottom")) Hide(sr);
                var bg = Spawn((GameObject)Object.Instantiate(bgPrefab), scene);
                bg.transform.SetParent(previewRoot.transform, false);
                bg.transform.position = new Vector3(pivot.x, pivot.y, 0f);
                bg.transform.rotation = Quaternion.Euler(ModelEuler);
            }

            if (grassPrefab != null && board != null) BuildLawn(previewRoot, grassPrefab, board);
        }

        /// <summary>Same lawn as SceneArt3D.RebuildLawn for a 9-column board.</summary>
        static void BuildLawn(GameObject parent, GameObject grassPrefab, Board board)
        {
            var src = grassPrefab.GetComponentInChildren<MeshRenderer>(true);
            if (src == null || src.sharedMaterial == null) return;
            _lawnMesh = new Mesh { name = "Lawn3DPreview", hideFlags = HideFlags.DontSave };
            SceneArt3D.BuildLawnMesh(_lawnMesh, board.Left, board.Right, board.Top, board.WaterLine - 0.05f, 9, 0.4f);
            _lawnMat = new Material(src.sharedMaterial) { name = "Lawn3DPreview", renderQueue = 3000, hideFlags = HideFlags.DontSave };

            var go = new GameObject("Lawn3D");
            go.transform.SetParent(parent.transform, false);
            go.transform.position = new Vector3(0f, 0f, 0.02f);
            go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            go.AddComponent<MeshFilter>().sharedMesh = _lawnMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _lawnMat;
            mr.sortingOrder = -9;   // under the GrassTop picture (transparent lawn)
        }

        static GameObject Spawn(GameObject go, Scene scene)
        {
            SceneManager.MoveGameObjectToScene(go, scene);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            _spawned.Add(go);
            return go;
        }

        static void Hide(Renderer r)
        {
            if (r == null || r.forceRenderingOff) return;
            r.forceRenderingOff = true;
            _hidden.Add(r);
        }

        static void Clear()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var r in _hidden) if (r != null) r.forceRenderingOff = false;
            _hidden.Clear();
            if (_lawnMesh != null) Object.DestroyImmediate(_lawnMesh);
            if (_lawnMat != null) Object.DestroyImmediate(_lawnMat);
            _lawnMesh = null; _lawnMat = null;
        }
    }
}
