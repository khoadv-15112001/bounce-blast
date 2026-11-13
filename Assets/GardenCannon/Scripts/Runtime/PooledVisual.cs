using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Pooled instance of an item prefab. Remembers the prefab's original state when first created
    /// (active flags, local transforms, meshes) and puts it back on reuse, so levels can reuse visuals instead of
    /// Instantiate/Destroy. Also caches the hit-animation info so hits don't allocate.
    /// </summary>
    public class PooledVisual : MonoBehaviour
    {
        public GameObject Prefab { get; private set; }
        public Animator[] Animators { get; private set; }
        /// <summary>Per animator: 1 = has trigger "shake", 2 = has state "Shake", 3 = has state "shake", 0 = none.</summary>
        public int[] ShakeMode { get; private set; }

        Transform[] _transforms;
        bool[] _active;
        Vector3[] _pos, _scale;
        Quaternion[] _rot;
        MeshFilter[] _filters;
        Mesh[] _meshes;
        Renderer[] _renderers;

        static readonly int ShakeTrigger = Animator.StringToHash("shake");
        static readonly int ShakeUpper = Animator.StringToHash("Shake");
        static readonly int ShakeLower = Animator.StringToHash("shake");

        public void Capture(GameObject prefab)
        {
            Prefab = prefab;
            _transforms = GetComponentsInChildren<Transform>(true);
            int n = _transforms.Length;
            _active = new bool[n]; _pos = new Vector3[n]; _scale = new Vector3[n]; _rot = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                var t = _transforms[i];
                _active[i] = t.gameObject.activeSelf;
                _pos[i] = t.localPosition; _rot[i] = t.localRotation; _scale[i] = t.localScale;
            }
            _filters = GetComponentsInChildren<MeshFilter>(true);
            _meshes = new Mesh[_filters.Length];
            for (int i = 0; i < _filters.Length; i++) _meshes[i] = _filters[i].sharedMesh;
            _renderers = GetComponentsInChildren<Renderer>(true);

            Animators = GetComponentsInChildren<Animator>(true);
            ShakeMode = new int[Animators.Length];
            _resolved = new bool[Animators.Length];
        }

        bool[] _resolved;

        /// <summary>Look the shake trigger/state up once, the first time the animator is active (no per-hit allocation).</summary>
        void Resolve(int i)
        {
            var a = Animators[i];
            if (_resolved[i] || a == null || !a.isActiveAndEnabled) return;
            _resolved[i] = true;
            if (a.runtimeAnimatorController == null) return;
            foreach (var p in a.parameters)
                if (p.nameHash == ShakeTrigger && p.type == AnimatorControllerParameterType.Trigger) { ShakeMode[i] = 1; return; }
            if (a.HasState(0, ShakeUpper)) ShakeMode[i] = 2;
            else if (a.HasState(0, ShakeLower)) ShakeMode[i] = 3;
        }

        /// <summary>Back to the prefab's original state.</summary>
        public void Restore()
        {
            for (int i = 0; i < _transforms.Length; i++)
            {
                var t = _transforms[i];
                if (t == null) continue;
                t.localPosition = _pos[i]; t.localRotation = _rot[i]; t.localScale = _scale[i];
                if (t.gameObject.activeSelf != _active[i]) t.gameObject.SetActive(_active[i]);
            }
            for (int i = 0; i < _filters.Length; i++)
                if (_filters[i] != null) _filters[i].sharedMesh = _meshes[i];
            foreach (var r in _renderers)
                if (r != null && r.HasPropertyBlock()) r.SetPropertyBlock(null);
            foreach (var a in Animators)
                if (a != null && a.isActiveAndEnabled) { a.Rebind(); a.Update(0f); }
        }

        public void PlayShake()
        {
            for (int i = 0; i < Animators.Length; i++)
            {
                var a = Animators[i];
                if (a == null || !a.isActiveAndEnabled) continue;
                Resolve(i);
                switch (ShakeMode[i])
                {
                    case 1: a.SetTrigger(ShakeTrigger); break;
                    case 2: a.Play(ShakeUpper, 0, 0f); break;
                    case 3: a.Play(ShakeLower, 0, 0f); break;
                }
            }
        }
    }
}
