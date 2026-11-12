using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GardenCannon.EditorTools
{
    /// <summary>
    /// Garden Cannon > Setup: creates the ItemCatalog (original prefabs, SFX and FX) and builds a playable scene
    /// from the original art (GrassTop / Water / GrassBottom / Cannon sprites).
    /// </summary>
    public static class GardenCannonSetup
    {
        const string Root = "Assets/GardenCannon";
        const string CatalogPath = Root + "/Data/ItemCatalog.asset";
        const string ScenePath = Root + "/Scenes/GardenCannon.unity";

        // Background measurements (pixels in the exported sprites).
        const float LawnLeftPx = 335f, LawnRightPx = 1372f, LawnTopPx = 528f; // GrassTop 1697x2048
        const float GrassTopW = 1697f, GrassTopH = 2048f;
        const float BottomW = 1905f, BottomH = 647f;                         // GrassBottom
        const float CannonPivotXPx = 955f, CannonPivotYPx = 165f;
        const float MovesBoxXPx = 612f, MovesBoxYPx = 112f;

        [MenuItem("Garden Cannon/Setup (catalog + scene)", priority = 0)]
        public static void SetupAll()
        {
            var catalog = CreateOrUpdateCatalog();
            BuildScene(catalog);
        }

        [MenuItem("Garden Cannon/Create or Update Item Catalog", priority = 1)]
        public static void CatalogOnly() => Selection.activeObject = CreateOrUpdateCatalog();

        [MenuItem("Garden Cannon/Open Game Scene", priority = 2)]
        public static void OpenScene()
        {
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else SetupAll();
        }

        // ------------------------------------------------------------------ catalog

        struct Def
        {
            public int id; public string label; public ItemKind kind; public string prefab;
            public int hits; public string hit, brk, fx; public bool color, rotate, count, fit;
            public float speed; public Color debug; public ItemShape shape;
        }

        static Def D(int id, string label, ItemKind kind, string prefab, string hit = null, string brk = null) =>
            new Def { id = id, label = label, kind = kind, prefab = prefab, hits = 1, hit = hit, brk = brk, speed = 1.5f, debug = Color.white };

        /// <summary>
        /// Type ids come from the original level data. 1/26/23/25/4/5/24 were matched against the demo and
        /// the data (footprints, goals, lanes); 2/6/7/8/9/13 are best guesses - edit the asset if needed.
        /// </summary>
        static IEnumerable<Def> Defaults()
        {
            var d = D(1, "Block", ItemKind.Breakable, "BlockItemView", "ball_collide", "block_break"); d.color = true; d.debug = Color.cyan; yield return d;
            d = D(26, "Brick Wall", ItemKind.Breakable, "BrickWallItemView", "brick_wall_hit", "brick_wall_explode"); d.fx = "BrickWallBlast"; d.debug = new Color(1f, .5f, .2f); yield return d;
            d = D(23, "Stone Corner", ItemKind.Wedge, "HalfUnbreakableBlockItemView", "unbreakable_hit"); d.rotate = true; d.debug = Color.gray; yield return d;
            d = D(25, "Stone", ItemKind.Indestructible, "UnbreakableBlockItemView", "unbreakable_hit"); d.fit = true; d.debug = Color.gray; yield return d;
            d = D(4, "Moving Barrier", ItemKind.Indestructible, "MovingBarrierItemView", "moving_barrier_hit"); d.fit = true; d.speed = 1.6f; d.debug = Color.red; yield return d;
            d = D(5, "Target Stand", ItemKind.Breakable, "TargetStandItemView", "target_hit", "target_stand_collapse"); d.hits = 3; d.fit = true; d.debug = Color.magenta; yield return d;
            d = D(24, "Container", ItemKind.Breakable, "ContainerItemView", "container_hit", "container_burst"); d.count = true; d.fit = true; d.debug = Color.yellow; yield return d;
            d = D(9, "Moving Target", ItemKind.Breakable, "TargetItemView", "target_hit", "target_break"); d.speed = 1.2f; d.debug = Color.magenta; yield return d;
            d = D(7, "Log Cutter", ItemKind.Breakable, "LogCutterItemView", "log_cut", "log_cutter_break"); d.hits = 2; d.fit = true; d.debug = new Color(.6f, .4f, .2f); yield return d;
            d = D(8, "Wooden Box", ItemKind.Breakable, "WoodenBoxItemView", "wooden_box_hit", "wooden_box_break"); d.hits = 4; d.debug = new Color(.7f, .5f, .3f); yield return d;
            d = D(13, "Egg Box", ItemKind.Breakable, "EggBoxItemView", "egg_crack", "egg_box_break"); d.hits = 3; d.fit = true; d.debug = Color.white; yield return d;
            d = D(6, "Electric Orb", ItemKind.Breakable, "ElectricOrbItemView", "orb_on", "orb_discharge"); d.hits = 2; d.fit = true; d.debug = Color.blue; yield return d;
            d = D(2, "Rising Bumper", ItemKind.Bumper, "RisingBumperItemView", "rising_bumper_bounce"); d.shape = ItemShape.Circle; d.debug = Color.green; yield return d;
        }

        public static ItemCatalog CreateOrUpdateCatalog()
        {
            EnsureFolder(Root + "/Data");
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            bool created = catalog == null;
            if (created)
            {
                catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            foreach (var def in Defaults())
            {
                var e = catalog.entries.FirstOrDefault(x => x.typeId == def.id);
                if (e != null && !created)
                {
                    // Keep user edits; only fill references that are still empty.
                    if (e.prefab == null) e.prefab = LoadPrefab(def.prefab);
                    if (e.hitSfx == null) e.hitSfx = LoadClip(def.hit);
                    if (e.breakSfx == null) e.breakSfx = LoadClip(def.brk);
                    if (e.breakFx == null) e.breakFx = LoadPrefab(def.fx);
                    continue;
                }
                catalog.entries.Add(new ItemCatalog.Entry
                {
                    label = def.label, typeId = def.id, kind = def.kind, shape = def.shape, defaultHits = def.hits,
                    prefab = LoadPrefab(def.prefab), hitSfx = LoadClip(def.hit), breakSfx = LoadClip(def.brk), breakFx = LoadPrefab(def.fx),
                    colorByVariant = def.color, rotateByVariant = def.rotate, showHitCount = def.count, fitFootprint = def.fit,
                    laneSpeed = def.speed, debugColor = def.debug,
                });
            }
            catalog.entries.Sort((a, b) => a.typeId.CompareTo(b.typeId));

            // Where do the block mesh UVs sit inside BlockColorAtlas? (needed to recolour blocks)
            var block = LoadPrefab("BlockItemView");
            if (block != null)
            {
                foreach (var mr in block.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var tex = mr.sharedMaterial != null && mr.sharedMaterial.HasProperty("_MainTex") ? mr.sharedMaterial.mainTexture : null;
                    if (tex == null || !tex.name.Contains("BlockColorAtlas")) continue;
                    var c = VisualUtil.MeshUvCenter(mr.GetComponent<MeshFilter>()?.sharedMesh);
                    if (c.x >= 0f) { catalog.atlasBaseUvCenter = c; catalog.atlasMeshFullUv = Mathf.Abs(c.x - 0.5f) < 0.1f && Mathf.Abs(c.y - 0.5f) < 0.1f; }
                    break;
                }
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GardenCannon] Item catalog ready: {CatalogPath} ({catalog.entries.Count} types, block UV centre {catalog.atlasBaseUvCenter})");
            return catalog;
        }

        // ------------------------------------------------------------------ scene

        public static void BuildScene(ItemCatalog catalog)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            float wpp = 9f / (LawnRightPx - LawnLeftPx);               // world units per GrassTop pixel
            float screenW = GrassTopW * wpp;                            // ~14.73
            float lawnTop = (GrassTopH - LawnTopPx) * wpp;              // ~13.19
            float bottomWpp = screenW / BottomW;
            float bottomTop = -1.95f;

            // Camera
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.91f, 0.87f, 0.79f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camGo.transform.position = new Vector3(0f, 5.4f, -50f);
            camGo.AddComponent<AudioListener>();
            var fit = camGo.AddComponent<CameraFit>();
            float bottomEdge = bottomTop - BottomH * bottomWpp;
            float topEdge = GrassTopH * wpp;
            fit.content = new Rect(-screenW * 0.5f, bottomEdge, screenW, topEdge - bottomEdge);

            // Light for the 3D item meshes (the original shaders are unlit, this only helps standard materials).
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(30f, -20f, 0f);

            // Background
            var env = new GameObject("Environment").transform;
            var grassTop = LoadSprite("GrassTop");
            var lawn = VisualUtil.MakeSprite("GrassTop", env, grassTop, new Vector3((GrassTopW * 0.5f - (LawnLeftPx + LawnRightPx) * 0.5f) * wpp, GrassTopH * 0.5f * wpp, 20f), screenW, -100);
            var water = LoadSprite("Water");
            if (water != null)
            {
                float h = water.bounds.size.y / water.bounds.size.x * (screenW + 0.4f);
                VisualUtil.MakeSprite("Water", env, water, new Vector3(0f, 0.05f - h * 0.5f, 21f), screenW + 0.4f, -102);
            }
            var bottom = LoadSprite("GrassBottom");
            float bottomH = BottomH * bottomWpp;
            VisualUtil.MakeSprite("GrassBottom", env, bottom, new Vector3(0f, bottomTop - bottomH * 0.5f, 19f), screenW, -99);

            // Board
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<Board>();
            board.lawnWidth = 9f;
            board.lawnTop = lawnTop;

            // Cannon
            var pivot = new Vector3((CannonPivotXPx - BottomW * 0.5f) * bottomWpp, bottomTop - CannonPivotYPx * bottomWpp, 0f);
            var cannonGo = new GameObject("Cannon");
            cannonGo.transform.position = pivot;
            var cannon = cannonGo.AddComponent<CannonView>();
            var barrel = new GameObject("Barrel").transform;
            barrel.SetParent(cannonGo.transform, false);
            float cannonW = 1.75f;
            var cannonSprite = LoadSprite("Cannon");
            float cannonH = cannonSprite != null ? cannonSprite.bounds.size.y / cannonSprite.bounds.size.x * cannonW : 2.9f;
            float lift = cannonH * 0.3f;
            var shadow = VisualUtil.MakeSprite("CannonShadow", barrel, LoadSprite("Cannon_Sh"), new Vector3(0.12f, lift - 0.15f, 0.5f), cannonW * 1.1f, -51);
            if (shadow != null) shadow.color = new Color(1, 1, 1, 0.7f);
            VisualUtil.MakeSprite("CannonBody", barrel, cannonSprite, new Vector3(0f, lift, 0f), cannonW, -50);
            cannon.barrel = barrel;
            cannon.muzzleDistance = lift + cannonH * 0.5f - 0.1f;

            // Aim guide
            var aim = new GameObject("AimGuide").AddComponent<AimGuide>();
            aim.dotSprite = LoadSprite("AimDot");

            // Audio
            var audioGo = new GameObject("Audio");
            var sfx = audioGo.AddComponent<GameAudio>();
            sfx.ballFire = LoadClip("ball_fire");
            sfx.ballFireRefused = LoadClip("ball_fire_refused");
            sfx.wallBounce = LoadClip("ball_wall_bounce");
            sfx.ballCollide = LoadClip("ball_collide");
            sfx.waterSplash = LoadClip("ball_water_splash");
            sfx.aimTick = LoadClip("aim_tick");
            sfx.lastMoves = LoadClip("last_moves_banner");

            // HUD
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<GameHud>();
            hud.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Eigerdals Norm Heavy.otf");
            hud.levelPlate = LoadSprite("Level Display");
            hud.settingsButton = LoadSprite("Settings_Button") ?? LoadSprite("Settings Button");
            hud.whiteSprite = LoadSprite("Square") ?? LoadSprite("White");
            hud.panelSprite = hud.whiteSprite;
            hud.levelAnchor = Anchor(hudGo.transform, "LevelAnchor", new Vector3(0f, lawnTop + 1.45f, -5f));
            hud.goalsAnchor = Anchor(hudGo.transform, "GoalsAnchor", new Vector3(0f, lawnTop + 3.1f, -5f));
            hud.settingsAnchor = Anchor(hudGo.transform, "SettingsAnchor", new Vector3(screenW * 0.5f - 1.3f, lawnTop + 1.45f, -5f));
            hud.movesAnchor = Anchor(hudGo.transform, "MovesAnchor", new Vector3((MovesBoxXPx - BottomW * 0.5f) * bottomWpp, bottomTop - MovesBoxYPx * bottomWpp, -5f));

            // Controller
            var gc = new GameObject("GameController").AddComponent<GameController>();
            gc.cam = cam;
            gc.board = board;
            gc.cannon = cannon;
            gc.aimGuide = aim;
            gc.hud = hud;
            gc.sfx = sfx;
            gc.catalog = catalog;
            gc.ballPrefab = LoadPrefab("BallItemView");
            gc.lawnRenderer = lawn;
            gc.lawn9x12 = grassTop;
            gc.lawn12x17 = LoadSprite("GrassTop_12x17");

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            Selection.activeGameObject = gc.gameObject;
            Debug.Log($"[GardenCannon] Scene saved: {ScenePath}. Press Play.");
        }

        static Transform Anchor(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = pos;
            return t;
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------ asset lookup

        static GameObject LoadPrefab(string name) =>
            string.IsNullOrEmpty(name) ? null : AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GameObject/{name}.prefab");

        static AudioClip LoadClip(string name) =>
            string.IsNullOrEmpty(name) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/AudioClip/{name}.ogg");

        static Sprite LoadSprite(string name)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprite/{name}.asset");
            if (s != null) return s;
            // Fallback: the exported PNGs (imported as sprites).
            return AssetDatabase.LoadAllAssetsAtPath($"Assets/SpriteExport/{name}.png").OfType<Sprite>().FirstOrDefault();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
