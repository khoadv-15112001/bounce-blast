using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace GardenCannon.EditorTools
{
    /// <summary>
    /// After each script reload: make sure GameAssets has the digit sprites (block HP labels) and that the digit
    /// texture is addressable, so nothing depends on remembering to run the Update menu.
    /// </summary>
    [InitializeOnLoad]
    static class GameAssetsAutoFix
    {
        const string GameAssetsPath = "Assets/GardenCannon/Data/GameAssets.asset";
        const string DigitsPath = "Assets/GardenCannon/Art/Digits.png";
        const string OverlayShaderPath = "Assets/GardenCannon/Shaders/SpriteOverlay.shader";
        const string BirdPath = "Assets/GardenCannon/Art/Bird.png";
        const string FxAtlasPath = "Assets/GardenCannon/Art/FxAtlas.png";

        static GameAssetsAutoFix()
        {
            EditorApplication.delayCall += Run;
        }

        const int BehaviourVersion = 7;

        /// <summary>Only replaces the old defaults (an edit made in GameAssets afterwards is kept).</summary>
        static bool ApplyAudioAndBalloons(GameAssets assets)
        {
            bool changed = false;
            var bgm = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/GardenCannon/Audio/BGM_Ingame_Universal_02.wav");
            if (bgm != null && (assets.music == null || assets.music.name == "Music - Core Game Play"))
            {
                GardenCannonSetup.ConfigureExtractedAudioPublic();   // long BGM: streamed
                assets.music = bgm;
                changed = true;
            }
            var blast = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/GardenCannon/Audio/cannon_blast.ogg");
            if (blast != null && (assets.ballFire == null || assets.ballFire.name == "ball_fire"))
            {
                assets.ballFire = blast;
                changed = true;
            }
            // New circus theme lawn with the checker baked in: 9-column and 12-column boards.
            var lawn = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GardenCannon/Art/GrassTop.png");
            var lawn12 = lawn;   // one picture for both board sizes: the checker is drawn underneath per level
            if (lawn != null && assets.grassTop != lawn) { assets.grassTop = lawn; changed = true; }
            if (lawn12 != null && assets.grassTop12x17 != lawn12) { assets.grassTop12x17 = lawn12; changed = true; }
            // Booster icons: cut from Art/Boosters/booster_icons.png (arrow, bowling, x2, bomb).
            Sprite Icon(string n) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GardenCannon/Art/Boosters/" + n + ".png");
            void SetIcon(ref Sprite slot, string n) { var s = Icon(n); if (s != null && slot != s) { slot = s; changed = true; } }
            SetIcon(ref assets.boosterX2Icon, "BoosterX2");
            SetIcon(ref assets.boosterArrowIcon, "BoosterArrow");
            SetIcon(ref assets.boosterBowlingIcon, "BoosterBowling");
            SetIcon(ref assets.boosterBombIcon, "BoosterBomb");
            var balloon = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/Prefab/Baloon/Baloon.prefab");
            var eggBox = assets.catalog != null ? assets.catalog.entries.FirstOrDefault(x => x.typeId == 5) : null;
            // Balloon flying away: balloon_fly instead of the bird sound.
            var fly = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/GardenCannon/Audio/ballon_fly.ogg");
            if (fly != null && eggBox != null && (eggBox.extraBreakSfx == null || eggBox.extraBreakSfx.name == "egg_bird_fly"))
            {
                eggBox.extraBreakSfx = fly;
                EditorUtility.SetDirty(assets.catalog);
                changed = true;
            }
            if (balloon != null && eggBox != null && eggBox.eggPrefab == null)
            {
                // Balloons in the egg box tray instead of eggs.
                eggBox.eggPrefab = balloon;
                eggBox.eggMaterials = AssetDatabase.FindAssets("t:Material", new[] { "Assets/3D/Mat baloon" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => !p.Contains("MAIN"))
                    .Select(AssetDatabase.LoadAssetAtPath<Material>)
                    .Where(m => m != null).ToArray();
                EditorUtility.SetDirty(assets.catalog);
                changed = true;
            }
            if (balloon != null && (assets.balloonPrefab == null || assets.balloonMaterials == null || assets.balloonMaterials.Length == 0))
            {
                assets.balloonPrefab = balloon;
                assets.balloonMaterials = AssetDatabase.FindAssets("t:Material", new[] { "Assets/3D/Mat baloon" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => !p.Contains("MAIN"))
                    .Select(AssetDatabase.LoadAssetAtPath<Material>)
                    .Where(m => m != null).ToArray();
                changed = true;
            }
            return changed;
        }

        const string Art3D = "Assets/3D/Prefab/";

        static GameObject Art(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(Art3D + path + ".prefab");

        /// <summary>
        /// New 3D art from the art team (Assets/3D): Y-up models of 1 unit per cell, already using Spyke/StandardCustomLightDir.
        /// Items without new art (egg box, log cutter, electric orb, targets) keep the original prefabs.
        /// </summary>
        static void ApplyArt3D(GameAssets assets)
        {
            if (!AssetDatabase.IsValidFolder("Assets/3D/Prefab")) return;
            // Y-up models whose -Z is the top of the screen (BG fence / cannon barrel): top faces the camera.
            var up = new Vector3(90f, 180f, 0f);
            (string n, Color c)[] colours =
            {
                ("Black", new Color(0.15f, 0.15f, 0.17f)), ("Blue", new Color(0.12f, 0.38f, 0.92f)),
                ("Green", new Color(0.2f, 0.72f, 0.25f)), ("Pink", new Color(1f, 0.45f, 0.72f)),
                ("Purple", new Color(0.58f, 0.25f, 0.88f)), ("Red", new Color(0.9f, 0.15f, 0.13f)),
                ("White", new Color(0.92f, 0.92f, 0.94f)), ("Yellow", new Color(1f, 0.82f, 0.12f)),
            };
            ItemCatalog.ColorPrefab[] Colours(string prefix) => colours
                .Select(c => new ItemCatalog.ColorPrefab { name = c.n, color = c.c, prefab = Art("Blocks/" + prefix + c.n) })
                .Where(c => c.prefab != null).ToArray();

            foreach (var e in assets.catalog.entries)
            {
                switch (e.typeId)
                {
                    case 5: // Egg box -> balloon box (balloons in the tray, lid halves on hinges)
                    {
                        var p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/Prefab/Baloon/Baloon Box.prefab");
                        if (p == null) break;
                        e.prefab = p; e.modelEuler = up; e.fitFootprint = false; e.castShadow = true;
                        e.openFx = null; e.hatchFx = null; e.breakFx = null;   // re-hooked by HookVfx (wooden pieces)
                        break;
                    }
                    case 1: // Block with a star
                        e.colorPrefabs = Colours("Star_Block_");
                        if (e.colorPrefabs.Length > 0) { e.modelEuler = up; e.castShadow = true; }
                        break;
                    case 26: // Number block
                        e.colorPrefabs = Colours("Block_");
                        if (e.colorPrefabs.Length > 0) { e.modelEuler = up; e.castShadow = true; }
                        break;
                    case 23: // Stone corner: the new triangle has its right angle top-right
                    {
                        var p = Art("Elements/Stone_Block_triangle");
                        if (p == null) break;
                        // Unity mirrors X on import, so after the model turn the right angle is bottom-right like the original.
                        // The triangle mesh is bevelled (0.92 of a cell, off-centre): fit it to the cell so its right angle
                        // sits in the cell corner for every rotation.
                        e.prefab = p; e.modelEuler = up; e.rotationOffset = 0f; e.rotateByVariant = true; e.fitFootprint = true; e.fitFill = 1f;
                        break;
                    }
                    case 2: // Brick wall -> stone block (the new art has no dynamite: solid again)
                    {
                        var p = Art("Elements/Stone_Block_1x1");
                        if (p == null) break;
                        e.prefab = p; e.modelEuler = up; e.autotile = false; e.fitFootprint = false;
                        e.kind = ItemKind.Indestructible; e.behaviour = ItemBehaviour.Default; e.castShadow = true;
                        e.breakFx = null;
                        break;
                    }
                    case 4: // Moving barrier -> slider of the footprint length
                    {
                        var s = new[] { Art("Obstacles/Slider_2"), Art("Obstacles/Slider_3"), Art("Obstacles/Slider_4") }.Where(x => x != null).ToArray();
                        if (s.Length == 0) break;
                        e.sizePrefabs = s; e.modelEuler = up; e.fitFootprint = false; e.castShadow = true;
                        break;
                    }
                    case 25: // Twirler -> spinner with 2 / 4 arms (arms along the model axes)
                    {
                        var s = new[] { Art("Obstacles/Spinner_2"), Art("Obstacles/Spinner_3"), Art("Obstacles/Spinner_4") }.Where(x => x != null).ToArray();
                        if (s.Length == 0) break;
                        e.sizePrefabs = s; e.modelEuler = up; e.fitFootprint = false; e.spinVisualOffset = 45f;
                        break;
                    }
                    case 13: // Wooden box -> one wooden block per cell, planks come off with hits
                    {
                        var p = Art("Obstacles/Wooden block");
                        if (p == null) break;
                        e.prefab = p; e.modelEuler = up; e.stagePrefix = "Wood "; e.stagesCumulative = true; e.cellHits = 2; e.castShadow = true;
                        break;
                    }
                    case 24: // Container -> wooden crate stretched over the footprint, HP number on top
                    {
                        var s = new[] { Art("Obstacles/Wooden_Crate_2x2"), Art("Obstacles/Wooden_Crate_2x3"), Art("Obstacles/Wooden_Crate_4x3") }.Where(x => x != null).ToArray();
                        if (s.Length == 0) break;
                        e.sizePrefabs = s; e.modelEuler = up; e.fitFootprint = false; e.showHitCount = true; e.castShadow = true;
                        break;
                    }
                }
            }

            var ball = Art("Elements/Ball");
            if (ball != null) assets.ballPrefab = ball;
            assets.cannon3D = Art("Elements/Cannon");
            assets.bg3D = Art("Elements/BG");
            assets.grass3D = Art("Elements/grass");
        }

        static AudioClip Clip(string n, AudioClip fallback)
        {
            var c = GardenCannonSetup.LoadClip(n);
            return c != null ? c : fallback;
        }

        /// <summary>
        /// Guessed obstacle rules (the original scripts were not recovered). Applied once per BehaviourVersion; after
        /// that every value can be edited in the ItemCatalog inspector.
        /// </summary>
        static void ApplyBehaviourDefaults(ItemCatalog catalog)
        {
            foreach (var e in catalog.entries)
            {
                switch (e.typeId)
                {
                    case 2: // Brick Wall: crack, expose dynamite, fuse -> explosion hitting the 8 neighbours
                        e.behaviour = ItemBehaviour.BrickWall;
                        e.kind = ItemKind.Breakable;
                        e.defaultHits = 3;
                        e.fuseTime = 0.7f;
                        e.blastRadius = 1.5f;
                        e.hitSfx = Clip("brick_wall_hit", e.hitSfx);
                        e.breakSfx = Clip("brick_wall_explode", e.breakSfx);
                        break;
                    case 24: // Container: yellow crate over items, HP badge
                        e.behaviour = ItemBehaviour.Container;
                        e.kind = ItemKind.Breakable;
                        e.fitFootprint = false;
                        e.showHitCount = true;
                        e.hitSfx = Clip("container_hit", e.hitSfx);
                        e.breakSfx = Clip("container_burst", e.breakSfx);
                        break;
                    case 7: // Log Cutter: one hit = one log cut
                        e.behaviour = ItemBehaviour.LogCutter;
                        e.kind = ItemKind.Breakable;
                        e.fitFootprint = false;
                        e.partBreakSfx = Clip("log_cut", e.partBreakSfx);
                        e.hitSfx = Clip("log_cut", e.hitSfx);
                        e.breakSfx = Clip("log_cutter_break", e.breakSfx);
                        break;
                    case 6: // Electric Orb: switch every orb on
                        e.behaviour = ItemBehaviour.ElectricOrb;
                        e.kind = ItemKind.Breakable;
                        e.fitFootprint = false;
                        e.orbResetTime = 8f;
                        e.hitSfx = Clip("orb_on", e.hitSfx);
                        e.breakSfx = Clip("orb_discharge", e.breakSfx);
                        e.altSfx = Clip("orb_off", e.altSfx);
                        e.extraBreakSfx = null;
                        break;
                    case 13: // Wooden Box: crates per cell
                        e.behaviour = ItemBehaviour.WoodenBox;
                        e.kind = ItemKind.Breakable;
                        e.fitFootprint = false;
                        e.cellHits = 2;
                        if (string.IsNullOrEmpty(e.stagePrefix)) e.stagePrefix = "stage_";
                        e.hitSfx = Clip("wooden_box_hit", e.hitSfx);
                        e.partBreakSfx = Clip("wooden_box_break", e.partBreakSfx);
                        e.breakSfx = Clip("wooden_box_break", e.breakSfx);
                        break;
                    case 8: // Target Stand: 2 hits, broken target after the first
                        e.behaviour = ItemBehaviour.TargetStand;
                        e.kind = ItemKind.Breakable;
                        e.defaultHits = 2;
                        e.hitSfx = Clip("target_hit", e.hitSfx);
                        e.breakSfx = Clip("target_stand_collapse", e.breakSfx);
                        break;
                    case 9: // Moving Target: 1 hit
                        e.behaviour = ItemBehaviour.TargetStand;
                        e.kind = ItemKind.Breakable;
                        e.defaultHits = 1;
                        e.hitSfx = Clip("target_hit", e.hitSfx);
                        e.breakSfx = Clip("target_break", e.breakSfx);
                        break;
                    case 4: // Moving Barrier: solid, moves along its lane
                        e.hitSfx = Clip("moving_barrier_hit", e.hitSfx);
                        break;
                    case 23: // Stone Corner
                        e.hitSfx = Clip("unbreakable_hit", e.hitSfx);
                        break;
                }
            }
            catalog.behaviourVersion = BehaviourVersion;
        }

        /// <summary>Item type -> original effect prefabs. Only fills empty slots (keeps manual edits).</summary>
        static bool HookVfx(GameAssets assets)
        {
            bool changed = false;
            void Set(ref GameObject slot, string prefab)
            {
                if (slot != null) return;
                var p = VfxExtractor.Load(prefab);
                if (p == null) return;
                slot = p;
                changed = true;
            }
            foreach (var e in assets.catalog.entries)
            {
                switch (e.typeId)
                {
                    case 1: case 26: Set(ref e.breakFx, "VFX_Block_Blast"); break;
                    case 5:
                        if (e.prefab != null && e.prefab.name.Contains("Baloon"))
                        {
                            // Balloon box: the egg shell / pink lid effects don't fit, use wooden pieces.
                            Set(ref e.breakFx, "VFX_WoodenBox_Blast");
                            break;
                        }
                        Set(ref e.openFx, "VFX_EggBox_Lid");
                        Set(ref e.hatchFx, "VFX_EggBox_Egg");
                        Set(ref e.breakFx, "VFX_EggBox_Blast");
                        break;
                    case 8: case 9: Set(ref e.breakFx, "VFX_TargetStand_Blast"); break;
                    case 7: Set(ref e.breakFx, "VFX_LogCutter_Blast"); break;
                    case 13:
                        Set(ref e.breakFx, "VFX_WoodenBox_Blast");
                        Set(ref e.partFx, "VFX_WoodenBox_Blast");
                        break;
                    case 2: Set(ref e.breakFx, "VFX_BrickWall_Blast"); break;
                    case 24:
                        Set(ref e.breakFx, "VFX_Container_Blast");
                        Set(ref e.hitFx, "VFX_Container_Hit");
                        break;
                    case 6: Set(ref e.breakFx, "VFX_ElectricOrb"); break;
                }
            }
            if (assets.splashFx == null)
            {
                var s = VfxExtractor.Load("VFX_Ball_Splash");
                if (s != null) { assets.splashFx = s; changed = true; }
            }
            if (assets.waterMaterial == null)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Water_GardenB.mat");
                if (m != null) { assets.waterMaterial = m; changed = true; }
            }
            return changed;
        }

        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var assets = AssetDatabase.LoadAssetAtPath<GameAssets>(GameAssetsPath);
            if (assets == null) return;

            bool ok = assets.digits != null && assets.digits.Length == 10 && assets.digits.All(d => d != null);
            if (!ok)
            {
                var all = AssetDatabase.LoadAllAssetsAtPath(DigitsPath).OfType<Sprite>().ToList();
                var digits = new Sprite[10];
                for (int i = 0; i < 10; i++) digits[i] = all.FirstOrDefault(s => s.name == "Digit_" + i);
                if (digits.Any(d => d == null))
                {
                    Debug.LogWarning($"[GardenCannon] {DigitsPath} is not sliced into Digit_0..Digit_9 yet (reimport it).");
                    return;
                }
                assets.digits = digits;
                EditorUtility.SetDirty(assets);
                AssetDatabase.SaveAssets();
                Debug.Log("[GardenCannon] Digit sprites assigned to GameAssets.");
            }

            // Catalog defaults changed (e.g. egg box behaviour): rebuild it and register new SFX.
            if (assets.catalog != null && assets.catalog.defaultsVersion < GardenCannonSetup.DefaultsVersion)
            {
                GardenCannonSetup.CreateOrUpdateCatalog();
                GardenCannonSetup.SetupAddressables(assets.catalog, assets);
            }

            bool birdsOk = assets.birdFrames != null && assets.birdFrames.Length == 2 && assets.birdFrames.All(b => b != null);
            if (!birdsOk)
            {
                var all = AssetDatabase.LoadAllAssetsAtPath(BirdPath).OfType<Sprite>().ToList();
                var frames = new[] { all.FirstOrDefault(s => s.name == "Bird_0"), all.FirstOrDefault(s => s.name == "Bird_1") };
                if (frames.All(f => f != null))
                {
                    assets.birdFrames = frames;
                    EditorUtility.SetDirty(assets);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[GardenCannon] Egg box bird frames assigned to GameAssets.");
                }
            }

            // FX sprites: own white atlas (chip / glow / star) + the original confetti pieces.
            if (assets.fxChip == null || assets.fxGlow == null || assets.fxStar == null)
            {
                var fx = AssetDatabase.LoadAllAssetsAtPath(FxAtlasPath).OfType<Sprite>().ToList();
                assets.fxChip = fx.FirstOrDefault(s => s.name == "FX_Chip");
                assets.fxGlow = fx.FirstOrDefault(s => s.name == "FX_Glow");
                assets.fxStar = fx.FirstOrDefault(s => s.name == "FX_Star");
                EditorUtility.SetDirty(assets);
                AssetDatabase.SaveAssets();
                if (assets.fxChip != null) Debug.Log("[GardenCannon] FX sprites assigned to GameAssets.");
            }
            if (assets.confetti == null || assets.confetti.Length != 5 || assets.confetti.Any(c => c == null))
            {
                assets.confetti = Enumerable.Range(1, 5)
                    .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprite/ConfettiParticle0{i}.asset"))
                    .Where(s => s != null).ToArray();
                EditorUtility.SetDirty(assets);
                AssetDatabase.SaveAssets();
            }

            // Obstacle rules (Container, Brick Wall, Log Cutter, Electric Orb, Wooden Box, Target Stand).
            if (assets.catalog != null && assets.catalog.behaviourVersion < BehaviourVersion)
            {
                ApplyBehaviourDefaults(assets.catalog);
                ApplyArt3D(assets);
                HookVfx(assets);
                EditorUtility.SetDirty(assets.catalog);
                EditorUtility.SetDirty(assets);
                AssetDatabase.SaveAssets();
                GardenCannonSetup.SetupAddressables(assets.catalog, assets);
                Debug.Log("[GardenCannon] Obstacle behaviours applied to the ItemCatalog (v" + BehaviourVersion + ").");
            }

            // New music / shot sound and the egg box balloons (checked every time: the files may import later).
            // BG without the old moves plate (the counter is in the top-left badge now).
            if (assets.bg3D != null && (assets.bgMesh == null || AssetDatabase.GetAssetPath(assets.bgMesh) != BgPlateRemover.OutPath))
            {
                var noPlate = BgPlateRemover.LoadOrBuild();
                if (noPlate != null)
                {
                    assets.bgMesh = noPlate;
                    EditorUtility.SetDirty(assets);
                    AssetDatabase.SaveAssets();
                }
            }

            if (ApplyAudioAndBalloons(assets))
            {
                EditorUtility.SetDirty(assets);
                AssetDatabase.SaveAssets();
                GardenCannonSetup.SetupAddressables(assets.catalog, assets);
                Debug.Log("[GardenCannon] Music, shot sound, egg box balloons and the circus lawn (Art/GrassTop.png) assigned.");
            }

            // Original item effects: extract them from Game.unity once, then hook them into the catalog.
            if (!VfxExtractor.AllExtracted()) VfxExtractor.Extract();
            if (assets.catalog != null && HookVfx(assets))
            {
                EditorUtility.SetDirty(assets.catalog);
                EditorUtility.SetDirty(assets);
                AssetDatabase.SaveAssets();
                GardenCannonSetup.SetupAddressables(assets.catalog, assets);
                Debug.Log("[GardenCannon] Original VFX hooked into the ItemCatalog.");
            }

            if (assets.digitShader == null)
            {
                assets.digitShader = AssetDatabase.LoadAssetAtPath<Shader>(OverlayShaderPath);
                if (assets.digitShader != null)
                {
                    EditorUtility.SetDirty(assets);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[GardenCannon] Digit overlay shader assigned to GameAssets.");
                }
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;
            var shaderGuid = AssetDatabase.AssetPathToGUID(OverlayShaderPath);
            if (!string.IsNullOrEmpty(shaderGuid) && settings.FindAssetEntry(shaderGuid) == null)
            {
                var g = settings.FindGroup("LocalShaders") ?? settings.DefaultGroup;
                var e = settings.CreateOrMoveEntry(shaderGuid, g, false, true);
                e.SetAddress("GardenCannon/Shaders/SpriteOverlay", true);
                AssetDatabase.SaveAssets();
            }
            var fxGuid = AssetDatabase.AssetPathToGUID(FxAtlasPath);
            if (!string.IsNullOrEmpty(fxGuid) && settings.FindAssetEntry(fxGuid) == null)
            {
                var fg = settings.FindGroup("Default") ?? settings.DefaultGroup;
                var fe = settings.CreateOrMoveEntry(fxGuid, fg, false, true);
                fe.SetAddress("GardenCannon/Sprites/FxAtlas", true);
                AssetDatabase.SaveAssets();
            }
            var birdGuid = AssetDatabase.AssetPathToGUID(BirdPath);
            if (!string.IsNullOrEmpty(birdGuid) && settings.FindAssetEntry(birdGuid) == null)
            {
                var bg = settings.FindGroup("Default") ?? settings.DefaultGroup;
                var be = settings.CreateOrMoveEntry(birdGuid, bg, false, true);
                be.SetAddress("GardenCannon/Sprites/Bird", true);
                AssetDatabase.SaveAssets();
            }
            var guid = AssetDatabase.AssetPathToGUID(DigitsPath);
            if (settings.FindAssetEntry(guid) == null)
            {
                var group = settings.FindGroup("Default") ?? settings.DefaultGroup;
                var entry = settings.CreateOrMoveEntry(guid, group, false, true);
                entry.SetAddress("GardenCannon/Sprites/Digits", true);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
