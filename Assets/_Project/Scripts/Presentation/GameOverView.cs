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
            _title.text = won ? "YOU WIN!" : "YOU LOSE";
            _title.color = won ? UiArt.Gold : UiArt.Bad;
            _body.text = "Final score\n<size=84><b>" + us + "  :  " + them + "</b></size>";
            Transform crown = transform.Find("Panel/Crown");
            if (crown != null) crown.gameObject.SetActive(won);
            _choice = Choice.None;
            gameObject.SetActive(true);
            Transform panel = transform.Find("Panel");
            if (panel != null) StartCoroutine(Tween.ScaleTo(panel, 0.75f, 1f, 0.45f, Ease.OutBack));
            if (crown != null && won) StartCoroutine(CrownBounce(crown));
            yield return Tween.Fade(_group, 1f, 0.3f);
            for (float t = 0f; _choice == Choice.None && (autoDismissSeconds <= 0f || t < autoDismissSeconds); t += Time.deltaTime) yield return null;
            if (_choice == Choice.None) _choice = Choice.PlayAgain;
            result(_choice);
        }

        private IEnumerator CrownBounce(Transform crown)
        {
            yield return Tween.Wait(0.3f);
            yield return Tween.ScaleTo(crown, 0f, 1f, 0.6f, Ease.OutElastic);
            UiFx.Instance?.Sparkles(crown.position, 10, 90f, UiArt.GoldLight, 1.2f, 44f);
            for (float t = 0f; crown != null && crown.gameObject.activeInHierarchy; t += Time.deltaTime)
            {
                crown.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.2f) * 4f);
                yield return null;
            }
        }

        public void Hide()
        {
            Bind();
            _group.alpha = 0f;
            gameObject.SetActive(false);
        }
    }
}
