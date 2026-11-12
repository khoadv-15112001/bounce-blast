using UnityEditor;
using UnityEngine;

public static class MenuItems
{
    [MenuItem("Tools/Take a Screenshot")]
    private static void Screenshot()
    {
        var path = EditorUtility.SaveFilePanel("截屏", null, "screenshot_", "png");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        ScreenCapture.CaptureScreenshot(path);
    }
}