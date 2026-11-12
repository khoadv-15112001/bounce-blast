using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GardenCannon
{
    /// <summary>
    /// SpriteRenderer whose sprite comes from Addressables. The scene only stores the AssetReference,
    /// so nothing is pulled into the scene directly. In edit mode a hidden, non-saved preview child shows
    /// the sprite so the layout can be arranged visually.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class AddressableSprite : MonoBehaviour
    {
        public AssetReferenceSprite sprite;

        AsyncOperationHandle<Sprite> _handle;
        SpriteRenderer _renderer;

        public bool IsDone => !Application.isPlaying || sprite == null || !sprite.RuntimeKeyIsValid() || (_handle.IsValid() && _handle.IsDone);
        public SpriteRenderer Renderer => _renderer != null ? _renderer : (_renderer = GetComponent<SpriteRenderer>());

        void OnEnable()
        {
            if (Application.isPlaying)
            {
                DestroyPreview();
                Load();
            }
#if UNITY_EDITOR
            else RefreshPreview();
#endif
        }

        void Load()
        {
            if (_handle.IsValid() || sprite == null || !sprite.RuntimeKeyIsValid()) return;
            _handle = Addressables.LoadAssetAsync<Sprite>(sprite.RuntimeKey);
            _handle.Completed += h =>
            {
                if (this == null) return;
                if (h.Status == AsyncOperationStatus.Succeeded) Renderer.sprite = h.Result;
                else Debug.LogError($"[GardenCannon] Could not load sprite for '{name}' from Addressables.");
            };
        }

        void OnDestroy()
        {
            if (_handle.IsValid()) Addressables.Release(_handle);
            DestroyPreview();
        }

        const string PreviewName = "~EditorPreview";

        void DestroyPreview()
        {
            var t = transform.Find(PreviewName);
            if (t == null) return;
            if (Application.isPlaying) Destroy(t.gameObject);
            else DestroyImmediate(t.gameObject);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += () => { if (this != null) RefreshPreview(); };
        }

        void OnDisable()
        {
            if (!Application.isPlaying) DestroyPreview();
        }

        void Update()
        {
            if (Application.isPlaying) return;
            var t = transform.Find(PreviewName);
            if (t == null) { RefreshPreview(); return; }
            var p = t.GetComponent<SpriteRenderer>();
            var r = Renderer;
            p.color = r.color;
            p.sortingOrder = r.sortingOrder;
            p.sortingLayerID = r.sortingLayerID;
            p.flipX = r.flipX;
            p.flipY = r.flipY;
        }

        /// <summary>Edit mode only: show the referenced sprite on a child that is never saved.</summary>
        public void RefreshPreview()
        {
            if (Application.isPlaying || this == null) return;
            DestroyPreview();
            Sprite s = null;
            if (sprite != null && !string.IsNullOrEmpty(sprite.AssetGUID))
            {
                s = sprite.editorAsset as Sprite;
                if (s == null)
                    s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(UnityEditor.AssetDatabase.GUIDToAssetPath(sprite.AssetGUID));
            }
            if (s == null) return;
            var go = new GameObject(PreviewName) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            var p = go.AddComponent<SpriteRenderer>();
            p.sprite = s;
            var r = Renderer;
            p.color = r.color;
            p.sortingOrder = r.sortingOrder;
            p.sortingLayerID = r.sortingLayerID;
        }
#endif
    }
}
