using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Camera setup of the original Game scene:
    ///  - orthographic, size 12.8 on the reference screen (1440x3120); narrower screens grow the size to keep the
    ///    width (CameraSizeAdjuster), wider screens show more of the garden on the sides;
    ///  - tilted 20° around X so the 3D items show their front faces, with the projection stretched by
    ///    1/cos(tilt) so the flat ground keeps its proportions (CameraVerticalStretch);
    ///  - an optional UI camera that gets the same size (HUD layer, not tilted).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public Vector2Int referenceResolution = new Vector2Int(1440, 3120);
        public float referenceSize = 12.8f;
        [Range(0f, 45f)] public float tilt = 20f;
        public bool verticalStretch = true;
        [Tooltip("World point the camera looks at (centre of the screen).")]
        public Vector3 focus = Vector3.zero;
        public float distance = 33.33f;
        public Camera uiCamera;

        Camera _cam;
        int _w, _h;
        float _shake;
        Vector3 _shakeOffset;

        public Camera Cam => _cam != null ? _cam : (_cam = GetComponent<Camera>());
        public float Size { get; private set; }

        void OnEnable() => Apply();

        void LateUpdate()
        {
            if (Screen.width != _w || Screen.height != _h) Apply();
            if (Application.isPlaying)
            {
                _shake = Mathf.Max(0f, _shake - Time.unscaledDeltaTime * 3.5f);
                _shakeOffset = _shake > 0f ? (Vector3)(Random.insideUnitCircle * (_shake * _shake * 0.35f)) : Vector3.zero;
                Place();
            }
        }

        void OnValidate() => Apply();

        /// <summary>Add a short screen shake (0..1).</summary>
        public void Shake(float amount) => _shake = Mathf.Clamp01(Mathf.Max(_shake, amount));

        public void Apply()
        {
            var cam = Cam;
            _w = Screen.width;
            _h = Screen.height;
            float aspect = _h > 0 ? _w / (float)_h : referenceResolution.x / (float)referenceResolution.y;
            float refAspect = referenceResolution.x / (float)Mathf.Max(1, referenceResolution.y);
            Size = aspect < refAspect ? referenceSize * refAspect / aspect : referenceSize;

            cam.orthographic = true;
            cam.orthographicSize = Size;
            cam.ResetProjectionMatrix();
            if (verticalStretch && tilt > 0.01f)
                cam.projectionMatrix = cam.projectionMatrix * Matrix4x4.Scale(new Vector3(1f, 1f / Mathf.Cos(tilt * Mathf.Deg2Rad), 1f));
            Place();

            if (uiCamera != null)
            {
                uiCamera.orthographic = true;
                uiCamera.orthographicSize = Size;
            }
        }

        void Place()
        {
            var rot = Quaternion.Euler(-tilt, 0f, 0f);
            transform.rotation = rot;
            transform.position = focus + _shakeOffset - rot * Vector3.forward * distance;
        }

        /// <summary>Screen point -> point on the ground plane (z = 0).</summary>
        public Vector2 ScreenToGround(Vector3 screen)
        {
            var ray = Cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.z) < 1e-5f) return ray.origin;
            float t = -ray.origin.z / ray.direction.z;
            return ray.origin + ray.direction * t;
        }
    }
}
