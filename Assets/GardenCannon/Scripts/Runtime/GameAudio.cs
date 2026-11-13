using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>Plays the original SFX with a small voice pool and spam protection, plus the gameplay music loop.</summary>
    public class GameAudio : MonoBehaviour
    {
        [Header("Gameplay")]
        public AudioClip ballFire;
        public AudioClip ballFireRefused;
        public AudioClip wallBounce;
        public AudioClip ballCollide;
        public AudioClip waterSplash;
        public AudioClip aimTick;
        public AudioClip lastMoves;
        public AudioClip refillLand;

        [Header("UI / flow")]
        public AudioClip buttonClick;
        public AudioClip appear;
        public AudioClip goalHit;
        public AudioClip goalComplete;
        public AudioClip levelWin;
        public AudioClip levelWinFireworks;
        public AudioClip outOfBalls;
        public AudioClip levelFail;

        [Header("Music")]
        public AudioClip music;
        [Range(0f, 1f)] public float musicVolume = 0.45f;

        [Header("Mix")]
        [Range(0f, 1f)] public float volume = 0.8f;
        public int voices = 14;
        [Tooltip("Same clip cannot restart within this many seconds.")]
        public float minRepeat = 0.035f;

        readonly List<AudioSource> _sources = new List<AudioSource>();
        readonly Dictionary<AudioClip, float> _lastPlay = new Dictionary<AudioClip, float>();
        AudioSource _music;
        int _next;

        /// <summary>Sound effects off.</summary>
        public static bool Muted
        {
            get => PlayerPrefs.GetInt("gc_muted", 0) == 1;
            set { PlayerPrefs.SetInt("gc_muted", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Music off.</summary>
        public static bool MusicMuted
        {
            get => PlayerPrefs.GetInt("gc_music_muted", 0) == 1;
            set { PlayerPrefs.SetInt("gc_music_muted", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        bool _ducked;

        void Awake()
        {
            for (int i = 0; i < voices; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _sources.Add(src);
            }
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.spatialBlend = 0f;
        }

        public void PlayMusic()
        {
            if (_music == null || music == null) return;
            _music.clip = music;
            _music.volume = MusicMuted ? 0f : musicVolume;
            if (!_music.isPlaying) _music.Play();
        }

        /// <summary>Duck the music (e.g. under the win jingle) and bring it back.</summary>
        public void SetMusicDuck(bool ducked)
        {
            _ducked = ducked;
            if (_music != null) _music.volume = MusicMuted ? 0f : musicVolume * (ducked ? 0.3f : 1f);
        }

        public void ToggleMusic()
        {
            MusicMuted = !MusicMuted;
            SetMusicDuck(_ducked);
            if (!MusicMuted) PlayMusic();
        }

        public void ToggleSound() => Muted = !Muted;

        public void Play(AudioClip clip, float vol = 1f, float pitchJitter = 0.06f) => PlayPitched(clip, vol, 1f, pitchJitter);

        public void PlayPitched(AudioClip clip, float vol, float pitch, float pitchJitter = 0f)
        {
            if (clip == null || Muted || _sources.Count == 0) return;
            float now = Time.unscaledTime;
            if (_lastPlay.TryGetValue(clip, out var last) && now - last < minRepeat) return;
            _lastPlay[clip] = now;
            var src = _sources[_next];
            _next = (_next + 1) % _sources.Count;
            src.pitch = pitch + Random.Range(-pitchJitter, pitchJitter);
            src.volume = volume * vol;
            src.clip = clip;
            src.Play();
        }
    }
}
