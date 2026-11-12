using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace GardenCannon.EditorTools
{
    /// <summary>
    /// While building, make sure the Splash scene carries its background / logo sprites directly
    /// (so the first frame after the Unity logo already shows the splash art), even if the scene was never re-saved.
    /// </summary>
    public class SplashBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var view in root.GetComponentsInChildren<SplashView>(true))
                    view.EnsureSprites();
        }
    }
}
