using System.Collections;
using Tarneeb.Core.Cards;
using Tarneeb.Localization;
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
        private TMP_Text _scoreUs, _scoreThem, _bidLabel, _bidValue, _tricks, _hintText, _toastText;
        private RectTransform _bidLabelRt, _bidValueRt, _medallionRt;
        private bool _trumpKnown;
        private Image _trumpIcon, _chipIcon;
        private GameObject _trumpBadge, _trumpUnknown;
        private RectTransform _scoreUsRt, _scoreThemRt, _bidCapsule, _chip, _trickRt;
        private CanvasGroup _toastGroup, _hintGroup;
        private Coroutine _toastRoutine, _hintRoutine, _usRoutine, _themRoutine;
        private int _shownUs, _shownThem;
        private bool _bound;

        private void Awake() => Bind();
        private void OnEnable() { Loc.Changed += OnLanguage; }
        private void OnDisable() { Loc.Changed -= OnLanguage; }

        private void OnLanguage() { if (isActiveAndEnabled) StartCoroutine(RelayoutNextFrame()); }

        private IEnumerator RelayoutNextFrame()
        {
            yield return null;                                                     // after the LocTexts have written their new text
            Bind();
            LayoutBid();
            LayoutTitle();
        }

        /// <summary>Keeps "TARNEEB" + crown centred as one group whatever the word's width in the current language.</summary>
        private void LayoutTitle()
        {
            Transform t = transform.Find("Header/Title"), c = transform.Find("Header/VipBadge");
            if (t == null || c == null) return;
            var title = t.GetComponent<TMP_Text>();
            title.ForceMeshUpdate();
            float tw = title.preferredWidth, crown = ((RectTransform)c).rect.width, gap = 10f;
            var trt = (RectTransform)t; var crt = (RectTransform)c;
            float left = 425f - (tw + gap + crown) / 2f;
            trt.anchoredPosition = new Vector2(left + tw / 2f, trt.anchoredPosition.y);
            crt.anchoredPosition = new Vector2(left + tw + gap + crown / 2f, crt.anchoredPosition.y);
        }

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _scoreUs = UiBind.Get<TMP_Text>(transform, "Header/ScoreUs");
            _scoreThem = UiBind.Get<TMP_Text>(transform, "Header/ScoreThem");
            _scoreUsRt = (RectTransform)_scoreUs.transform;
            _scoreThemRt = (RectTransform)_scoreThem.transform;
            _bidLabel = UiBind.Get<TMP_Text>(transform, "Header/BidLabel");
            _bidValue = UiBind.Get<TMP_Text>(transform, "Header/BidValue");
            _bidLabelRt = (RectTransform)_bidLabel.transform;
            _bidValueRt = (RectTransform)_bidValue.transform;
            _bidCapsule = UiBind.Get<RectTransform>(transform, "Header/BidCapsule");
            _trumpBadge = UiBind.Go(transform, "Header/TrumpMedallion");
            _medallionRt = (RectTransform)_trumpBadge.transform;
            _trumpIcon = UiBind.Get<Image>(transform, "Header/TrumpMedallion/Icon");
            Transform unknown = transform.Find("Header/TrumpMedallion/Unknown");
            _trumpUnknown = unknown != null ? unknown.gameObject : null;
            _chip = UiBind.Get<RectTransform>(transform, "Footer/ContractChip");
            _chipIcon = UiBind.Get<Image>(transform, "Footer/ContractChip/TricksIcon");
            _tricks = UiBind.Get<TMP_Text>(transform, "Footer/ContractChip/TrickCount");
            _trickRt = (RectTransform)_tricks.transform;
            _hintGroup = UiBind.Get<CanvasGroup>(transform, "Hint");
            _hintText = UiBind.Get<TMP_Text>(transform, "Hint/Text");
            _toastGroup = UiBind.Get<CanvasGroup>(transform, "Toast");
            _toastText = UiBind.Get<TMP_Text>(transform, "Toast/Text");
            LocText toastLoc = _toastText.GetComponent<LocText>();
            if (toastLoc == null) toastLoc = _toastText.gameObject.AddComponent<LocText>();
            toastLoc.upper = true;
            _toastGroup.alpha = 0f;
            _hintGroup.alpha = 0f;
            LayoutTitle();
            _trumpIcon.gameObject.SetActive(false);
            if (_trumpUnknown != null) _trumpUnknown.SetActive(false);             // no question mark: until the tarneeb is chosen the chip shows just "BID n", centred
            _trumpBadge.SetActive(false);
            LayoutBid();
        }

        /// <summary>
        /// Centres "BID n" in the capsule; once the tarneeb is known the suit medallion is added on the right and the pair shifts left to make room.
        /// </summary>
        private void LayoutBid()
        {
            const float gap = 16f, iconGap = 20f;
            _bidLabel.ForceMeshUpdate(); _bidValue.ForceMeshUpdate();
            float lw = _bidLabel.preferredWidth, vw = Mathf.Max(_bidValue.preferredWidth, 22f);
            float mw = _trumpKnown ? _medallionRt.rect.width : 0f;
            float total = lw + gap + vw + (_trumpKnown ? iconGap + mw : 0f);
            float cx = _bidCapsule.anchoredPosition.x, left = cx - total / 2f;
            _bidLabelRt.sizeDelta = new Vector2(lw + 4f, _bidLabelRt.sizeDelta.y);
            _bidValueRt.sizeDelta = new Vector2(vw + 4f, _bidValueRt.sizeDelta.y);
            _bidLabelRt.anchoredPosition = new Vector2(left + lw / 2f, _bidLabelRt.anchoredPosition.y);
            _bidValueRt.anchoredPosition = new Vector2(left + lw + gap + vw / 2f, _bidValueRt.anchoredPosition.y);
            if (_trumpKnown) _medallionRt.anchoredPosition = new Vector2(left + lw + gap + vw + iconGap + mw / 2f, _medallionRt.anchoredPosition.y);
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
            string s = amount.ToString();                                          // "0" until somebody bids
            if (animate && _bidValue.text != s) StartCoroutine(Tween.Punch(_bidCapsule, 0.12f, 0.3f));
            _bidValue.text = s;
            LayoutBid();
        }

        public void SetTrump(Suit? suit)
        {
            Bind();
            _trumpKnown = suit.HasValue;
            _trumpBadge.SetActive(suit.HasValue);
            _trumpIcon.gameObject.SetActive(suit.HasValue);
            LayoutBid();
            if (!suit.HasValue) return;
            _trumpIcon.sprite = UiArt.SuitIcon(suit.Value, false);
            StartCoroutine(Tween.Pop(_trumpBadge.transform, 0.45f, 0.4f));
            UiFx.Instance?.Sparkles(_trumpBadge.transform.position, 8, 34f, UiArt.GoldLight, 1.0f);
            UiFx.Instance?.Ring(_trumpBadge.transform.position, UiArt.Gold, 30f, 120f, 0.6f);
        }

        // ------------------------------------------------------------ hint + toast

        /// <summary>Small persistent line over the table ("Your turn"); empty text hides it.</summary>
        public void SetStatus(string english, params object[] args)
        {
            Bind();
            if (_hintRoutine != null) StopCoroutine(_hintRoutine);
            _hintRoutine = StartCoroutine(HintRoutine(english, args));
        }

        private IEnumerator HintRoutine(string text, object[] args)
        {
            if (string.IsNullOrEmpty(text)) { yield return Tween.Fade(_hintGroup, 0f, 0.15f); yield break; }
            if (_hintGroup.alpha > 0.05f) yield return Tween.Fade(_hintGroup, 0f, 0.08f);
            Loc.Set(_hintText, text, args);
            yield return Tween.Fade(_hintGroup, 1f, 0.18f);
        }

        public void Toast(string english, float seconds = 1.4f, params object[] args)
        {
            Bind();
            Loc.Set(_toastText, english, args);
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
