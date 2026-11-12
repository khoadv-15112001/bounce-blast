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

namespace GardenCannon.EditorTools
{
    /// <summary>
    /// Garden Cannon > Setup:
    ///  1. ItemCatalog + GameAssets (references to the original prefabs, sprites, SFX, font).
    ///  2. Addressables groups mirroring the original bundles in Assets/addressable:
    ///     Default (default_assets_all), AudioClips (audioclips_assets_all), LocalLevels (locallevels_assets_all),
    ///     LocalFont (localfont_assets_all), LocalShaders (localshaders_assets_all).
    ///  3. A scene that only holds the camera + GameBootstrap (no direct asset references, nothing in Resources).
    ///  4. Portrait 1440x3120 Game view.
    /// </summary>
    public static class GardenCannonSetup
    {
        const string Root = "Assets/GardenCannon";
        const string CatalogPath = Root + "/Data/ItemCatalog.asset";
        const string GameAssetsPath = Root + "/Data/GameAssets.asset";
        const string LevelsFolder = Root + "/Levels";
        const string ScenePath = Root + "/Scenes/GardenCannon.unity";

        // Group names = original bundle names without the "_assets_all" suffix.
        public const string GroupDefault = "Default";
        public const string GroupAudio = "AudioClips";
        public const string GroupLevels = "LocalLevels";
        public const string GroupFont = "LocalFont";
        public const string GroupShaders = "LocalShaders";

        const int RefWidth = 1440, RefHeight = 3120;

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
                    var mf = mr.GetComponent<MeshFilter>();
                    var c = VisualUtil.MeshUvCenter(mf != null ? mf.sharedMesh : null);
                    if (c.x >= 0f)
                    {
                        catalog.atlasBaseUvCenter = c;
                        catalog.atlasMeshFullUv = Mathf.Abs(c.x - 0.5f) < 0.1f && Mathf.Abs(c.y - 0.5f) < 0.1f;
                    }
                    break;
                }
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GardenCannon] Item catalog ready: {CatalogPath} ({catalog.entries.Count} types)");
            return catalog;
        }

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
            if (a.levelPlate == null) a.levelPlate = LoadSprite("Level Display");
            if (a.settingsButton == null) a.settingsButton = LoadSprite("Settings_Button") ?? LoadSprite("Settings Button");
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

            // Default: root assets, prefabs, sprites.
            Add(settings, gDefault, assets, GameAssets.Address);
            Add(settings, gDefault, catalog, "GardenCannon/ItemCatalog");
            var prefabs = new List<GameObject> { assets.ballPrefab };
            foreach (var e in catalog.entries) { prefabs.Add(e.prefab); prefabs.Add(e.breakFx); }
            prefabs = prefabs.Where(p => p != null).Distinct().ToList();
            foreach (var p in prefabs)
                Add(settings, gDefault, p, "GardenCannon/Prefabs/" + p.name);
            foreach (var s in new[] { assets.grassTop, assets.grassTop12x17, assets.water, assets.grassBottom, assets.cannon,
                         assets.cannonShadow, assets.aimDot, assets.levelPlate, assets.settingsButton, assets.white })
                if (s != null) Add(settings, gDefault, s, "GardenCannon/Sprites/" + s.name);

            // AudioClips
            var clips = new List<AudioClip> { assets.ballFire, assets.ballFireRefused, assets.wallBounce, assets.ballCollide, assets.waterSplash, assets.aimTick, assets.lastMoves };
            foreach (var e in catalog.entries) { clips.Add(e.hitSfx); clips.Add(e.breakSfx); }
            foreach (var c in clips.Where(c => c != null).Distinct())
                Add(settings, gAudio, c, "GardenCannon/Audio/" + c.name);

            // LocalFont
            if (assets.font != null) Add(settings, gFont, assets.font, "GardenCannon/Font/" + assets.font.name);

            // LocalLevels (label "levels")
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

            // LocalShaders: custom shaders used by the prefabs (kept in one bundle instead of duplicated).
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

            // Drop the auto-created empty group so the group list matches the original bundles.
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

        // ------------------------------------------------------------------ scene

        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera: shows exactly this world rect on a 1440x3120 screen (more on other aspects).
            var visible = new Rect(-5.645f, -6.94f, 11.29f, 24.46f);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.91f, 0.87f, 0.79f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camGo.transform.position = new Vector3(0f, visible.center.y, -50f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraFit>().content = visible;

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(30f, -20f, 0f);

            // ---- Background (measured on the original sprites: lawn = 9 units wide, water line at y = 0)
            var grassTop = LoadSprite("GrassTop");
            var grassBottom = LoadSprite("GrassBottom");
            var water = LoadSprite("Water");
            const float lawnLeftPx = 335f, lawnRightPx = 1372f, lawnTopPx = 528f;
            const float bottomTopY = -1.95f;
            float wpp = 9f / (lawnRightPx - lawnLeftPx);
            float topW = grassTop != null ? grassTop.rect.width : 1697f, topH = grassTop != null ? grassTop.rect.height : 2048f;
            float spriteW = topW * wpp;
            float lawnTop = (topH - lawnTopPx) * wpp;
            float bottomW = grassBottom != null ? grassBottom.rect.width : 1905f, bottomH = grassBottom != null ? grassBottom.rect.height : 647f;
            float bwpp = spriteW / bottomW;

            var env = new GameObject("Environment").transform;
            var lawn = AddrSprite("GrassTop", env, grassTop, new Vector3((topW * 0.5f - (lawnLeftPx + lawnRightPx) * 0.5f) * wpp, topH * 0.5f * wpp, 20f), spriteW, -100);
            if (water != null)
            {
                float w = spriteW + 0.4f, h = water.bounds.size.y / water.bounds.size.x * w;
                AddrSprite("Water", env, water, new Vector3(0f, 0.05f - h * 0.5f, 21f), w, -102);
            }
            AddrSprite("GrassBottom", env, grassBottom, new Vector3(0f, bottomTopY - bottomH * bwpp * 0.5f, 19f), spriteW, -99);

            // ---- Board (items are spawned here per level)
            var board = new GameObject("Board").AddComponent<Board>();
            board.lawnWidth = 9f;
            board.lawnTop = lawnTop;

            // ---- Cannon
            var cannonGo = new GameObject("Cannon");
            cannonGo.transform.position = new Vector3((955f - bottomW * 0.5f) * bwpp, bottomTopY - 165f * bwpp, 0f);
            var barrel = new GameObject("Barrel").transform;
            barrel.SetParent(cannonGo.transform, false);
            var cannonSprite = LoadSprite("Cannon");
            const float cw = 1.75f;
            float ch = cannonSprite != null ? cannonSprite.bounds.size.y / cannonSprite.bounds.size.x * cw : 2.9f;
            float lift = ch * 0.3f;
            var sh = AddrSprite("CannonShadow", barrel, LoadSprite("Cannon_Sh"), new Vector3(0.12f, lift - 0.15f, 0.5f), cw * 1.1f, -51);
            sh.Renderer.color = new Color(1f, 1f, 1f, 0.7f);
            AddrSprite("CannonBody", barrel, cannonSprite, new Vector3(0f, lift, 0f), cw, -50);
            var cannon = cannonGo.AddComponent<CannonView>();
            cannon.barrel = barrel;
            cannon.muzzleDistance = lift + ch * 0.5f - 0.1f;

            var aim = new GameObject("AimGuide").AddComponent<AimGuide>();
            var sfx = new GameObject("Audio").AddComponent<GameAudio>();

            // ---- HUD
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<GameHud>();
            hud.levelAnchor = Anchor(hudGo.transform, "LevelAnchor", new Vector3(0f, lawnTop + 1.45f, -5f));
            AddrSprite("LevelPlate", hud.levelAnchor, LoadSprite("Level Display"), Vector3.zero, 3.2f, 60);
            hud.goalsAnchor = Anchor(hudGo.transform, "GoalsAnchor", new Vector3(0f, lawnTop + 3.1f, -5f));
            hud.settingsAnchor = Anchor(hudGo.transform, "SettingsAnchor", new Vector3(4.55f, lawnTop + 1.45f, -5f));
            AddrSprite("RestartButton", hud.settingsAnchor, LoadSprite("Settings_Button") ?? LoadSprite("Settings Button"), Vector3.zero, 1.25f, 60);
            hud.movesAnchor = Anchor(hudGo.transform, "MovesAnchor", new Vector3((612f - bottomW * 0.5f) * bwpp, bottomTopY - 112f * bwpp, -5f));

            // ---- Controller + bootstrap
            var gc = new GameObject("GameController").AddComponent<GameController>();
            var boot = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            boot.cam = cam;
            boot.board = board;
            boot.cannon = cannon;
            boot.aimGuide = aim;
            boot.sfx = sfx;
            boot.hud = hud;
            boot.controller = gc;
            boot.lawnRenderer = lawn.Renderer;

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Selection.activeGameObject = boot.gameObject;
            Debug.Log($"[GardenCannon] Scene saved: {ScenePath}. Press Play.");
        }

        /// <summary>Sprite object whose sprite is an Addressables reference (previewed in edit mode, not saved in the scene).</summary>
        static AddressableSprite AddrSprite(string name, Transform parent, Sprite sprite, Vector3 localPos, float worldWidth, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            var a = go.AddComponent<AddressableSprite>();
            if (sprite != null)
            {
                var path = AssetDatabase.GetAssetPath(sprite);
                a.sprite = new UnityEngine.AddressableAssets.AssetReferenceSprite(AssetDatabase.AssetPathToGUID(path));
                if (!AssetDatabase.IsMainAsset(sprite)) a.sprite.SetEditorSubObject(sprite);
                float w = sprite.bounds.size.x;
                if (w > 0f && worldWidth > 0f) go.transform.localScale = Vector3.one * (worldWidth / w);
                EnsureAddressable(sprite);
            }
            a.RefreshPreview();
            return a;
        }

        static Transform Anchor(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = pos;
            return t;
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
