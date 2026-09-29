using System.Collections;
using Tarneeb.Core.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    public sealed class HudView : MonoBehaviour
    {
        private TMP_Text _scoreUs, _scoreThem, _contract, _tricks, _trumpLabel, _status, _toast;
        private Image _trumpIcon;
        private GameObject _trumpBadge;
        private CanvasGroup _toastGroup;
        private Coroutine _toastRoutine;
        private bool _bound;

        private void Awake() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _scoreUs = UiBind.Get<TMP_Text>(transform, "TopBar/ScoreUs");
            _scoreThem = UiBind.Get<TMP_Text>(transform, "TopBar/ScoreThem");
            _contract = UiBind.Get<TMP_Text>(transform, "TopBar/ContractText");
            _tricks = UiBind.Get<TMP_Text>(transform, "TopBar/TricksText");
            _trumpBadge = UiBind.Go(transform, "TrumpBadge");
            _trumpIcon = UiBind.Get<Image>(transform, "TrumpBadge/Icon");
            _trumpLabel = UiBind.Get<TMP_Text>(transform, "TrumpBadge/Label");
            _status = UiBind.Get<TMP_Text>(transform, "StatusText");
            _toastGroup = UiBind.Get<CanvasGroup>(transform, "Toast");
            _toast = UiBind.Get<TMP_Text>(transform, "Toast/ToastText");
            _toastGroup.alpha = 0f;
            _trumpBadge.SetActive(false);
        }

        public void SetScores(int us, int them)
        {
            Bind();
            _scoreUs.text = us.ToString();
            _scoreThem.text = them.ToString();
        }

        public void SetTricks(int us, int them)
        {
            Bind();
            _tricks.text = "Tricks  " + us + " - " + them;
        }

        public void SetContract(string text) { Bind(); _contract.text = text; }
        public void SetStatus(string text) { Bind(); _status.text = text; }

        public void SetTrump(Suit? suit)
        {
            Bind();
            _trumpBadge.SetActive(suit.HasValue);
            if (!suit.HasValue) return;
            _trumpIcon.sprite = CardArt.Instance.SuitSprite(suit.Value);
            _trumpIcon.color = CardArt.SuitColor(suit.Value);
            _trumpLabel.text = "Trump";
        }

        public void Toast(string text, float seconds = 1.4f)
        {
            Bind();
            _toast.text = text;
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine(seconds));
        }

        private IEnumerator ToastRoutine(float seconds)
        {
            yield return Tween.Fade(_toastGroup, 1f, 0.15f);
            yield return new WaitForSeconds(seconds);
            yield return Tween.Fade(_toastGroup, 0f, 0.3f);
        }
    }
}
