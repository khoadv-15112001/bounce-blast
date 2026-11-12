using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>Plays the original SFX (Assets/AudioClip) with a small voice pool and spam protection.</summary>
    public class GameAudio : MonoBehaviour
    {
        public AudioClip ballFire;
        public AudioClip ballFireRefused;
        public AudioClip wallBounce;
        public AudioClip ballCollide;
        public AudioClip waterSplash;
        public AudioClip aimTick;
        public AudioClip lastMoves;
        [Range(0f, 1f)] public float volume = 0.8f;
        public int voices = 12;
        [Tooltip("Same clip cannot restart within this many seconds.")]
        public float minRepeat = 0.035f;

        readonly List<AudioSource> _sources = new List<AudioSource>();
        readonly Dictionary<AudioClip, float> _lastPlay = new Dictionary<AudioClip, float>();
        int _next;

        public static bool Muted
        {
            get => PlayerPrefs.GetInt("gc_muted", 0) == 1;
            set { PlayerPrefs.SetInt("gc_muted", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        void Awake()
        {
            for (int i = 0; i < voices; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _sources.Add(src);
            }
        }

        public void Play(AudioClip clip, float vol = 1f, float pitchJitter = 0.06f)
        {
            if (clip == null || Muted || _sources.Count == 0) return;
            float now = Time.unscaledTime;
            if (_lastPlay.TryGetValue(clip, out var last) && now - last < minRepeat) return;
            _lastPlay[clip] = now;
            var src = _sources[_next];
            _next = (_next + 1) % _sources.Count;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.volume = volume * vol;
            src.clip = clip;
            src.Play();
        }
    }
}
