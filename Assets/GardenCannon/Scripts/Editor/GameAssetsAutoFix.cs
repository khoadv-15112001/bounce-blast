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

        const int BehaviourVersion = 1;

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
                HookVfx(assets);
                EditorUtility.SetDirty(assets.catalog);
                EditorUtility.SetDirty(assets);
                AssetDatabase.SaveAssets();
                GardenCannonSetup.SetupAddressables(assets.catalog, assets);
                Debug.Log("[GardenCannon] Obstacle behaviours applied to the ItemCatalog (v" + BehaviourVersion + ").");
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
