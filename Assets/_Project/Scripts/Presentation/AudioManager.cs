using Tarneeb.Config;
using UnityEngine;

namespace Tarneeb.Presentation
{
    public enum Sfx
    {
        CardFlip, CardPlace, TrickWin, RoundWin, RoundLose, Click, CardDeal, CardSlide, Whoosh, Bid, Pass, TurnPing,
        TimerTick, Deny, Pop, Sparkle, GameWin, GameLose, Coin, ToggleOn, ToggleOff
    }

    public enum MusicTrack { None, Menu, Table }

    /// <summary>
    /// Sound effects (a small pool of sources so overlapping sounds can have their own pitch) and background music with
    /// cross-fades. One instance lives across scenes (DontDestroyOnLoad); the second scene's copy destroys itself.
    /// Browsers unlock audio on the first user gesture; Unity WebGL resumes the context by itself.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        /// <summary>Wired by the scene builder in Sfx enum order (see GameSceneAssembler.WireAudio).</summary>
        public AudioClip[] sfxClips;
        public AudioClip menuMusic, tableMusic;

        private const int SfxSources = 8;
        private AudioSource[] _sfx;
        private AudioSource _musicA, _musicB, _active;
        private int _next;
        private MusicTrack _track = MusicTrack.None;
        private float _duck = 1f, _duckTarget = 1f, _fade;
        private AudioClip _incoming;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Keep the living instance; hand over any clips this scene's copy has that the old one lacks.
                if (Instance.sfxClips == null || Instance.sfxClips.Length == 0) Instance.sfxClips = sfxClips;
                if (Instance.menuMusic == null) Instance.menuMusic = menuMusic;
                if (Instance.tableMusic == null) Instance.tableMusic = tableMusic;
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            _sfx = new AudioSource[SfxSources];
            for (int i = 0; i < SfxSources; i++) _sfx[i] = NewSource(false);
            _musicA = NewSource(true);
            _musicB = NewSource(true);
            _active = _musicA;
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL decodes lazily ("sound which is not loaded yet"); load the tiny effects up front so the first play is instant.
            if (sfxClips != null) foreach (AudioClip c in sfxClips) if (c != null) c.LoadAudioData();
#endif
        }

        private AudioSource NewSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.volume = loop ? 0f : 1f;
            return s;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ------------------------------------------------------------ effects

        public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
        {
            if (Instance != null) Instance.PlayInternal(sfx, volume, pitch);
        }

        /// <summary>Slightly randomised pitch so repeated card sounds do not feel mechanical.</summary>
        public static void PlayVaried(Sfx sfx, float volume = 1f, float spread = 0.06f)
        {
            if (Instance != null) Instance.PlayInternal(sfx, volume, 1f + Random.Range(-spread, spread));
        }

        private void PlayInternal(Sfx sfx, float volume, float pitch)
        {
            if (!GameSettings.SoundOn || sfxClips == null || (int)sfx >= sfxClips.Length) return;
            AudioClip clip = sfxClips[(int)sfx];
            if (clip == null) return;
            AudioSource s = _sfx[_next];
            _next = (_next + 1) % SfxSources;
            s.pitch = pitch;
            s.PlayOneShot(clip, Mathf.Clamp01(volume) * GameSettings.SfxVolume);
        }

        // ------------------------------------------------------------ music

        public static void PlayMusic(MusicTrack track)
        {
            if (Instance != null) Instance.SetTrack(track);
        }

        /// <summary>Lowers the music for a moment (fanfares and jingles stay audible).</summary>
        public static void Duck(float seconds, float level = 0.35f)
        {
            if (Instance != null) Instance.StartCoroutine(Instance.DuckRoutine(seconds, level));
        }

        private System.Collections.IEnumerator DuckRoutine(float seconds, float level)
        {
            _duckTarget = level;
            yield return new WaitForSecondsRealtime(seconds);
            _duckTarget = 1f;
        }

        private void SetTrack(MusicTrack track)
        {
            if (track == _track) return;
            _track = track;
            AudioClip clip = track == MusicTrack.Menu ? menuMusic : track == MusicTrack.Table ? tableMusic : null;
            AudioSource from = _active, to = _active == _musicA ? _musicB : _musicA;
            _active = to;
            if (clip != null)
            {
                to.clip = clip;
                to.volume = 0f;
                to.Play();
            }
            _fade = 0f;
            _incoming = clip;
            _fadeFrom = from;
        }

        private AudioSource _fadeFrom;

        private void Update()
        {
            if (_musicA == null) return;
            _duck = Mathf.MoveTowards(_duck, _duckTarget, Time.unscaledDeltaTime * 2.5f);
            float target = GameSettings.MusicOn ? GameSettings.MusicVolume * 0.55f * _duck : 0f;      // headroom under the effects
            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / 1.6f);
            if (_active != null)
                _active.volume = _incoming != null ? Mathf.Lerp(_active.volume, target * Mathf.SmoothStep(0f, 1f, _fade), 0.25f) : 0f;
            if (_fadeFrom != null && _fadeFrom != _active)
            {
                _fadeFrom.volume = Mathf.Max(0f, _fadeFrom.volume - Time.unscaledDeltaTime / 1.4f * Mathf.Max(0.05f, target));
                if (_fadeFrom.volume <= 0.001f) { _fadeFrom.Stop(); _fadeFrom.clip = null; _fadeFrom = null; }
            }
        }
    }
}
