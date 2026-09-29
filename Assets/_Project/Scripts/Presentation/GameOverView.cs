using System.Collections;
using System.Text;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>Game over popup. Children: Panel/Title, Panel/Body, Panel/PlayAgain, Panel/MenuBtn.</summary>
    public sealed class GameOverView : MonoBehaviour
    {
        public enum Choice { None, PlayAgain, Menu }

        private CanvasGroup _group;
        private TMP_Text _title, _body;
        private Choice _choice;
        private bool _bound;

        private void Awake() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _group = GetComponent<CanvasGroup>();
            _title = UiBind.Get<TMP_Text>(transform, "Panel/Title");
            _body = UiBind.Get<TMP_Text>(transform, "Panel/Body");
            UiBind.Get<Button>(transform, "Panel/PlayAgain").onClick.AddListener(() => _choice = Choice.PlayAgain);
            UiBind.Get<Button>(transform, "Panel/MenuBtn").onClick.AddListener(() => _choice = Choice.Menu);
            _group.alpha = 0f;
            gameObject.SetActive(false);
        }

        public IEnumerator Show(bool won, int us, int them, System.Action<Choice> result, float autoDismissSeconds = 0f)
        {
            Bind();
            _title.text = won ? "You win!" : "You lose";
            _title.color = won ? new Color(0.98f, 0.84f, 0.35f) : new Color(1f, 0.55f, 0.5f);
            _body.text = "Final score\n<size=72><b>" + us + "  -  " + them + "</b></size>";
            _choice = Choice.None;
            gameObject.SetActive(true);
            yield return Tween.Fade(_group, 1f, 0.3f);
            for (float t = 0f; _choice == Choice.None && (autoDismissSeconds <= 0f || t < autoDismissSeconds); t += Time.deltaTime) yield return null;
            if (_choice == Choice.None) _choice = Choice.PlayAgain;
            result(_choice);
        }

        public void Hide()
        {
            Bind();
            _group.alpha = 0f;
            gameObject.SetActive(false);
        }
    }
}
