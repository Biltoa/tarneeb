using System.Collections;
using Tarneeb.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>One player's spot: name, team color, turn ring, bid bubble, dealer chip, card count.</summary>
    public sealed class SeatView : MonoBehaviour
    {
        private TMP_Text _name, _bubbleText, _count;
        private Image _stripe;
        private GameObject _turn, _bubble, _dealer, _countBadge;
        private RectTransform _handAnchor;
        private Coroutine _bubbleRoutine, _chatRoutine;
        private GameObject _chatBubble, _emojiBadge;
        private TMP_Text _chatText;
        private Image _emojiImage;
        private bool _bound;

        public RectTransform HandAnchor { get { Bind(); return _handAnchor; } }

        private void Awake() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _name = UiBind.Get<TMP_Text>(transform, "NameLabel");
            _stripe = UiBind.Get<Image>(transform, "TeamStripe");
            _turn = UiBind.Go(transform, "TurnRing");
            _bubble = UiBind.Go(transform, "Bubble");
            _bubbleText = UiBind.Get<TMP_Text>(transform, "Bubble/BubbleText");
            _dealer = UiBind.Go(transform, "DealerChip");
            _handAnchor = UiBind.Get<RectTransform>(transform, "HandAnchor");
            Transform badge = transform.Find("CountBadge");
            if (badge != null)
            {
                _countBadge = badge.gameObject;
                _count = UiBind.Get<TMP_Text>(transform, "CountBadge/CountText");
            }
            Transform chat = transform.Find("ChatBubble");          // optional: older scenes have no chat
            if (chat != null) { _chatBubble = chat.gameObject; _chatText = chat.Find("Text").GetComponent<TMP_Text>(); _chatBubble.SetActive(false); }
            Transform emoji = transform.Find("EmojiBadge");
            if (emoji != null) { _emojiBadge = emoji.gameObject; _emojiImage = emoji.Find("Icon").GetComponent<Image>(); _emojiBadge.SetActive(false); }
            _turn.SetActive(false);
            _bubble.SetActive(false);
            _dealer.SetActive(false);
        }

        /// <summary>Shows a chat line or emoji above this seat for a few seconds; a new one replaces the old.</summary>
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
            if (m.IsEmoji)
            {
                _emojiImage.sprite = EmojiArt.Get(m.EmojiId);
                _emojiBadge.SetActive(true);
                var rt = (RectTransform)_emojiBadge.transform;
                rt.localScale = Vector3.zero;
                yield return Tween.Local(rt, rt.anchoredPosition, 0f, 1f, 0.22f);
                yield return new WaitForSeconds(2.4f);
                _emojiBadge.SetActive(false);
            }
            else
            {
                _chatText.text = m.Text;
                _chatBubble.SetActive(true);
                yield return new WaitForSeconds(2.0f + m.Text.Length * 0.05f);
                _chatBubble.SetActive(false);
            }
        }

        public void Setup(string playerName, Color teamColor)
        {
            Bind();
            _name.text = playerName;
            _stripe.color = teamColor;
        }

        public void SetTurn(bool on) { Bind(); _turn.SetActive(on); }
        public void SetDealer(bool on) { Bind(); _dealer.SetActive(on); }

        public void SetCardCount(int n)
        {
            Bind();
            if (_count != null) _count.text = n.ToString();
            if (_countBadge != null) _countBadge.SetActive(n > 0);
        }

        public void ShowBubble(string text, float seconds = 0f)
        {
            Bind();
            _bubbleText.text = text;
            _bubble.SetActive(true);
            if (_bubbleRoutine != null) StopCoroutine(_bubbleRoutine);
            if (seconds > 0f) _bubbleRoutine = StartCoroutine(HideAfter(seconds));
        }

        public void HideBubble()
        {
            Bind();
            if (_bubbleRoutine != null) StopCoroutine(_bubbleRoutine);
            _bubble.SetActive(false);
        }

        private IEnumerator HideAfter(float s)
        {
            yield return new WaitForSeconds(s);
            _bubble.SetActive(false);
        }
    }
}
