using UnityEngine;

namespace GardenCannon
{
    /// <summary>Stretches this RectTransform over the device safe area (notch / rounded corners / home bar).</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        Rect _safe;
        Vector2Int _screen;

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.safeArea != _safe || Screen.width != _screen.x || Screen.height != _screen.y) Apply();
        }

        void Apply()
        {
            _safe = Screen.safeArea;
            _screen = new Vector2Int(Screen.width, Screen.height);
            if (_screen.x <= 0 || _screen.y <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(_safe.xMin / _screen.x, _safe.yMin / _screen.y);
            rt.anchorMax = new Vector2(_safe.xMax / _screen.x, _safe.yMax / _screen.y);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
