using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace GardenCannon.EditorTools
{
    /// <summary>
    /// Garden Cannon > Setup:
    ///  1. ItemCatalog + GameAssets (references to the original prefabs, sprites, SFX, font).
    ///  2. Addressables groups mirroring the original bundles in Assets/addressable:
    ///     Default, AudioClips, LocalLevels, LocalFont, LocalShaders.
    ///  3. A scene laid out like the original Game scene (same world units, tilted main camera + UI camera),
    ///     sprites referenced through Addressables only.
    ///  4. Portrait 1440x3120 Game view.
    /// </summary>
    public static class GardenCannonSetup
    {
        const string Root = "Assets/GardenCannon";
        const string CatalogPath = Root + "/Data/ItemCatalog.asset";
        const string GameAssetsPath = Root + "/Data/GameAssets.asset";
        const string LevelsFolder = Root + "/Levels";
        const string ScenePath = Root + "/Scenes/GardenCannon.unity";

        public const string GroupDefault = "Default";
        public const string GroupAudio = "AudioClips";
        public const string GroupLevels = "LocalLevels";
        public const string GroupFont = "LocalFont";
        public const string GroupShaders = "LocalShaders";

        const int RefWidth = 1440, RefHeight = 3120;
        const int DefaultsVersion = 2;
        const int UiLayer = 5; // built-in "UI"

        [MenuItem("Garden Cannon/Setup (catalog + addressables + scene)", priority = 0)]
        public static void SetupAll()
        {
            var catalog = CreateOrUpdateCatalog();
            var assets = CreateOrUpdateGameAssets(catalog);
            SetupAddressables(catalog, assets);
            BuildScene();
            SetupResolution();
        }

        [MenuItem("Garden Cannon/Create or Update Item Catalog", priority = 1)]
        public static void CatalogOnly() => Selection.activeObject = CreateOrUpdateCatalog();

        [MenuItem("Garden Cannon/Refresh Addressables Groups", priority = 2)]
        public static void AddressablesOnly()
        {
            var catalog = CreateOrUpdateCatalog();
            SetupAddressables(catalog, CreateOrUpdateGameAssets(catalog));
        }

        [MenuItem("Garden Cannon/Open Game Scene", priority = 3)]
        public static void OpenScene()
        {
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else SetupAll();
        }

        [MenuItem("Garden Cannon/Set Game View 1440x3120", priority = 20)]
        public static void SetupResolution()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            try { SelectGameViewSize(RefWidth, RefHeight, "Garden Cannon 1440x3120"); }
            catch (Exception e) { Debug.LogWarning("[GardenCannon] Could not set the Game view size automatically (" + e.Message + "). Add 1440x3120 in the Game view resolution menu."); }
        }

        // ------------------------------------------------------------------ catalog

        struct Def
        {
            public int id; public string label; public ItemKind kind; public string prefab;
            public int hits; public string hit, brk, stages; public bool color, rotate, count, fit, autotile;
            public float speed; public Color fxColor;
        }

        static Def D(int id, string label, ItemKind kind, string prefab, string hit, string brk, Color fxColor) =>
            new Def { id = id, label = label, kind = kind, prefab = prefab, hits = 1, hit = hit, brk = brk, speed = 1.5f, fxColor = fxColor };

        /// <summary>
        /// Item ids from the level data, matched against the original game (video) and the data itself
        /// (footprints, goals, lanes, HP). 2/6/7/8/9/13 appear from level 12 on and are best guesses.
        /// </summary>
        static IEnumerable<Def> Defaults()
        {
            Def d;
            d = D(1, "Block", ItemKind.Breakable, "BlockItemView", "ball_collide", "block_break", Color.white); d.color = true; yield return d;
            d = D(26, "Wooden Box", ItemKind.Breakable, "WoodenBoxItemView", "wooden_box_hit", "wooden_box_break", new Color(.93f, .55f, .16f)); d.stages = "stage_"; yield return d;
            d = D(23, "Stone Corner", ItemKind.Wedge, "HalfUnbreakableBlockItemView", "unbreakable_hit", null, new Color(.9f, .86f, .78f)); d.rotate = true; yield return d;
            d = D(25, "Stone", ItemKind.Indestructible, "UnbreakableBlockItemView", "unbreakable_hit", null, new Color(.9f, .86f, .78f)); d.fit = true; yield return d;
            d = D(4, "Moving Barrier", ItemKind.Indestructible, "MovingBarrierItemView", "moving_barrier_hit", null, new Color(.9f, .25f, .2f)); d.fit = true; d.speed = 1.6f; yield return d;
            d = D(5, "Target Stand", ItemKind.Breakable, "TargetStandItemView", "target_hit", "target_stand_collapse", new Color(1f, .45f, .7f)); d.hits = 3; d.fit = true; yield return d;
            d = D(24, "Container", ItemKind.Breakable, "ContainerItemView", "container_hit", "container_burst", new Color(1f, .8f, .25f)); d.count = true; d.fit = true; yield return d;
            d = D(9, "Moving Target", ItemKind.Breakable, "TargetItemView", "target_hit", "target_break", new Color(1f, .45f, .7f)); d.speed = 1.2f; yield return d;
            d = D(7, "Log Cutter", ItemKind.Breakable, "LogCutterItemView", "log_cut", "log_cutter_break", new Color(.6f, .4f, .2f)); d.hits = 2; d.fit = true; yield return d;
            d = D(8, "Egg Box", ItemKind.Breakable, "EggBoxItemView", "egg_crack", "egg_box_break", new Color(.98f, .95f, .85f)); d.hits = 3; yield return d;
            d = D(13, "Multi Block", ItemKind.Breakable, "MultiBlockItemView", "ball_collide", "block_break", Color.white); d.hits = 10; d.count = true; d.color = true; d.fit = true; yield return d;
            d = D(6, "Electric Orb", ItemKind.Breakable, "ElectricOrbItemView", "orb_on", "orb_discharge", new Color(.4f, .7f, 1f)); d.hits = 2; d.fit = true; yield return d;
            d = D(2, "Brick Wall", ItemKind.Indestructible, "BrickWallItemView", "brick_wall_hit", null, new Color(.25f, .3f, .8f)); d.autotile = true; yield return d;
        }

        static readonly string[] AutotileMeshes =
        {
            "Single_module", "End_module1", "Straight_module1", "Corner_module_straight1", "Corner_module_vers_03",
            "Double_Middle_module1", "Quad_middle_module1",
        };

        public static ItemCatalog CreateOrUpdateCatalog()
        {
            EnsureFolder(Root + "/Data");
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            bool rebuild = catalog.defaultsVersion < DefaultsVersion;
            if (rebuild)
            {
                catalog.entries.Clear();
                catalog.defaultsVersion = DefaultsVersion;
            }
            foreach (var def in Defaults())
            {
                var e = catalog.entries.FirstOrDefault(x => x.typeId == def.id);
                if (e != null)
                {
                    // Keep user edits; only fill references that are still empty.
                    if (e.prefab == null) e.prefab = LoadPrefab(def.prefab);
                    if (e.hitSfx == null) e.hitSfx = LoadClip(def.hit);
                    if (e.breakSfx == null) e.breakSfx = LoadClip(def.brk);
                    
                    continue;
                }
                catalog.entries.Add(new ItemCatalog.Entry
                {
                    label = def.label, typeId = def.id, kind = def.kind, defaultHits = def.hits,
                    prefab = LoadPrefab(def.prefab), hitSfx = LoadClip(def.hit), breakSfx = LoadClip(def.brk), breakFx = null,
                    colorByVariant = def.color, rotateByVariant = def.rotate, showHitCount = def.count, fitFootprint = def.fit,
                    stagePrefix = def.stages ?? "", autotile = def.autotile, laneSpeed = def.speed, debugColor = def.fxColor,
                });
            }
            catalog.entries.Sort((a, b) => a.typeId.CompareTo(b.typeId));

            DetectBlockAtlas(catalog);
            DetectAutotileModules(catalog);

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GardenCannon] Item catalog ready: {CatalogPath} ({catalog.entries.Count} types{(rebuild ? ", rebuilt from defaults v" + DefaultsVersion : "")})");
            return catalog;
        }

        static void DetectBlockAtlas(ItemCatalog catalog)
        {
            var block = LoadPrefab("BlockItemView");
            if (block == null) return;
            foreach (var mr in block.GetComponentsInChildren<MeshRenderer>(true))
            {
                var tex = mr.sharedMaterial != null && mr.sharedMaterial.HasProperty("_MainTex") ? mr.sharedMaterial.mainTexture : null;
                if (tex == null || !tex.name.Contains("BlockColorAtlas")) continue;
                var mf = mr.GetComponent<MeshFilter>();
                var c = VisualUtil.MeshUvCenter(mf != null ? mf.sharedMesh : null);
                if (c.x >= 0f)
                {
                    catalog.atlasBaseUvCenter = c;
                    catalog.atlasMeshFullUv = Mathf.Abs(c.x - 0.5f) < 0.1f && Mathf.Abs(c.y - 0.5f) < 0.1f;
                }
                return;
            }
        }

        /// <summary>
        /// For every brick wall module mesh, find which cell sides carry the raised brick rim when the mesh sits
        /// in the prefab's "Tile" slot unrotated. Rim = vertices in the top band of the height (towards the camera).
        /// </summary>
        static void DetectAutotileModules(ItemCatalog catalog)
        {
            var prefab = LoadPrefab("BrickWallItemView");
            if (prefab == null) return;
            var inst = UnityEngine.Object.Instantiate(prefab);
            inst.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var tile = inst.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name == "Tile");
                if (tile == null) return;
                var toRoot = inst.transform.worldToLocalMatrix * tile.transform.localToWorldMatrix;
                catalog.autotileModules.Clear();
                var log = new List<string>();
                foreach (var name in AutotileMeshes)
                {
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/Mesh/{name}.asset");
                    if (mesh == null) continue;
                    var verts = mesh.vertices.Select(v => toRoot.MultiplyPoint3x4(v)).ToArray();
                    if (verts.Length == 0) continue;
                    float minX = verts.Min(v => v.x), maxX = verts.Max(v => v.x);
                    float minY = verts.Min(v => v.y), maxY = verts.Max(v => v.y);
                    float top = verts.Min(v => v.z), bottom = verts.Max(v => v.z); // -z = towards the camera
                    float cut = top + (bottom - top) * 0.2f;
                    float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;
                    float hx = Mathf.Max(1e-4f, (maxX - minX) * 0.5f), hy = Mathf.Max(1e-4f, (maxY - minY) * 0.5f);
                    int mask = 0;
                    foreach (var v in verts)
                    {
                        if (v.z > cut) continue;
                        float nx = (v.x - cx) / hx, ny = (v.y - cy) / hy;
                        if (ny > 0.6f && Mathf.Abs(nx) < 0.35f) mask |= 1;
                        if (nx > 0.6f && Mathf.Abs(ny) < 0.35f) mask |= 2;
                        if (ny < -0.6f && Mathf.Abs(nx) < 0.35f) mask |= 4;
                        if (nx < -0.6f && Mathf.Abs(ny) < 0.35f) mask |= 8;
                    }
                    catalog.autotileModules.Add(new ItemCatalog.AutotileModule { mesh = mesh, rimMask = mask });
                    log.Add($"{name}={MaskText(mask)}");
                }
                Debug.Log("[GardenCannon] Brick wall modules (rim sides): " + string.Join(", ", log));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inst);
            }
        }

        static string MaskText(int m) => m == 0 ? "none" : ((m & 1) != 0 ? "T" : "") + ((m & 2) != 0 ? "R" : "") + ((m & 4) != 0 ? "B" : "") + ((m & 8) != 0 ? "L" : "");

        public static GameAssets CreateOrUpdateGameAssets(ItemCatalog catalog)
        {
            EnsureFolder(Root + "/Data");
            var a = AssetDatabase.LoadAssetAtPath<GameAssets>(GameAssetsPath);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<GameAssets>();
                AssetDatabase.CreateAsset(a, GameAssetsPath);
            }
            a.catalog = catalog;
            if (a.ballPrefab == null) a.ballPrefab = LoadPrefab("BallItemView");
            if (a.font == null) a.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Eigerdals Norm Heavy.otf");
            if (a.grassTop == null) a.grassTop = LoadSprite("GrassTop");
            if (a.grassTop12x17 == null) a.grassTop12x17 = LoadSprite("GrassTop_12x17");
            if (a.water == null) a.water = LoadSprite("Water");
            if (a.grassBottom == null) a.grassBottom = LoadSprite("GrassBottom");
            if (a.cannon == null) a.cannon = LoadSprite("Cannon");
            if (a.cannonShadow == null) a.cannonShadow = LoadSprite("Cannon_Sh");
            if (a.aimDot == null) a.aimDot = LoadSprite("AimDot");
            if (a.sparkle == null) a.sparkle = LoadSprite("Sparkle");
            if (a.levelPlate == null) a.levelPlate = LoadSprite("Level Display");
            if (a.settingsButton == null) a.settingsButton = LoadSprite("Settings Button") ?? LoadSprite("Settings_Button");
            if (a.white == null) a.white = LoadSprite("Square") ?? LoadSprite("White");
            if (a.ballFire == null) a.ballFire = LoadClip("ball_fire");
            if (a.ballFireRefused == null) a.ballFireRefused = LoadClip("ball_fire_refused");
            if (a.wallBounce == null) a.wallBounce = LoadClip("ball_wall_bounce");
            if (a.ballCollide == null) a.ballCollide = LoadClip("ball_collide");
            if (a.waterSplash == null) a.waterSplash = LoadClip("ball_water_splash");
            if (a.aimTick == null) a.aimTick = LoadClip("aim_tick");
            if (a.lastMoves == null) a.lastMoves = LoadClip("last_moves_banner");
            EditorUtility.SetDirty(a);
            AssetDatabase.SaveAssets();
            return a;
        }

        // ------------------------------------------------------------------ addressables

        public static void SetupAddressables(ItemCatalog catalog, GameAssets assets)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var gDefault = EnsureGroup(settings, GroupDefault);
            var gAudio = EnsureGroup(settings, GroupAudio);
            var gLevels = EnsureGroup(settings, GroupLevels);
            var gFont = EnsureGroup(settings, GroupFont);
            var gShaders = EnsureGroup(settings, GroupShaders);
            settings.DefaultGroup = gDefault;

            Add(settings, gDefault, assets, GameAssets.Address);
            Add(settings, gDefault, catalog, "GardenCannon/ItemCatalog");
            var prefabs = new List<GameObject> { assets.ballPrefab };
            foreach (var e in catalog.entries) { prefabs.Add(e.prefab); prefabs.Add(e.breakFx); }
            prefabs = prefabs.Where(p => p != null).Distinct().ToList();
            foreach (var p in prefabs)
                Add(settings, gDefault, p, "GardenCannon/Prefabs/" + p.name);
            foreach (var s in new[] { assets.grassTop, assets.grassTop12x17, assets.water, assets.grassBottom, assets.cannon,
                         assets.cannonShadow, assets.aimDot, assets.sparkle, assets.levelPlate, assets.settingsButton, assets.white })
                if (s != null) Add(settings, gDefault, s, "GardenCannon/Sprites/" + s.name);

            var clips = new List<AudioClip> { assets.ballFire, assets.ballFireRefused, assets.wallBounce, assets.ballCollide, assets.waterSplash, assets.aimTick, assets.lastMoves };
            foreach (var e in catalog.entries) { clips.Add(e.hitSfx); clips.Add(e.breakSfx); }
            foreach (var c in clips.Where(c => c != null).Distinct())
                Add(settings, gAudio, c, "GardenCannon/Audio/" + c.name);

            if (assets.font != null) Add(settings, gFont, assets.font, "GardenCannon/Font/" + assets.font.name);

            int levels = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { LevelsFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".json")) continue;
                var entry = settings.CreateOrMoveEntry(guid, gLevels, false, false);
                entry.SetAddress("GardenCannon/Levels/" + Path.GetFileNameWithoutExtension(path), false);
                entry.SetLabel(LevelLibrary.Label, true, true, false);
                levels++;
            }

            var prefabPaths = prefabs.Select(AssetDatabase.GetAssetPath).ToArray();
            int shaders = 0;
            foreach (var dep in AssetDatabase.GetDependencies(prefabPaths, true))
            {
                if (!dep.EndsWith(".shader") || !dep.StartsWith("Assets/")) continue;
                var sh = AssetDatabase.LoadAssetAtPath<Shader>(dep);
                if (sh == null) continue;
                Add(settings, gShaders, sh, "GardenCannon/Shaders/" + Path.GetFileNameWithoutExtension(dep));
                shaders++;
            }

            var auto = settings.FindGroup("Default Local Group");
            if (auto != null && auto != gDefault && auto.entries.Count == 0) settings.RemoveGroup(auto);

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GardenCannon] Addressables ready: {GroupDefault}, {GroupAudio}, {GroupLevels} ({levels} levels), {GroupFont}, {GroupShaders} ({shaders} shaders).");
        }

        static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name)
        {
            var g = settings.FindGroup(name);
            if (g == null)
                g = settings.CreateGroup(name, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = g.GetSchema<BundledAssetGroupSchema>();
            if (schema == null) schema = g.AddSchema<BundledAssetGroupSchema>();
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            if (g.GetSchema<ContentUpdateGroupSchema>() == null) g.AddSchema<ContentUpdateGroupSchema>();
            return g;
        }

        static void Add(AddressableAssetSettings settings, AddressableAssetGroup group, UnityEngine.Object asset, string address)
        {
            if (asset == null) return;
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) return;
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group, false, false);
            entry.SetAddress(address, false);
        }

        // ------------------------------------------------------------------ scene (layout of the original Game scene)

        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- Cameras: main (tilted, world) + UI (flat, HUD layer)
            var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.91f, 0.87f, 0.79f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.cullingMask = ~(1 << UiLayer);
            cam.depth = 0;
            camGo.AddComponent<AudioListener>();

            var uiGo = new GameObject("UICamera");
            uiGo.transform.position = new Vector3(0f, 0f, -10f);
            var ui = uiGo.AddComponent<Camera>();
            ui.orthographic = true;
            ui.orthographicSize = 12.8f;
            ui.clearFlags = CameraClearFlags.Depth;
            ui.cullingMask = 1 << UiLayer;
            ui.depth = 1;
            ui.nearClipPlane = 0.1f;
            ui.farClipPlane = 40f;

            var rig = camGo.AddComponent<CameraRig>();
            rig.uiCamera = ui;
            rig.Apply();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(30f, -20f, 0f);

            // ---- GrassTheme (original positions, native sprite size)
            var theme = new GameObject("GrassTheme").transform;
            var top = AddrSprite("Top", theme, LoadSprite("GrassTop"), new Vector3(0f, 4.1f, 0f), -8);
            var bottom = AddrSprite("Bottom", theme, LoadSprite("GrassBottom"), new Vector3(0f, -12.37f, 0f), -10);
            var water = AddrSprite("Water", theme, LoadSprite("Water"), new Vector3(0f, -8.48f, 0f), -11);
            water.transform.localScale = new Vector3(0.972f, 1f, 1f);

            // ---- Board (original GridView)
            var board = new GameObject("Board").AddComponent<Board>();

            // ---- Cannon (original CannonBase/Piv/Rail)
            var cannonGo = new GameObject("CannonBase");
            cannonGo.transform.position = new Vector3(0f, -10.54f, 0f);
            var rail = new GameObject("Rail").transform;
            rail.SetParent(cannonGo.transform, false);
            AddrSprite("Cannon Body", rail, LoadSprite("Cannon"), new Vector3(0.005f, 0.6548f, 0f), 50);
            AddrSprite("Cannon Sh", rail, LoadSprite("Cannon_Sh"), new Vector3(-0.27125f, -0.0252f, 0f), 49);
            var cannon = cannonGo.AddComponent<CannonView>();
            cannon.barrel = rail;
            cannon.muzzleDistance = 2.5f; // original "Tip"

            var aim = new GameObject("AimGuide").AddComponent<AimGuide>();
            var sfx = new GameObject("Audio").AddComponent<GameAudio>();
            var fx = new GameObject("Fx").AddComponent<FxPool>();

            // ---- HUD
            var hudGo = new GameObject("HUD") { layer = UiLayer };
            var hud = hudGo.AddComponent<GameHud>();
            hud.uiCamera = ui;
            hud.levelAnchor = UiAnchorAt(hudGo.transform, "LevelDisplay", ui, UiAnchor.Edge.Top, 12.8f - 11.47f, 0f);
            AddrSprite("LevelPlate", hud.levelAnchor, LoadSprite("Level Display"), new Vector3(0f, -0.105f, 0f), 59).gameObject.layer = UiLayer;
            hud.settingsAnchor = UiAnchorAt(hudGo.transform, "SettingsButton", ui, UiAnchor.Edge.Top, 12.8f - 11.47f, 5.0f);
            AddrSprite("Button", hud.settingsAnchor, LoadSprite("Settings Button") ?? LoadSprite("Settings_Button"), Vector3.zero, 15).gameObject.layer = UiLayer;
            hud.goalsAnchor = UiAnchorAt(hudGo.transform, "Goals", ui, UiAnchor.Edge.Top, 12.8f - 11.47f + 1.75f, 0f);
            hud.bannerAnchor = UiAnchorAt(hudGo.transform, "TutorialBanner", ui, UiAnchor.Edge.Centre, 4.2f, 0f);
            // Move counter printed on the GrassBottom art (original MoveCountText, tilted to face the camera).
            var moves = new GameObject("MoveCountText").transform;
            moves.SetParent(bottom.transform, false);
            moves.localPosition = new Vector3(-3.39f, 2.1348f, -0.05f);
            moves.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            hud.movesAnchor = moves;

            // ---- Controller + bootstrap
            var gc = new GameObject("GameController").AddComponent<GameController>();
            var boot = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            boot.rig = rig;
            boot.uiCamera = ui;
            boot.board = board;
            boot.cannon = cannon;
            boot.aimGuide = aim;
            boot.sfx = sfx;
            boot.fx = fx;
            boot.hud = hud;
            boot.controller = gc;
            boot.lawnRenderer = top.Renderer;

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Selection.activeGameObject = boot.gameObject;
            Debug.Log($"[GardenCannon] Scene saved: {ScenePath}. Press Play.");
        }

        static Transform UiAnchorAt(Transform parent, string name, Camera ui, UiAnchor.Edge edge, float offset, float x)
        {
            var go = new GameObject(name) { layer = UiLayer };
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<UiAnchor>();
            a.uiCamera = ui;
            a.edge = edge;
            a.offset = offset;
            a.x = x;
            a.Apply();
            return go.transform;
        }

        /// <summary>Sprite object whose sprite is an Addressables reference (previewed in edit mode, not saved in the scene).</summary>
        static AddressableSprite AddrSprite(string name, Transform parent, Sprite sprite, Vector3 localPos, int order)
        {
            var go = new GameObject(name);
            go.layer = parent != null ? parent.gameObject.layer : 0;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            var a = go.AddComponent<AddressableSprite>();
            if (sprite != null)
            {
                var path = AssetDatabase.GetAssetPath(sprite);
                a.sprite = new AssetReferenceSprite(AssetDatabase.AssetPathToGUID(path));
                if (!AssetDatabase.IsMainAsset(sprite)) a.sprite.SetEditorSubObject(sprite);
                EnsureAddressable(sprite);
            }
            a.RefreshPreview();
            return a;
        }

        static void EnsureAddressable(UnityEngine.Object asset)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            if (settings.FindAssetEntry(guid) != null) return;
            var e = settings.CreateOrMoveEntry(guid, EnsureGroup(settings, GroupDefault), false, true);
            e.SetAddress("GardenCannon/Sprites/" + asset.name, true);
        }

        // ------------------------------------------------------------------ game view size (internal API, via reflection)

        static void SelectGameViewSize(int w, int h, string label)
        {
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleton.GetProperty("instance").GetValue(null);
            var group = sizesType.GetProperty("currentGroup").GetValue(instance);
            var groupType = group.GetType();
            int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            int index = -1;
            for (int i = 0; i < total; i++)
            {
                var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                var st = size.GetType();
                if ((int)st.GetProperty("width").GetValue(size) == w && (int)st.GetProperty("height").GetValue(size) == h) { index = i; break; }
            }
            if (index < 0)
            {
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var kindType = asm.GetType("UnityEditor.GameViewSizeType");
                var ctor = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) });
                var newSize = ctor.Invoke(new object[] { Enum.Parse(kindType, "FixedResolution"), w, h, label });
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                sizesType.GetMethod("SaveToHDD")?.Invoke(instance, null);
                index = total;
            }
            var gameViewType = asm.GetType("UnityEditor.GameView");
            var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
            var prop = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            prop?.SetValue(gameView, index);
            gameView.Repaint();
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
