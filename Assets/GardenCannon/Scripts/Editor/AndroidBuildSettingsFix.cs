using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GardenCannon.EditorTools
{
    /// <summary>
    /// Android build requirements: at least one CPU architecture must be selected ("Target architecture not specified"),
    /// and Google Play needs 64-bit (ARM64), which only exists with IL2CPP. Applied when the editor loads (so it is
    /// saved in ProjectSettings) and again before every Android build.
    /// </summary>
    [InitializeOnLoad]
    public class AndroidBuildSettingsFix : IPreprocessBuildWithReport
    {
        const AndroidArchitecture Wanted = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

        public int callbackOrder => -2000;

        static AndroidBuildSettingsFix()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) Apply(false);
            };
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android) Apply(true);
        }

        [MenuItem("Garden Cannon/Fix Android Build Settings (IL2CPP, ARMv7 + ARM64)", priority = 41)]
        static void Menu() => Apply(true);

        static void Apply(bool log)
        {
            bool changed = false;
            if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) != ScriptingImplementation.IL2CPP)
            {
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
                changed = true;
            }
            if ((PlayerSettings.Android.targetArchitectures & Wanted) != Wanted)
            {
                PlayerSettings.Android.targetArchitectures = PlayerSettings.Android.targetArchitectures | Wanted;
                changed = true;
            }
            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[GardenCannon] Android: scripting backend IL2CPP, architectures ARMv7 + ARM64.");
            }
            else if (log) Debug.Log("[GardenCannon] Android build settings already OK (IL2CPP, ARMv7 + ARM64).");
        }
    }
}
