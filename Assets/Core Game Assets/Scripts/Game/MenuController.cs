using System.Collections;
using Tarneeb.Config;
using Tarneeb.Localization;
using Tarneeb.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Game
{
    /// <summary>
    /// Main menu (Figma styling): Play vs Bots, Play Online, Settings. Children: PlayButton, OnlineButton, SettingsButton, Title, Crown,
    /// Tagline, Cards (decorative fan), Glow. Runs the intro animation and the ambient sparkles.
    /// </summary>
    public sealed class MenuController : MonoBehaviour
    {
        public const string GameSceneName = "Game";

        private RectTransform _title, _crown, _tagline, _cards, _glow;
        private Vector2 _cardsHome;
        private RectTransform[] _buttons;
        private CanvasGroup[] _buttonGroups;
        private float _clock;

        private void Awake()
        {
            Tween.SpeedMultiplier = 1f;
            AudioManager.PlayMusic(MusicTrack.Menu);

            UiBind.Get<Button>(transform, "PlayButton").onClick.AddListener(() => { GameController.OnlineRequested = false; ScreenFader.Go(GameSceneName); });

            Button online = UiBind.Get<Button>(transform, "OnlineButton");
            TMP_Text onlineLabel = UiBind.Get<TMP_Text>(transform, "OnlineButton/Label");
            online.interactable = GameSettings.OnlineEnabled;
            Loc.Set(onlineLabel, GameSettings.OnlineEnabled ? "PLAY ONLINE" : "ONLINE UNAVAILABLE");
            LobbyScreen lobby = transform.parent != null ? transform.parent.Find("OnlinePanel")?.GetComponent<LobbyScreen>() : null;
            if (lobby != null) online.onClick.AddListener(lobby.Open);

            Button settings = UiBind.Get<Button>(transform, "SettingsButton");
            settings.onClick.AddListener(() => SettingsPanel.Instance?.Open());

            SetupQuickToggle("SoundButton", "icon_sound_on", "icon_sound_off", () => GameSettings.SoundOn, v => GameSettings.SoundOn = v);
            SetupQuickToggle("MusicButton", "icon_music_on", "icon_music_off", () => GameSettings.MusicOn, v => GameSettings.MusicOn = v);

            _title = transform.Find("Title") as RectTransform;
            _crown = transform.Find("Crown") as RectTransform;
            _tagline = transform.Find("Tagline") as RectTransform;
            _cards = transform.Find("Cards") as RectTransform;
            if (_cards != null) _cardsHome = _cards.anchoredPosition;
            _glow = transform.Find("Glow") as RectTransform;
            _buttons = new[] { (RectTransform)transform.Find("PlayButton"), (RectTransform)transform.Find("OnlineButton"), (RectTransform)transform.Find("SettingsButton") };
            _buttonGroups = new CanvasGroup[_buttons.Length];
            for (int i = 0; i < _buttons.Length; i++)
            {
                _buttonGroups[i] = _buttons[i].GetComponent<CanvasGroup>();
                if (_buttonGroups[i] == null) _buttonGroups[i] = _buttons[i].gameObject.AddComponent<CanvasGroup>();
                _buttonGroups[i].alpha = 0f;
            }
            if (_title != null) _title.localScale = Vector3.zero;
            if (_crown != null) _crown.localScale = Vector3.zero;
            if (_tagline != null) _tagline.GetComponent<CanvasGroup>().alpha = 0f;
            if (_cards != null) _cards.GetComponent<CanvasGroup>().alpha = 0f;
        }

        private void SetupQuickToggle(string node, string onIcon, string offIcon, System.Func<bool> get, System.Action<bool> set)
        {
            Transform t = transform.Find(node);
            if (t == null) return;
            Image icon = t.Find("Icon").GetComponent<Image>();
            void Refresh() => icon.sprite = UiArt.Get(get() ? onIcon : offIcon);
            Refresh();
            var fx = t.GetComponent<ButtonFx>();
            if (fx != null) fx.silent = true;
            t.GetComponent<Button>().onClick.AddListener(() =>
            {
                set(!get());
                Refresh();
                AudioManager.Play(get() ? Sfx.ToggleOn : Sfx.ToggleOff, 0.8f);
            });
        }

        private void OnEnable() => ResponsiveFrame.Applied += OnLayout;
        private void OnDisable() => ResponsiveFrame.Applied -= OnLayout;

        /// <summary>The responsive layout moved the menu's elements: the card fan floats around its new home.</summary>
        private void OnLayout()
        {
            if (_cards != null) _cardsHome = _cards.anchoredPosition;
        }

        private IEnumerator Start()
        {
            LanguagePrompt prompt = transform.parent != null ? transform.parent.Find("LanguagePanel")?.GetComponent<LanguagePrompt>() : null;
            if (prompt != null) StartCoroutine(OpenPromptLater(prompt));
            AudioManager.PlayMusic(MusicTrack.Menu);                // again, in case the menu's own AudioManager woke up after Awake

            yield return new WaitForSeconds(0.25f);
            if (_title != null) { AudioManager.Play(Sfx.Sparkle, 0.7f); StartCoroutine(Tween.ScaleTo(_title, 0.6f, 1f, 0.7f, Ease.OutBack)); }
            if (_title != null) StartCoroutine(FadeIn(_title.GetComponent<CanvasGroup>(), 0.4f));
            yield return new WaitForSeconds(0.3f);
            if (_crown != null)
            {
                StartCoroutine(Tween.ScaleTo(_crown, 0f, 1f, 0.7f, Ease.OutElastic));
                UiFx.Instance?.Sparkles(_crown.position, 10, 60f, UiArt.GoldLight, 1.2f, 40f);
            }
            yield return new WaitForSeconds(0.2f);
            if (_tagline != null) StartCoroutine(FadeIn(_tagline.GetComponent<CanvasGroup>(), 0.6f));
            if (_cards != null) StartCoroutine(FadeIn(_cards.GetComponent<CanvasGroup>(), 0.9f));
            for (int i = 0; i < _buttons.Length; i++)
            {
                StartCoroutine(SlideIn(_buttons[i], _buttonGroups[i]));
                AudioManager.Play(Sfx.CardDeal, 0.5f);
                yield return new WaitForSeconds(0.14f);
            }
        }

        private static IEnumerator OpenPromptLater(LanguagePrompt prompt)
        {
            yield return new WaitForSeconds(0.5f);
            prompt.OpenIfFirstLaunch();
        }

        private static IEnumerator FadeIn(CanvasGroup g, float seconds)
        {
            if (g == null) yield break;
            for (float t = 0f; t < seconds; t += Time.deltaTime) { g.alpha = t / seconds; yield return null; }
            g.alpha = 1f;
        }

        private static IEnumerator SlideIn(RectTransform rt, CanvasGroup g)
        {
            Vector2 end = rt.anchoredPosition, start = end + new Vector2(0f, -70f);
            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                float k = Tween.Evaluate(Ease.OutBack, t / 0.45f);
                rt.anchoredPosition = Vector2.LerpUnclamped(start, end, k);
                g.alpha = Mathf.Clamp01(t / 0.25f);
                yield return null;
            }
            rt.anchoredPosition = end;
            g.alpha = 1f;
        }

        private void Update()
        {
            _clock += Time.deltaTime;
                        if (_cards != null) _cards.anchoredPosition = _cardsHome + new Vector2(0f, Mathf.Sin(_clock * 1.3f) * 7f);      // fixed range: the fan never drifts onto the tagline
            if (_buttons != null && _buttons.Length > 0 && _buttons[0] != null && _clock > 1.6f)
            {
                // gentle idle pulse on the primary button's label (ButtonFx owns hover/press on the button itself)
                Transform label = _buttons[0].Find("Label");
                if (label != null) label.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(_clock * 2.4f));
            }
        }
    }
}
