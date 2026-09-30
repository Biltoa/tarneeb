using System.Collections;
using System.Collections.Generic;
using Tarneeb.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>
    /// One player's spot, built from the Figma "Player" panel: portrait ring, nameplate, tricks-won capsule, animated turn glow
    /// with sparkle rays, an optional countdown timer, a fan of card backs (one per card in hand), bid bubble, dealer chip and chat.
    /// Children are found by name (see GameSceneAssembler); optional ones may be missing.
    /// </summary>
    public sealed class SeatView : MonoBehaviour
    {
        private const float FanRadius = 47f;
        private const float MaxHalfAngle = 50f;

        private TMP_Text _name, _tricks, _bubbleText, _chatText, _timerText;
        private Image _teamMark, _timerArc, _emojiImage;
        private GameObject _turn, _bubble, _dealer, _chatBubble, _emojiBadge, _timer;
        private RectTransform _fan, _tricksCapsule, _ringRoot, _rays, _rim, _glow;
        private CanvasGroup _turnGroup;
        private Coroutine _bubbleRoutine, _chatRoutine, _turnRoutine;
        private readonly List<RectTransform> _fanCards = new List<RectTransform>();
        private float _fanScale = 1f;
        private int _handCount;
        private float _timerTotal, _timerLeft;
        private bool _turnOn, _bound;

        public int HandCount => _handCount;
        /// <summary>Where cards leave from / arrive at (the middle of the fan).</summary>
        public RectTransform HandAnchor { get { Bind(); return _fan; } }
        public RectTransform Ring { get { Bind(); return _ringRoot; } }

        private void Awake() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _name = UiBind.Get<TMP_Text>(transform, "NameLabel");
            _tricks = UiBind.Get<TMP_Text>(transform, "TricksText");
            _tricksCapsule = transform.Find("TricksCapsule") as RectTransform;
            _ringRoot = UiBind.Get<RectTransform>(transform, "Ring");
            _fan = UiBind.Get<RectTransform>(transform, "Fan");
            _teamMark = transform.Find("TeamMark") != null ? transform.Find("TeamMark").GetComponent<Image>() : null;
            _turn = UiBind.Go(transform, "Turn");
            _turnGroup = _turn.GetComponent<CanvasGroup>();
            _glow = _turn.transform.Find("Glow") as RectTransform;
            _rim = _turn.transform.Find("Rim") as RectTransform;
            _rays = _turn.transform.Find("Rays") as RectTransform;
            Transform b = transform.Find("Bubble");                      // the local seat has none (its actions show as toasts)
            if (b != null) { _bubble = b.gameObject; _bubbleText = b.Find("BubbleText").GetComponent<TMP_Text>(); _bubble.SetActive(false); }
            Transform d = transform.Find("DealerChip"); if (d != null) { _dealer = d.gameObject; _dealer.SetActive(false); }
            Transform tm = transform.Find("Timer");
            if (tm != null)
            {
                _timer = tm.gameObject;
                _timerArc = tm.Find("Arc").GetComponent<Image>();
                _timerText = tm.Find("Seconds").GetComponent<TMP_Text>();
                _timer.SetActive(false);
            }
            Transform chat = transform.Find("ChatBubble");
            if (chat != null) { _chatBubble = chat.gameObject; _chatText = chat.Find("Text").GetComponent<TMP_Text>(); _chatBubble.SetActive(false); }
            Transform emoji = transform.Find("EmojiBadge");
            if (emoji != null) { _emojiBadge = emoji.gameObject; _emojiImage = emoji.Find("Icon").GetComponent<Image>(); _emojiBadge.SetActive(false); }
            _turn.SetActive(false);
            _fanScale = _fan.localScale.x > 0.01f ? _fan.localScale.x : 1f;
        }

        public void Setup(string playerName, Color teamColor)
        {
            Bind();
            _name.text = playerName.ToUpperInvariant();
            if (_teamMark != null) _teamMark.color = teamColor;
        }

        // ------------------------------------------------------------ turn, dealer, tricks

        /// <param name="timerSeconds">Human turn limit; 0 hides the countdown.</param>
        public void SetTurn(bool on, float timerSeconds = 0f)
        {
            Bind();
            if (_turnOn == on && !on) return;
            _turnOn = on;
            if (_turnRoutine != null) { StopCoroutine(_turnRoutine); _turnRoutine = null; }
            if (!on)
            {
                _turnRoutine = StartCoroutine(FadeTurnOut());
                if (_timer != null) _timer.SetActive(false);
                _timerLeft = 0f;
                return;
            }
            _turn.SetActive(true);
            _turnGroup.alpha = 0f;
            _turnRoutine = StartCoroutine(TurnLoop());
            if (_timer != null && timerSeconds > 0f)
            {
                _timerTotal = _timerLeft = timerSeconds;
                _timer.SetActive(true);
                RefreshTimer();
            }
            else if (_timer != null) _timer.SetActive(false);
        }

        private IEnumerator TurnLoop()
        {
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                _turnGroup.alpha = t / 0.25f;
                yield return null;
            }
            _turnGroup.alpha = 1f;
            float clock = 0f;
            while (true)
            {
                clock += Time.deltaTime;
                float breathe = 0.5f + 0.5f * Mathf.Sin(clock * 3.2f);
                if (_glow != null) _glow.localScale = Vector3.one * (1f + 0.07f * breathe);
                if (_rim != null) _rim.localScale = Vector3.one * (1f + 0.025f * breathe);
                if (_rays != null) _rays.localRotation = Quaternion.Euler(0f, 0f, clock * 14f);
                yield return null;
            }
        }

        private IEnumerator FadeTurnOut()
        {
            if (!_turn.activeSelf) yield break;
            float a0 = _turnGroup.alpha;
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                _turnGroup.alpha = Mathf.Lerp(a0, 0f, t / 0.2f);
                yield return null;
            }
            _turn.SetActive(false);
        }

        private void Update()
        {
            if (_timer == null || !_timer.activeSelf || _timerLeft <= 0f) return;
            int before = Mathf.CeilToInt(_timerLeft);
            _timerLeft = Mathf.Max(0f, _timerLeft - Time.deltaTime);
            int after = Mathf.CeilToInt(_timerLeft);
            if (after != before && after <= 5 && after > 0) AudioManager.Play(Sfx.TimerTick, 0.7f);
            RefreshTimer();
        }

        private void RefreshTimer()
        {
            _timerArc.fillAmount = _timerTotal > 0f ? _timerLeft / _timerTotal : 0f;
            _timerText.text = Mathf.CeilToInt(_timerLeft).ToString();
            bool urgent = _timerLeft <= 5f;
            _timerArc.color = urgent ? new Color(1f, 0.45f, 0.4f) : UiArt.Gold;
        }

        public void SetDealer(bool on) { Bind(); if (_dealer != null) _dealer.SetActive(on); }

        public void SetTricks(int n, bool animate = false)
        {
            Bind();
            _tricks.text = n.ToString();
            if (animate && _tricksCapsule != null) StartCoroutine(Tween.Punch(_tricksCapsule, 0.22f, 0.32f));
        }

        // ------------------------------------------------------------ fan of card backs

        /// <summary>Shows exactly <paramref name="count"/> backs, fanned like the Figma design. Extra backs disappear from the right.</summary>
        public void SetHandCount(int count)
        {
            Bind();
            _handCount = Mathf.Max(0, count);
            while (_fanCards.Count < _handCount) _fanCards.Add(NewBack());
            for (int i = 0; i < _fanCards.Count; i++) _fanCards[i].gameObject.SetActive(i < _handCount);
            LayoutFan();
        }

        private RectTransform NewBack()
        {
            var go = new GameObject("Back", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_fan, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(62f, 94f);
            var im = go.GetComponent<Image>();
            im.sprite = CardArt.Back;
            im.raycastTarget = false;
            return rt;
        }

        private void LayoutFan()
        {
            int n = _handCount;
            if (n == 0) return;
            float half = n == 1 ? 0f : Mathf.Min(MaxHalfAngle, 6.25f * (n - 1));
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0f : (i / (float)(n - 1)) * 2f - 1f;            // -1 .. 1
                float ang = t * half;
                float rad = ang * Mathf.Deg2Rad;
                RectTransform rt = _fanCards[i];
                rt.anchoredPosition = new Vector2(Mathf.Sin(rad) * FanRadius, -FanRadius * (1f - Mathf.Cos(rad)));
                rt.localRotation = Quaternion.Euler(0f, 0f, -ang);
                rt.SetSiblingIndex(i);
            }
        }

        // ------------------------------------------------------------ bubbles

        public void ShowBubble(string text, float seconds = 0f)
        {
            Bind();
            if (_bubble == null) return;
            _bubbleText.text = text;
            _bubble.SetActive(true);
            if (_bubbleRoutine != null) StopCoroutine(_bubbleRoutine);
            _bubbleRoutine = StartCoroutine(BubbleRoutine(seconds));
        }

        private IEnumerator BubbleRoutine(float seconds)
        {
            yield return Tween.Pop(_bubble.transform, 0.25f, 0.6f);
            if (seconds <= 0f) yield break;
            yield return new WaitForSeconds(seconds);
            _bubble.SetActive(false);
        }

        public void HideBubble()
        {
            Bind();
            if (_bubbleRoutine != null) StopCoroutine(_bubbleRoutine);
            if (_bubble != null) _bubble.SetActive(false);
        }

        /// <summary>Shows a chat line or emoji beside this seat for a few seconds; a new one replaces the old.</summary>
        public void ShowChat(ChatMessage m)
        {
            Bind();
            if (_chatBubble == null || _emojiBadge == null) return;
            if (_chatRoutine != null) StopCoroutine(_chatRoutine);
            _chatRoutine = StartCoroutine(ChatRoutine(m));
        }

        private IEnumerator ChatRoutine(ChatMessage m)
        {
            _chatBubble.SetActive(false);
            _emojiBadge.SetActive(false);
            AudioManager.Play(Sfx.Pop, 0.6f);
            if (m.IsEmoji)
            {
                _emojiImage.sprite = EmojiArt.Get(m.EmojiId);
                _emojiBadge.SetActive(true);
                yield return Tween.ScaleTo(_emojiBadge.transform, 0f, 1f, 0.35f, Ease.OutElastic);
                UiFx.Instance?.Sparkles(_emojiBadge.transform.position, 5, 46f, UiArt.GoldLight, 0.7f, 26f);
                float t = 0f;
                while (t < 2.4f) { t += Time.deltaTime; _emojiBadge.transform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(t * 6f)); yield return null; }
                yield return Tween.ScaleTo(_emojiBadge.transform, 1f, 0f, 0.18f, Ease.InBack);
                _emojiBadge.SetActive(false);
            }
            else
            {
                _chatText.text = m.Text;
                _chatBubble.SetActive(true);
                yield return Tween.Pop(_chatBubble.transform, 0.28f, 0.7f);
                yield return new WaitForSeconds(2.0f + m.Text.Length * 0.05f);
                yield return Tween.ScaleTo(_chatBubble.transform, 1f, 0f, 0.15f, Ease.InBack);
                _chatBubble.SetActive(false);
            }
        }
    }
}
