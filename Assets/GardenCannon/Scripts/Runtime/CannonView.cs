using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Rotating cannon. With the original CannonController the barrel plays "Fire" on each shot and "PlayOn"
    /// (refill) at level start; without it a simple code recoil is used.
    /// The original clips animate the path Piv/Base/Rail below the Animator, so that hierarchy is created at runtime
    /// around the scene's barrel (the scene itself is not changed).
    /// </summary>
    public class CannonView : MonoBehaviour
    {
        [Tooltip("Rotates around this transform's position (original: Rail).")]
        public Transform barrel;
        [Tooltip("Distance from the pivot to the muzzle (original Tip: 2.5).")]
        public float muzzleDistance = 2.5f;
        public float maxAngle = 75f;
        public float recoilDistance = 0.25f;

        float _angle;
        float _recoil;
        Vector3 _barrelBase;
        Animator _animator;

        static readonly int FireState = Animator.StringToHash("Fire");
        static readonly int PlayOnState = Animator.StringToHash("PlayOn");

        public float Angle => _angle;
        public Vector2 Pivot => transform.position;
        public Vector2 Direction => new Vector2(-Mathf.Sin(_angle * Mathf.Deg2Rad), Mathf.Cos(_angle * Mathf.Deg2Rad));
        public Vector2 Muzzle => Pivot + Direction * muzzleDistance;

        void Awake()
        {
            if (barrel != null) _barrelBase = barrel.localPosition;
        }

        /// <summary>Hook up the original cannon animator (Idle / Fire / PlayOn).</summary>
        public void UseAnimator(RuntimeAnimatorController controller)
        {
            if (controller == null || barrel == null || _animator != null) return;
            // Build CannonBase/Piv/Base/Rail like the original so the clip paths resolve.
            var piv = new GameObject("Piv").transform;
            piv.SetParent(transform, false);
            var b = new GameObject("Base").transform;
            b.SetParent(piv, false);
            var rot = barrel.localRotation;
            barrel.SetParent(b, false);
            barrel.name = "Rail";
            barrel.localPosition = Vector3.zero;
            barrel.localRotation = rot;
            _barrelBase = Vector3.zero;

            _animator = gameObject.GetComponent<Animator>();
            if (_animator == null) _animator = gameObject.AddComponent<Animator>();
            _animator.runtimeAnimatorController = controller;
            _animator.applyRootMotion = false;
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
        }

        public void Kick()
        {
            if (_animator != null && _animator.HasState(0, FireState)) _animator.Play(FireState, 0, 0f);
            else _recoil = 1f;
        }

        public void PlayRefill()
        {
            if (_animator != null && _animator.HasState(0, PlayOnState)) _animator.Play(PlayOnState, 0, 0f);
        }

        void LateUpdate()
        {
            if (barrel == null) return;
            // Rotation is applied after the animator so the clips (position/scale only) and aiming combine.
            barrel.localRotation = Quaternion.Euler(0f, 0f, _angle);
            if (_animator == null)
            {
                _recoil = Mathf.Max(0f, _recoil - Time.deltaTime * 6f);
                barrel.localPosition = _barrelBase - (Vector3)(Direction * (_recoil * recoilDistance));
            }
        }
    }
}
