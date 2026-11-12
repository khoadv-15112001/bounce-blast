using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GardenCannon.EditorTools
{
    /// <summary>
    /// The original item effects are not prefabs: they live in Spyke/Scenes/Game.unity under
    /// GridView/SharedParticleEmitter. This copies them out as prefabs (Assets/GardenCannon/VFX) so the game can play
    /// them: particle systems set to not play on awake and to scale with the hierarchy, stub scripts removed, and the
    /// root scaled so that a scale of 1 = 1 grid cell (the original GridView was scaled by 1.28).
    /// </summary>
    public static class VfxExtractor
    {
        public const string Folder = "Assets/GardenCannon/VFX";
        const string SourceScene = "Assets/Spyke/Scenes/Game.unity";

        // (object in the scene, optional child, prefab name)
        public static readonly (string node, string child, string prefab)[] Specs =
        {
            ("EggBoxFx", "Lids", "VFX_EggBox_Lid"),
            ("EggBoxFx", "Egg", "VFX_EggBox_Egg"),
            ("EggBoxFx", "Blast", "VFX_EggBox_Blast"),
            ("BlockBlastAnim", null, "VFX_Block_Blast"),
            ("WoodenBoxFx", "Blast", "VFX_WoodenBox_Blast"),
            ("TargetStandFx", "Blast", "VFX_TargetStand_Blast"),
            ("LogCutterFx", "Blast", "VFX_LogCutter_Blast"),
            ("BrickWallFx", "Blast", "VFX_BrickWall_Blast"),
            ("ContainerBlast", null, "VFX_Container_Blast"),
            ("ContainerHit", null, "VFX_Container_Hit"),
            ("ElectricOrbFx", null, "VFX_ElectricOrb"),
            ("BallSplashAnim", null, "VFX_Ball_Splash"),
        };

        public static string PathOf(string prefab) => $"{Folder}/{prefab}.prefab";

        public static bool AllExtracted() => Specs.All(s => File.Exists(PathOf(s.prefab)));

        [MenuItem("Garden Cannon/Extract Original VFX from Game.unity", priority = 42)]
        public static void Extract()
        {
            if (!File.Exists(SourceScene))
            {
                Debug.LogWarning("[GardenCannon] " + SourceScene + " not found, cannot extract VFX.");
                return;
            }
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/GardenCannon", "VFX");

            Scene scene = default;
            bool opened = false;
            var previousActive = SceneManager.GetActiveScene();
            try
            {
                scene = SceneManager.GetSceneByPath(SourceScene);
                if (!scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
                    opened = true;
                }
                // Temporary copies go into the source scene (closed without saving), not the user's open scene.
                SceneManager.SetActiveScene(scene);
                var roots = scene.GetRootGameObjects();
                var grid = FindDeep(roots, "GridView");
                float gridScale = grid != null ? grid.lossyScale.x : 1.28f;
                int saved = 0;
                foreach (var spec in Specs)
                {
                    var node = FindDeep(roots, spec.node);
                    if (node == null) { Debug.LogWarning($"[GardenCannon] VFX source '{spec.node}' not found."); continue; }
                    var src = spec.child != null ? node.Find(spec.child) : node;
                    if (src == null) { Debug.LogWarning($"[GardenCannon] VFX source '{spec.node}/{spec.child}' not found."); continue; }

                    var copy = Object.Instantiate(src.gameObject);
                    copy.name = spec.prefab;
                    copy.SetActive(true);
                    copy.transform.position = Vector3.zero;
                    copy.transform.rotation = src.rotation;
                    copy.transform.localScale = src.lossyScale / Mathf.Max(0.0001f, gridScale);

                    foreach (var mb in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
                    foreach (var ps in copy.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var main = ps.main;
                        main.playOnAwake = false;
                        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                        main.loop = false;
                    }
                    PrefabUtility.SaveAsPrefabAsset(copy, PathOf(spec.prefab));
                    Object.DestroyImmediate(copy);
                    saved++;
                }
                Debug.Log($"[GardenCannon] Extracted {saved} original VFX prefabs into {Folder}.");
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded && previousActive != scene) SceneManager.SetActiveScene(previousActive);
                if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
        }

        static Transform FindDeep(GameObject[] roots, string name)
        {
            foreach (var r in roots)
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            return null;
        }

        public static GameObject Load(string prefab) => AssetDatabase.LoadAssetAtPath<GameObject>(PathOf(prefab));
    }
}
