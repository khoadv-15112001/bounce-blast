using UnityEngine;

namespace GardenCannon
{
    /// <summary>Keeps a world rectangle fully visible on any aspect ratio (orthographic camera).</summary>
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways]
    public class CameraFit : MonoBehaviour
    {
        [Tooltip("World rectangle that must always be visible.")]
        public Rect content = new Rect(-7.36f, -6.95f, 14.72f, 24.72f);
        [Tooltip("Vertical alignment when the screen is taller than the content (0 = bottom, 1 = top).")]
        [Range(0f, 1f)] public float verticalAlign = 0.5f;

        Camera _cam;
        int _w, _h;

        void OnEnable() { _cam = GetComponent<Camera>(); Apply(); }

        void Update()
        {
            if (Screen.width != _w || Screen.height != _h) Apply();
        }

        public void Apply()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            _w = Screen.width;
            _h = Screen.height;
            float aspect = _h > 0 ? _w / (float)_h : 0.5625f;
            _cam.orthographic = true;
            float sizeForHeight = content.height * 0.5f;
            float sizeForWidth = content.width * 0.5f / Mathf.Max(0.01f, aspect);
            float size = Mathf.Max(sizeForHeight, sizeForWidth);
            _cam.orthographicSize = size;
            float extra = size * 2f - content.height;
            float y = content.center.y + (verticalAlign - 0.5f) * -extra;
            var p = transform.position;
            transform.position = new Vector3(content.center.x, y, p.z);
        }
    }
}
