using System.Collections;
using Tarneeb.Core.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>
    /// Header (scoreboard, bid and trump), footer contract chip, a hint pill over the table and a toast. Children are bound by name
    /// (Header/ScoreUs ...); GameSceneAssembler builds them from the Figma header and footer.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private TMP_Text _scoreUs, _scoreThem, _bidValue, _tricks, _hintText, _toastText;
        private Image _trumpIcon, _chipIcon;
        private GameObject _trumpBadge, _trumpUnknown;
        private RectTransform _scoreUsRt, _scoreThemRt, _bidCapsule, _chip, _trickRt;
        private CanvasGroup _toastGroup, _hintGroup;
        private Coroutine _toastRoutine, _hintRoutine, _usRoutine, _themRoutine;
        private int _shownUs, _shownThem;
        private bool _bound;

        private void Awake() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _scoreUs = UiBind.Get<TMP_Text>(transform, "Header/ScoreUs");
            _scoreThem = UiBind.Get<TMP_Text>(transform, "Header/ScoreThem");
            _scoreUsRt = (RectTransform)_scoreUs.transform;
            _scoreThemRt = (RectTransform)_scoreThem.transform;
            _bidValue = UiBind.Get<TMP_Text>(transform, "Header/BidValue");
            _bidCapsule = UiBind.Get<RectTransform>(transform, "Header/BidCapsule");
            _trumpBadge = UiBind.Go(transform, "Header/TrumpMedallion");
            _trumpIcon = UiBind.Get<Image>(transform, "Header/TrumpMedallion/Icon");
            Transform unknown = transform.Find("Header/TrumpMedallion/Unknown");
            _trumpUnknown = unknown != null ? unknown.gameObject : null;
            _chip = UiBind.Get<RectTransform>(transform, "Footer/ContractChip");
            _chipIcon = UiBind.Get<Image>(transform, "Footer/ContractChip/SuitIcon");
            _tricks = UiBind.Get<TMP_Text>(transform, "Footer/ContractChip/TrickCount");
            _trickRt = (RectTransform)_tricks.transform;
            _hintGroup = UiBind.Get<CanvasGroup>(transform, "Hint");
            _hintText = UiBind.Get<TMP_Text>(transform, "Hint/Text");
            _toastGroup = UiBind.Get<CanvasGroup>(transform, "Toast");
            _toastText = UiBind.Get<TMP_Text>(transform, "Toast/Text");
            _toastGroup.alpha = 0f;
            _hintGroup.alpha = 0f;
            _chipIcon.gameObject.SetActive(false);
            _trumpIcon.gameObject.SetActive(false);
            if (_trumpUnknown != null) _trumpUnknown.SetActive(true);
        }

        // ------------------------------------------------------------ scoreboard

        public void SetScores(int us, int them, bool animate = false)
        {
            Bind();
            if (!animate) { _shownUs = us; _shownThem = them; _scoreUs.text = us.ToString(); _scoreThem.text = them.ToString(); return; }
            if (us != _shownUs) { if (_usRoutine != null) StopCoroutine(_usRoutine); _usRoutine = StartCoroutine(CountTo(_scoreUs, _scoreUsRt, _shownUs, us, true)); }
            if (them != _shownThem) { if (_themRoutine != null) StopCoroutine(_themRoutine); _themRoutine = StartCoroutine(CountTo(_scoreThem, _scoreThemRt, _shownThem, them, false)); }
            _shownUs = us; _shownThem = them;
        }

        private IEnumerator CountTo(TMP_Text label, RectTransform rt, int from, int to, bool ours)
        {
            AudioManager.Play(Sfx.Coin, 0.7f);
            UiFx.Instance?.Sparkles(rt.position, 6, 40f, ours ? UiArt.Teal : UiArt.Gold);
            StartCoroutine(Tween.Punch(rt, 0.35f, 0.5f));
            float dur = Mathf.Min(1.1f, 0.25f + Mathf.Abs(to - from) * 0.07f);
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                label.text = Mathf.RoundToInt(Mathf.Lerp(from, to, Tween.Evaluate(Ease.OutCubic, t / dur))).ToString();
                yield return null;
            }
            label.text = to.ToString();
        }

        public void SetTricks(int ours, int theirs, bool animate = false)
        {
            Bind();
            string s = ours.ToString();
            if (animate && _tricks.text != s) StartCoroutine(Tween.Punch(_trickRt, 0.3f, 0.35f));
            _tricks.text = s;
        }

        public void SetBid(int amount, bool animate = true)
        {
            Bind();
            string s = amount > 0 ? amount.ToString() : "-";
            if (animate && _bidValue.text != s) StartCoroutine(Tween.Punch(_bidCapsule, 0.12f, 0.3f));
            _bidValue.text = s;
        }

        public void SetTrump(Suit? suit)
        {
            Bind();
            _trumpIcon.gameObject.SetActive(suit.HasValue);
            _chipIcon.gameObject.SetActive(suit.HasValue);
            if (_trumpUnknown != null) _trumpUnknown.SetActive(!suit.HasValue);
            if (!suit.HasValue) return;
            _trumpIcon.sprite = UiArt.SuitIcon(suit.Value, false);
            _chipIcon.sprite = UiArt.SuitIcon(suit.Value, true);
            _chipIcon.color = UiArt.Cream;
            StartCoroutine(Tween.Pop(_trumpBadge.transform, 0.45f, 0.4f));
            StartCoroutine(Tween.Pop(_chipIcon.transform, 0.45f, 0.4f));
            UiFx.Instance?.Sparkles(_trumpBadge.transform.position, 8, 34f, UiArt.GoldLight, 1.0f);
            UiFx.Instance?.Ring(_trumpBadge.transform.position, UiArt.Gold, 30f, 120f, 0.6f);
        }

        // ------------------------------------------------------------ hint + toast

        /// <summary>Small persistent line over the table ("Your turn"); empty text hides it.</summary>
        public void SetStatus(string text)
        {
            Bind();
            if (_hintRoutine != null) StopCoroutine(_hintRoutine);
            _hintRoutine = StartCoroutine(HintRoutine(text));
        }

        private IEnumerator HintRoutine(string text)
        {
            if (string.IsNullOrEmpty(text)) { yield return Tween.Fade(_hintGroup, 0f, 0.15f); yield break; }
            if (_hintGroup.alpha > 0.05f && _hintText.text != text) yield return Tween.Fade(_hintGroup, 0f, 0.08f);
            _hintText.text = text;
            yield return Tween.Fade(_hintGroup, 1f, 0.18f);
        }

        public void Toast(string text, float seconds = 1.4f)
        {
            Bind();
            _toastText.text = text;
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine(seconds));
        }

        private IEnumerator ToastRoutine(float seconds)
        {
            _toastGroup.alpha = 1f;
            StartCoroutine(Tween.Pop(_toastGroup.transform, 0.3f, 0.6f));
            yield return new WaitForSeconds(seconds);
            yield return Tween.Fade(_toastGroup, 0f, 0.3f);
        }
    }
}
