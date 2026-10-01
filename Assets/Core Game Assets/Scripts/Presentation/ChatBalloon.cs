using Tarneeb.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>
    /// A dialogue balloon that grows out of a player's portrait. Children (built by GameSceneAssembler): Tail, Body, Cover, Text, Icon.
    /// The root sits on the tail's tip; the balloon extends to <see cref="direction"/> (+1 right, -1 left). It sizes itself to the text, shows
    /// at most two lines, and shortens long messages ("Nice bid par...") so nothing overflows the balloon.
    /// </summary>
    public sealed class ChatBalloon : MonoBehaviour
    {
        public const int MaxChars = 28;
        private const float MaxTextWidth = 232f, LineHeight = 34f, PadX = 28f, PadY = 15f, TailLength = 36f, Margin = 8f;

        public int direction = 1;

        private RectTransform _tail, _body, _cover;
        private TMP_Text _text;
        private Image _icon;
        private bool _bound;

        public RectTransform Body { get { Bind(); return _body; } }

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _tail = UiBind.Get<RectTransform>(transform, "Tail");
            _body = UiBind.Get<RectTransform>(transform, "Body");
            _cover = UiBind.Get<RectTransform>(transform, "Cover");
            _text = UiBind.Get<TMP_Text>(transform, "Text");
            _icon = UiBind.Get<Image>(transform, "Icon");
            _tail.pivot = new Vector2(0.03f, 0.262f);                              // tail tip is at 3% / 74% of the sprite
            _tail.sizeDelta = new Vector2(56f, 49f);
            _tail.anchoredPosition = Vector2.zero;
            _tail.localScale = new Vector3(direction, 1f, 1f);
        }

        /// <summary>Long text becomes "first words..." (cut at a word boundary when there is one), never more than MaxChars characters.</summary>
        public static string Shorten(string text, int maxChars = MaxChars)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Trim();
            if (text.Length <= maxChars) return text;
            int cut = maxChars - 3;
            int space = text.LastIndexOf(' ', cut);
            if (space >= cut * 0.55f) cut = space;
            return text.Substring(0, cut).TrimEnd() + "...";
        }

        public void ShowText(string message)
        {
            Bind();
            _icon.gameObject.SetActive(false);
            _text.gameObject.SetActive(true);
            _text.textWrappingMode = TextWrappingModes.NoWrap;                     // lines are broken here (at most two), never by TMP
            _text.overflowMode = TextOverflowModes.Overflow;
            string logical = Shorten(Loc.T(message));                                // known quick phrases are shown in the reader's language
            string shown = Loc.Display(logical);
            int lines = 1;
            if (_text.GetPreferredValues(shown).x > MaxTextWidth)
            {
                // break the LOGICAL text in two at the space nearest the middle, then shape each line on its own (line 1 stays on top)
                int mid = logical.Length / 2, cut = -1;
                for (int d = 0; d < logical.Length && cut < 0; d++)
                {
                    if (mid - d > 0 && logical[mid - d] == ' ') cut = mid - d;
                    else if (mid + d < logical.Length - 1 && logical[mid + d] == ' ') cut = mid + d;
                }
                string a = cut > 0 ? logical.Substring(0, cut) : logical.Substring(0, mid);
                string b = cut > 0 ? logical.Substring(cut + 1) : logical.Substring(mid);
                shown = Loc.Display(a) + "\n" + Loc.Display(b);
                lines = 2;
            }
            _text.text = shown;
            float tw = Mathf.Clamp(Mathf.Ceil(_text.GetPreferredValues(shown).x) + 12f, 44f, MaxTextWidth + 20f);
            float th = LineHeight * lines;
            _text.rectTransform.sizeDelta = new Vector2(tw + 4f, th);
            Layout(tw + PadX * 2f, th + PadY * 2f, _text.rectTransform);
        }

        public void ShowEmoji(Sprite sprite)
        {
            Bind();
            _text.gameObject.SetActive(false);
            _icon.gameObject.SetActive(true);
            _icon.sprite = sprite;
            Layout(120f, 108f, _icon.rectTransform);
        }

        private void Layout(float visibleW, float visibleH, RectTransform content)
        {
            float x = direction * (TailLength + visibleW * 0.5f);
            float y = 10f;                                                         // the tail base is 10 units above its tip: it meets the body's middle
            _body.sizeDelta = new Vector2(visibleW + Margin * 2f, visibleH + Margin * 2f);
            _body.anchoredPosition = new Vector2(x, y);
            content.anchoredPosition = new Vector2(x, y);
            _cover.anchoredPosition = new Vector2(direction * (TailLength + 1f), y);
        }
    }
}
