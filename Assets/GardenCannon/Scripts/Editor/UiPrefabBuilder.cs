using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace GardenCannon.EditorTools
{
    /// <summary>
    /// Saves the HUD canvas and every canvas popup as prefabs in Assets/GardenCannon/Prefabs/UI (once: an existing prefab
    /// is never overwritten, so hand edits stay) and links them to GameAssets. "Rebuild UI Prefabs" re-creates them from
    /// the default layout (overwrites hand edits, asks first).
    /// </summary>
    public static class UiPrefabBuilder
    {
        public const string Folder = "Assets/GardenCannon/Prefabs/UI";
        public const string PopupFolder = Folder + "/Popups";
        const string TmpSettings = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        static UiLayout.Kit KitFrom(GameAssets a) => new UiLayout.Kit
        {
            theme = a.uiTheme,
            skin = a.popupSkin,
            white = a.white,
            badge = a.movesBadgeSprite,
            boosterPlate = a.boosterPlateSprite,
            boosterBubble = a.boosterBubbleSprite,
            boosterIcon = id => id switch
            {
                BoosterId.X2 => a.boosterX2Icon,
                BoosterId.Arrow => a.boosterArrowIcon,
                BoosterId.Bowling => a.boosterBowlingIcon,
                _ => a.boosterBombIcon,
            },
            bodyColor = new Color(0.45f, 0.2f, 0.1f),
        };

        /// <summary>Create the missing UI prefabs (or all with <paramref name="overwrite"/>) and link them. Returns true when GameAssets changed.</summary>
        public static bool Ensure(GameAssets assets, bool overwrite)
        {
            if (assets == null || assets.uiTheme == null || !assets.uiTheme.Valid) return false;
            if (assets.popupSkin == null || !assets.popupSkin.Valid) return false;
            // TextMeshPro needs its settings / shaders (TMP Essential Resources) before building texts.
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TmpSettings) == null) return false;
            EnsureFolder(PopupFolder);
            var kit = KitFrom(assets);
            bool changed = One(Folder, UiLayout.Hud, UiLayout.BuildHud, kit, overwrite, ref assets.hudCanvasPrefab);
            // Popups: not referenced by GameAssets, loaded through Addressables by address.
            GameObject dummy = null;
            Popup(UiLayout.Win, UiLayout.BuildWin, kit, overwrite, ref dummy);
            Popup(UiLayout.Lose, UiLayout.BuildLose, kit, overwrite, ref dummy);
            Popup(UiLayout.Settings, UiLayout.BuildSettings, kit, overwrite, ref dummy);
            Popup(UiLayout.Cheat, UiLayout.BuildCheat, kit, overwrite, ref dummy);
            Popup(UiLayout.Password, UiLayout.BuildPassword, kit, overwrite, ref dummy);
            Popup(UiLayout.LevelInput, UiLayout.BuildLevelInput, kit, overwrite, ref dummy);
            Popup(UiLayout.Haptics, UiLayout.BuildHaptics, kit, overwrite, ref dummy);
            if (changed) EditorUtility.SetDirty(assets);
            return changed;
        }

        /// <summary>Create the popup prefab if missing and make it addressable (address GardenCannon/UI/&lt;name&gt;).</summary>
        static void Popup(string name, Func<UiLayout.Kit, GameObject> build, UiLayout.Kit kit, bool overwrite, ref GameObject slot)
        {
            One(PopupFolder, name, build, kit, overwrite, ref slot);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;
            string guid = AssetDatabase.AssetPathToGUID(PopupFolder + "/" + name + ".prefab");
            if (string.IsNullOrEmpty(guid)) return;
            string address = UiLayout.Address(name);
            var entry = settings.FindAssetEntry(guid);
            if (entry != null && entry.address == address) return;
            var group = settings.FindGroup("Default") ?? settings.DefaultGroup;
            entry = settings.CreateOrMoveEntry(guid, group, false, true);
            entry.SetAddress(address, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GardenCannon] Popup addressable: {address}");
        }

        static bool One(string folder, string name, Func<UiLayout.Kit, GameObject> build, UiLayout.Kit kit, bool overwrite, ref GameObject slot)
        {
            string path = folder + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing == null || overwrite)
            {
                var go = build(kit);
                try { existing = PrefabUtility.SaveAsPrefabAsset(go, path); }
                finally { UnityEngine.Object.DestroyImmediate(go); }
                Debug.Log($"[GardenCannon] UI prefab {(overwrite ? "rebuilt" : "created")}: {path}");
            }
            if (existing != null && slot != existing) { slot = existing; return true; }
            return false;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [MenuItem("Garden Cannon/Rebuild UI Prefabs (overwrites hand edits)")]
        static void Rebuild()
        {
            if (!EditorUtility.DisplayDialog("Rebuild UI prefabs",
                    "Re-create the HUD canvas and every popup prefab in " + Folder + " from the default layout?\nHand edits to them will be lost.",
                    "Rebuild", "Cancel")) return;
            var assets = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/GardenCannon/Data/GameAssets.asset");
            if (assets == null) { Debug.LogWarning("[GardenCannon] GameAssets not found."); return; }
            if (!Ensure(assets, true)) Debug.LogWarning("[GardenCannon] UI prefabs not rebuilt: TMP font / popup art / TMP Essential Resources missing.");
            AssetDatabase.SaveAssets();
        }
    }
}
