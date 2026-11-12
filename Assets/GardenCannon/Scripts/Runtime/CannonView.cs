using UnityEngine;

namespace GardenCannon
{
    /// <summary>Rotating cannon barrel with a small recoil kick.</summary>
    public class CannonView : MonoBehaviour
    {
        [Tooltip("Rotates around this transform's position.")]
        public Transform barrel;
        [Tooltip("Distance from the pivot to the muzzle (world units).")]
        public float muzzleDistance = 1.4f;
        public float maxAngle = 75f;
        public float recoilDistance = 0.18f;

        float _angle;
        float _recoil;
        Vector3 _barrelBase;

        public float Angle => _angle;
        public Vector2 Pivot => transform.position;
        public Vector2 Direction => new Vector2(-Mathf.Sin(_angle * Mathf.Deg2Rad), Mathf.Cos(_angle * Mathf.Deg2Rad));
        public Vector2 Muzzle => Pivot + Direction * muzzleDistance;

        void Awake()
        {
            if (barrel != null) _barrelBase = barrel.localPosition;
        }

        /// <summary>Aim at a world point. Points below the pivot snap to the max angle on that side.</summary>
        public void AimAt(Vector2 world)
        {
            Vector2 d = world - Pivot;
            float a = Mathf.Atan2(-d.x, d.y) * Mathf.Rad2Deg; // 0 = straight up, positive = left
            if (d.y < 0f) a = d.x < 0f ? maxAngle : -maxAngle;
            SetAngle(a);
        }

        public void SetAngle(float degrees)
        {
            _angle = Mathf.Clamp(degrees, -maxAngle, maxAngle);
            if (barrel != null) barrel.localRotation = Quaternion.Euler(0f, 0f, _angle);
        }

        public void Kick() => _recoil = 1f;

        void Update()
        {
            if (barrel == null) return;
            _recoil = Mathf.Max(0f, _recoil - Time.deltaTime * 6f);
            barrel.localPosition = _barrelBase - (Vector3)(Direction * (_recoil * recoilDistance));
        }
    }
}
