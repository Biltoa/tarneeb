using Tarneeb.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>
    /// The one-time language choice shown on the very first launch (no flags: the two languages are named in their own words). Children:
    /// Panel/English and Panel/Arabic. Until a choice is made the game runs in English.
    /// </summary>
    public sealed class LanguagePrompt : MonoBehaviour
    {
        private CanvasGroup _group;
        private bool _bound;

        private void Awake() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _group = GetComponent<CanvasGroup>();
            UiBind.Get<Button>(transform, "Panel/English").onClick.AddListener(() => Choose(Language.English));
            UiBind.Get<Button>(transform, "Panel/Arabic").onClick.AddListener(() => Choose(Language.Arabic));
            gameObject.SetActive(false);
        }

        /// <summary>Opens the prompt if the player has never chosen a language.</summary>
        public void OpenIfFirstLaunch()
        {
            Bind();
            if (Loc.HasChosen) return;
            gameObject.SetActive(true);
            StartCoroutine(Tween.Fade(_group, 1f, 0.3f));
        }

        private void Choose(Language l)
        {
            Loc.SetLanguage(l);                                             // stores the choice, so the prompt never shows again
            AudioManager.Play(Sfx.Click, 0.8f);
            StartCoroutine(Close());
        }

        private System.Collections.IEnumerator Close()
        {
            yield return Tween.Fade(_group, 0f, 0.2f);
            gameObject.SetActive(false);
        }
    }
}
