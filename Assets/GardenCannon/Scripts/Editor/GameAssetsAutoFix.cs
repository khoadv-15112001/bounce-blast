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

        static GameAssetsAutoFix()
        {
            EditorApplication.delayCall += Run;
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
