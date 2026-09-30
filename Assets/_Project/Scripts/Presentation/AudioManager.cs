using Tarneeb.Config;
using UnityEngine;

namespace Tarneeb.Presentation
{
    public enum Sfx { CardFlip, CardPlace, TrickWin, RoundWin, RoundLose, Click }

    /// <summary>Plays one-shot effects. Browsers unlock audio on the first user gesture; Unity WebGL handles the resume.</summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public AudioClip cardFlip, cardPlace, trickWin, roundWin, roundLose, click;
        private AudioSource _source;

        private void Awake()
        {
            Instance = this;
            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL decodes lazily ("sound which is not loaded yet"); load the tiny effects up front so the first play is instant.
            foreach (AudioClip c in new[] { cardFlip, cardPlace, trickWin, roundWin, roundLose, click }) if (c != null) c.LoadAudioData();
#endif
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public static void Play(Sfx sfx)
        {
            if (Instance != null) Instance.PlayInternal(sfx);
        }

        private void PlayInternal(Sfx sfx)
        {
            if (!GameSettings.SoundOn) return;
            AudioClip clip;
            switch (sfx)
            {
                case Sfx.CardFlip: clip = cardFlip; break;
                case Sfx.CardPlace: clip = cardPlace; break;
                case Sfx.TrickWin: clip = trickWin; break;
                case Sfx.RoundWin: clip = roundWin; break;
                case Sfx.RoundLose: clip = roundLose; break;
                default: clip = click; break;
            }
            if (clip != null) _source.PlayOneShot(clip);
        }
    }
}
