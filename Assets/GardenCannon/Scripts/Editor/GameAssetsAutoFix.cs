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
                    case 13: Set(ref e.breakFx, "VFX_WoodenBox_Blast"); break;
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
