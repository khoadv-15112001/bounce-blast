using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GardenCannon.EditorTools
{
    /// <summary>
    /// Saves every popup as its own prefab in Assets/GardenCannon/Prefabs/Popups (once: an existing prefab is never
    /// overwritten, so hand edits stay) and links them to GameAssets. "Rebuild Popup Prefabs" re-creates them from the
    /// default layout (overwrites the hand edits, asks first).
    /// </summary>
    public static class PopupPrefabBuilder
    {
        public const string Folder = "Assets/GardenCannon/Prefabs/Popups";

        static PopupLayout.Kit KitFrom(GameAssets a) => new PopupLayout.Kit
        {
            skin = a.popupSkin,
            font = a.font,
            white = a.white,
            text = new Color(0.45f, 0.2f, 0.1f),
            titleOutline = new Color(0.42f, 0.05f, 0.12f),
            buttonOutline = new Color(0.2f, 0.12f, 0.05f, 0.9f),
        };

        /// <summary>Create the missing popup prefabs (or all of them with <paramref name="overwrite"/>) and link them. Returns true when GameAssets changed.</summary>
        public static bool Ensure(GameAssets assets, bool overwrite)
        {
            if (assets == null || assets.font == null || assets.popupSkin == null || !assets.popupSkin.Valid) return false;
            if (assets.popupPrefabs == null) assets.popupPrefabs = new PopupPrefabs();
            EnsureFolder(Folder);
            var kit = KitFrom(assets);
            var p = assets.popupPrefabs;
            bool changed = false;
            changed |= One(PopupLayout.Win, PopupLayout.BuildWin, kit, overwrite, ref p.win);
            changed |= One(PopupLayout.Lose, PopupLayout.BuildLose, kit, overwrite, ref p.lose);
            changed |= One(PopupLayout.Settings, PopupLayout.BuildSettings, kit, overwrite, ref p.settings);
            changed |= One(PopupLayout.Cheat, PopupLayout.BuildCheat, kit, overwrite, ref p.cheat);
            changed |= One(PopupLayout.Password, PopupLayout.BuildPassword, kit, overwrite, ref p.password);
            changed |= One(PopupLayout.LevelInput, PopupLayout.BuildLevelInput, kit, overwrite, ref p.levelInput);
            changed |= One(PopupLayout.Haptics, PopupLayout.BuildHaptics, kit, overwrite, ref p.haptics);
            if (changed) EditorUtility.SetDirty(assets);
            return changed;
        }

        static bool One(string name, Func<PopupLayout.Kit, GameObject> build, PopupLayout.Kit kit, bool overwrite, ref GameObject slot)
        {
            string path = Folder + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing == null || overwrite)
            {
                var go = build(kit);
                try { existing = PrefabUtility.SaveAsPrefabAsset(go, path); }
                finally { UnityEngine.Object.DestroyImmediate(go); }
                Debug.Log($"[GardenCannon] Popup prefab {(overwrite ? "rebuilt" : "created")}: {path}");
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

        [MenuItem("Garden Cannon/Rebuild Popup Prefabs (overwrites hand edits)")]
        static void Rebuild()
        {
            if (!EditorUtility.DisplayDialog("Rebuild popup prefabs",
                    "Re-create every popup prefab in " + Folder + " from the default layout?\nHand edits to them will be lost.",
                    "Rebuild", "Cancel")) return;
            var assets = AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/GardenCannon/Data/GameAssets.asset");
            if (assets == null) { Debug.LogWarning("[GardenCannon] GameAssets not found."); return; }
            Ensure(assets, true);
            AssetDatabase.SaveAssets();
        }
    }
}
