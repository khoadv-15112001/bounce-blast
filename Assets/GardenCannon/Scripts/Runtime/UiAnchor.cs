using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Keeps a HUD object at a fixed distance from a screen edge of the UI camera, inside the safe area
    /// (like HeaderPositioner / FooterPositioner / SafeAreaTopAnchor in the original scene).
    /// </summary>
    [ExecuteAlways]
    public class UiAnchor : MonoBehaviour
    {
        public enum Edge { Top, Bottom, Centre }

        public Camera uiCamera;
        public Edge edge = Edge.Top;
        [Tooltip("Offset from the edge in world units (positive = towards the screen centre).")]
        public float offset = 1.33f;
        public float x;
        public bool respectSafeArea = true;

        int _w, _h;
        Rect _safe;

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        void LateUpdate()
        {
            if (Screen.width != _w || Screen.height != _h || Screen.safeArea != _safe) Apply();
        }

        public void Apply()
        {
            if (uiCamera == null) return;
            _w = Screen.width; _h = Screen.height; _safe = Screen.safeArea;
            float size = uiCamera.orthographicSize;
            float centreY = uiCamera.transform.position.y;
            float insetTop = 0f, insetBottom = 0f;
            if (respectSafeArea && _h > 0)
            {
                insetTop = (_h - _safe.yMax) / _h * size * 2f;
                insetBottom = _safe.yMin / _h * size * 2f;
            }
            float y = edge switch
            {
                Edge.Top => centreY + size - insetTop - offset,
                Edge.Bottom => centreY - size + insetBottom + offset,
                _ => centreY + offset,
            };
            var p = transform.position;
            transform.position = new Vector3(uiCamera.transform.position.x + x, y, p.z);
        }
    }
}
