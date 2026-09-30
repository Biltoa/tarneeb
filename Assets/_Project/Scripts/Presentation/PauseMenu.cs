using Tarneeb.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>The hamburger menu at the table: Resume, Settings, Leave table. Children: Dim, Panel/{Resume, Settings, Leave}.</summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        private CanvasGroup _group;
        private Transform _panel;
        private bool _bound;

        private void Awake() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _group = GetComponent<CanvasGroup>();
            _panel = UiBind.Get<Transform>(transform, "Panel");
            UiBind.Get<Button>(transform, "Dim").onClick.AddListener(Close);
            UiBind.Get<Button>(transform, "Panel/Resume").onClick.AddListener(Close);
            UiBind.Get<Button>(transform, "Panel/Settings").onClick.AddListener(() => { Close(); SettingsPanel.Instance?.Open(); });
            UiBind.Get<Button>(transform, "Panel/Leave").onClick.AddListener(() => ScreenFader.Go(GameController.MenuSceneName));
            gameObject.SetActive(false);
        }

        public void Open()
        {
            Bind();
            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(Intro());
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            StopAllCoroutines();
            StartCoroutine(Outro());
        }

        private System.Collections.IEnumerator Intro()
        {
            AudioManager.Play(Sfx.Whoosh, 0.5f);
            _group.alpha = 0f;
            StartCoroutine(Tween.ScaleTo(_panel, 0.85f, 1f, 0.28f, Ease.OutBack));
            yield return Tween.Fade(_group, 1f, 0.15f);
        }

        private System.Collections.IEnumerator Outro()
        {
            yield return Tween.Fade(_group, 0f, 0.12f);
            gameObject.SetActive(false);
        }
    }
}
