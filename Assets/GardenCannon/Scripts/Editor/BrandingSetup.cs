using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GardenCannon.EditorTools
{
    /// <summary>
    /// Player Settings branding:
    ///  - Splash screen like Frenze-Hole: SONAT logo (light on dark), background (0.137, 0.122, 0.125), dolly
    ///    animation, Unity logo hidden (hiding it needs a Unity Pro / Plus licence, otherwise Unity keeps it).
    ///  - App icon: Art/Branding/AppIcon.png (1024x1024, cropped from the original splash art) as the default icon.
    /// </summary>
    [InitializeOnLoad]
    public static class BrandingSetup
    {
        const string Folder = "Assets/GardenCannon/Art/Branding";
        const string SonatLogo = Folder + "/logo_sonat.png";
        const string AppIcon = Folder + "/AppIcon.png";

        // Applied automatically after each script reload when Player Settings don't have the branding yet.
        static BrandingSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || IsApplied()) return;
                Apply();
            };
        }

        static bool IsApplied()
        {
            var logo = AssetDatabase.LoadAllAssetsAtPath(SonatLogo).OfType<Sprite>().FirstOrDefault();
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIcon);
            bool logoOk = logo == null || PlayerSettings.SplashScreen.logos.Any(l => l.logo == logo);
            var icons = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown);
            bool iconOk = icon == null || (icons != null && icons.Length > 0 && icons[0] == icon);
            return logoOk && iconOk;
        }

        [MenuItem("Garden Cannon/Apply Splash (SONAT) + App Icon", priority = 30)]
        public static void Apply()
        {
            ConfigureImporters();

            // ---- Splash screen (values copied from Frenze-Hole's ProjectSettings)
            var logo = AssetDatabase.LoadAllAssetsAtPath(SonatLogo).OfType<Sprite>().FirstOrDefault();
            if (logo != null)
            {
                PlayerSettings.SplashScreen.show = true;
                PlayerSettings.SplashScreen.showUnityLogo = false;
                PlayerSettings.SplashScreen.backgroundColor = new Color(0.13725491f, 0.12156863f, 0.1254902f, 1f);
                PlayerSettings.SplashScreen.overlayOpacity = 1f;
                PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;
                PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
                PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.UnityLogoBelow;
                PlayerSettings.SplashScreen.background = null;
                PlayerSettings.SplashScreen.backgroundPortrait = null;
                PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2f, logo) };
            }
            else Debug.LogWarning("[GardenCannon] Missing splash logo " + SonatLogo);

            // ---- App icon (default icon = used for every platform / size)
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIcon);
            if (icon != null) PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
            else Debug.LogWarning("[GardenCannon] Missing app icon " + AppIcon);

            AssetDatabase.SaveAssets();
            Debug.Log("[GardenCannon] Splash (SONAT) and app icon applied. Preview: Player Settings > Splash Image > Preview.");
        }

        static void ConfigureImporters()
        {
            if (AssetImporter.GetAtPath(SonatLogo) is TextureImporter logo &&
                (logo.textureType != TextureImporterType.Sprite || logo.spriteImportMode != SpriteImportMode.Single))
            {
                logo.textureType = TextureImporterType.Sprite;
                logo.spriteImportMode = SpriteImportMode.Single;
                logo.mipmapEnabled = false;
                logo.alphaIsTransparency = true;
                logo.SaveAndReimport();
            }
            if (AssetImporter.GetAtPath(AppIcon) is TextureImporter icon &&
                (icon.textureType != TextureImporterType.Default || icon.maxTextureSize != 1024 || icon.mipmapEnabled))
            {
                icon.textureType = TextureImporterType.Default;
                icon.mipmapEnabled = false;
                icon.maxTextureSize = 1024;
                icon.npotScale = TextureImporterNPOTScale.None;
                icon.textureCompression = TextureImporterCompression.Uncompressed;
                icon.SaveAndReimport();
            }
        }
    }
}
