using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GardenCannon.EditorTools
{
    /// <summary>
    /// Unity never saves keystore passwords in ProjectSettings, so they are filled in every time the editor
    /// loads and again right before each build (Android). Uses bounce_blast.keystore in the project root.
    /// </summary>
    [InitializeOnLoad]
    public class KeystoreAutoFill : IPreprocessBuildWithReport
    {
        const string KeystoreFile = "bounce_blast.keystore"; // project root
        const string KeyAlias = "bounce_blast";
        const string Password = "sonat@123";                 // keystore + alias (PKCS12: same password)

        public int callbackOrder => -1000;

        static KeystoreAutoFill()
        {
            EditorApplication.delayCall += Apply;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android) Apply();
        }

        [MenuItem("Garden Cannon/Fill Android Keystore Passwords", priority = 40)]
        public static void Apply()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string path = Path.Combine(projectRoot, KeystoreFile);
            if (string.IsNullOrEmpty(PlayerSettings.Android.keystoreName) || !File.Exists(PlayerSettings.Android.keystoreName))
            {
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[GardenCannon] Keystore not found: {path}");
                    return;
                }
                PlayerSettings.Android.keystoreName = path.Replace('\\', '/');
            }
            if (string.IsNullOrEmpty(PlayerSettings.Android.keyaliasName)) PlayerSettings.Android.keyaliasName = KeyAlias;
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystorePass = Password;
            PlayerSettings.Android.keyaliasPass = Password;
        }
    }
}
