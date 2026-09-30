using Tarneeb.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>
    /// Small settings window used by the menu and the table: sound effects and music (switch + volume), game speed, bot skill and a
    /// credits page (the LGPL card artwork needs attribution). Everything is written to GameSettings immediately.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        public static SettingsPanel Instance { get; private set; }

        private CanvasGroup _group;
        private Transform _panel;
        private GameObject _creditsView, _mainView;
        private ToggleSwitch _sfxToggle, _musicToggle;
        private Slider _sfxSlider, _musicSlider;
        private Button[] _speed = new Button[2], _bots = new Button[2];
        private TMP_Text _title;
        private bool _bound;
        private Coroutine _routine;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake() { Instance = this; Bind(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _group = GetComponent<CanvasGroup>();
            _panel = UiBind.Get<Transform>(transform, "Panel");
            _title = UiBind.Get<TMP_Text>(transform, "Panel/Title");
            _mainView = UiBind.Go(transform, "Panel/Main");
            _creditsView = UiBind.Go(transform, "Panel/Credits");
            UiBind.Get<Button>(transform, "Dim").onClick.AddListener(Close);
            UiBind.Get<Button>(transform, "Panel/Close").onClick.AddListener(Close);

            _sfxToggle = UiBind.Get<ToggleSwitch>(transform, "Panel/Main/Sfx/Toggle");
            _sfxSlider = UiBind.Get<Slider>(transform, "Panel/Main/Sfx/Slider");
            _musicToggle = UiBind.Get<ToggleSwitch>(transform, "Panel/Main/Music/Toggle");
            _musicSlider = UiBind.Get<Slider>(transform, "Panel/Main/Music/Slider");
            _sfxToggle.Changed += on => { GameSettings.SoundOn = on; };
            _musicToggle.Changed += on => { GameSettings.MusicOn = on; };
            _sfxSlider.onValueChanged.AddListener(v => GameSettings.SfxVolume = v);
            _sfxSlider.gameObject.AddComponent<SliderTick>().Init(() => AudioManager.Play(Sfx.Click, 0.6f));
            _musicSlider.onValueChanged.AddListener(v => GameSettings.MusicVolume = v);

            for (int i = 0; i < 2; i++)
            {
                int idx = i;
                _speed[i] = UiBind.Get<Button>(transform, "Panel/Main/Speed/Opt" + i);
                _speed[i].onClick.AddListener(() => { GameSettings.GameSpeed = idx == 1 ? 2f : 1f; Tween.SpeedMultiplier = 1f; Refresh(); });
                _bots[i] = UiBind.Get<Button>(transform, "Panel/Main/Bots/Opt" + i);
                _bots[i].onClick.AddListener(() => { GameSettings.Difficulty = idx == 0 ? BotDifficulty.Easy : BotDifficulty.Normal; Refresh(); });
            }
            UiBind.Get<Button>(transform, "Panel/Main/CreditsButton").onClick.AddListener(() => ShowCredits(true));
            UiBind.Get<Button>(transform, "Panel/Credits/Back").onClick.AddListener(() => ShowCredits(false));
            gameObject.SetActive(false);
        }

        public void Open()
        {
            Bind();
            gameObject.SetActive(true);
            ShowCredits(false);
            Refresh();
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Intro());
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Outro());
        }

        private System.Collections.IEnumerator Intro()
        {
            AudioManager.Play(Sfx.Whoosh, 0.5f);
            _group.alpha = 0f;
            StartCoroutine(Tween.ScaleTo(_panel, 0.85f, 1f, 0.3f, Ease.OutBack));
            yield return Tween.Fade(_group, 1f, 0.18f);
        }

        private System.Collections.IEnumerator Outro()
        {
            StartCoroutine(Tween.ScaleTo(_panel, 1f, 0.9f, 0.15f, Ease.OutQuad));
            yield return Tween.Fade(_group, 0f, 0.15f);
            gameObject.SetActive(false);
        }

        private void ShowCredits(bool on)
        {
            if (_title != null) _title.text = on ? "CREDITS" : "SETTINGS";
            _mainView.SetActive(!on);
            _creditsView.SetActive(on);
        }

        private void Refresh()
        {
            _sfxToggle.Set(GameSettings.SoundOn);
            _musicToggle.Set(GameSettings.MusicOn);
            _sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            _musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            Mark(_speed, GameSettings.GameSpeed > 1.5f ? 1 : 0);
            Mark(_bots, GameSettings.Difficulty == BotDifficulty.Easy ? 0 : 1);
        }

        private static void Mark(Button[] opts, int selected)
        {
            for (int i = 0; i < opts.Length; i++)
            {
                bool on = i == selected;
                opts[i].GetComponent<Image>().sprite = UiArt.Get(on ? "button_gold_9s48" : "button_emerald_9s48");
                TMP_Text label = opts[i].GetComponentInChildren<TMP_Text>();
                if (label != null) label.color = on ? UiArt.Midnight : UiArt.Cream;
            }
        }
    }
}
