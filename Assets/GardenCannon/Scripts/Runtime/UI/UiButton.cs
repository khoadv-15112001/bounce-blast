using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GardenCannon
{
    /// <summary>
    /// Canvas button: shrinks while pressed, bounces back on release, runs <see cref="onClick"/> a moment later so the
    /// press reads. <see cref="id"/> tells the game what the button does (popup prefabs are hooked by id).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UiButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [Tooltip("What the button does (set by the game): replay, next, close, ok, cancel, win, lose...")]
        public string id;
        public Image art;
        public TMP_Text label;
        public bool interactable = true;
        [Header("Press animation")]
        public float pressScale = 0.86f;
        public float pressTime = 0.07f;
        public float releaseTime = 0.2f;
        [Tooltip("Delay between the click and the action.")]
        public float actionDelay = 0.1f;

        /// <summary>The action (set by the game).</summary>
        [NonSerialized] public Action onClick;
        /// <summary>Any button clicked (click sound / haptic).</summary>
        public static Action AnyClicked;
        static float _blockUntil;

        Vector3 _base;
        bool _baseSet, _down;
        float _k = 1f, _kRelease = 1f, _release = -1f;

        void Awake() => CacheBase();

        void CacheBase()
        {
            if (_baseSet) return;
            _base = transform.localScale;
            _baseSet = true;
        }

        void OnDisable()
        {
            if (!_baseSet) return;
            _down = false;
            _release = -1f;
            _k = 1f;
            transform.localScale = _base;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!interactable) return;
            CacheBase();
            _down = true;
            _release = -1f;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_down) return;
            _down = false;
            _kRelease = _k;
            _release = 0f;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!interactable || onClick == null) return;
            if (Time.unscaledTime < _blockUntil) return;   // one action at a time
            _blockUntil = Time.unscaledTime + actionDelay + 0.02f;
            AnyClicked?.Invoke();
            var a = onClick;
            if (actionDelay <= 0f || !isActiveAndEnabled) a();
            else StartCoroutine(Later(a));
        }

        IEnumerator Later(Action a)
        {
            yield return new WaitForSecondsRealtime(actionDelay);
            a?.Invoke();
        }

        void Update()
        {
            if (!_baseSet) return;
            float dt = Time.unscaledDeltaTime;
            if (_down)
                _k = Mathf.MoveTowards(_k, pressScale, dt * (1f - pressScale) / Mathf.Max(0.01f, pressTime));
            else if (_release >= 0f)
            {
                _release += dt / Mathf.Max(0.01f, releaseTime);
                float u = Mathf.Clamp01(_release) - 1f;
                const float c1 = 1.9f, c3 = c1 + 1f;
                _k = Mathf.LerpUnclamped(_kRelease, 1f, 1f + c3 * u * u * u + c1 * u * u);
                if (_release >= 1f) { _k = 1f; _release = -1f; }
            }
            else return;
            transform.localScale = _base * _k;
        }
    }
}
