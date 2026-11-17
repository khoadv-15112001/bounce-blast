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
        [Tooltip("Sideways offset of the barrel axis from the pivot, in the barrel's unrotated frame (+ = right). " +
                 "Set from the 3D model by SceneArt3D so the aim line and balls leave from the middle of the barrel.")]
        public float muzzleSideOffset = 0f;
        [Tooltip("Height of the barrel axis above the ground (world units, towards the camera). The game camera is " +
                 "tilted, so a raised barrel shows up higher on screen: the muzzle is projected along the view onto z = 0.")]
        public float muzzleHeight = 0f;
        [Tooltip("Tilted game camera (default: the CameraRig's camera, else Camera.main).")]
        public Camera viewCamera;
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
        /// <summary>Barrel's right-hand axis (local +X rotated by the aim angle).</summary>
        public Vector2 Side => new Vector2(Mathf.Cos(_angle * Mathf.Deg2Rad), Mathf.Sin(_angle * Mathf.Deg2Rad));
        public Vector2 Muzzle => Pivot + Direction * muzzleDistance + Side * muzzleSideOffset + ViewShift(muzzleHeight);

        /// <summary>
        /// Ground (z = 0) offset that lies on the same view ray as a point raised by <paramref name="height"/>
        /// (z = -height). Zero for a camera looking straight down the Z axis (e.g. the 2D Scene view setup).
        /// </summary>
        Vector2 ViewShift(float height)
        {
            if (Mathf.Abs(height) < 1e-5f) return Vector2.zero;
            var cam = viewCamera;
            if (cam == null)
            {
                var rig = FindObjectOfType<CameraRig>();
                cam = viewCamera = rig != null ? rig.Cam : Camera.main;
            }
            if (cam == null) return Vector2.zero;
            Vector3 d = cam.transform.forward;
            if (d.z < 1e-4f) return Vector2.zero;
            // P = (x, y, -h); G = P - d * (P.z / d.z) = P + d * (h / d.z)
            return new Vector2(d.x, d.y) * (height / d.z);
        }

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
