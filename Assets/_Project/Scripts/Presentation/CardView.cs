using System;
using Tarneeb.Core.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>One card on screen. Prefab layout: Face/{Rank,SuitSmall,SuitBig}, Back, Dim.</summary>
    public sealed class CardView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const float Width = 170f;
        public const float Height = 246f;

        private TMP_Text _rank;
        private Image _suitSmall, _suitBig, _dim;
        private GameObject _face, _back;
        private CanvasGroup _group;
        private bool _bound;
        private float _dragStartY;

        public Card Card { get; private set; }
        public bool FaceUp { get; private set; }
        public bool IsDimmed => _dim != null && _dim.gameObject.activeSelf;
        public RectTransform Rect => (RectTransform)transform;

        /// <summary>Card was tapped.</summary>
        public event Action<CardView> Clicked;
        /// <summary>Card was dragged upward and released.</summary>
        public event Action<CardView> Flicked;

        private void Awake() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _rank = UiBind.Get<TMP_Text>(transform, "Face/Rank");
            _suitSmall = UiBind.Get<Image>(transform, "Face/SuitSmall");
            _suitBig = UiBind.Get<Image>(transform, "Face/SuitBig");
            _face = UiBind.Go(transform, "Face");
            _back = UiBind.Go(transform, "Back");
            _dim = UiBind.Get<Image>(transform, "Dim");
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        public void Show(Card card, bool faceUp)
        {
            Bind();
            Card = card;
            Color c = CardArt.SuitColor(card.Suit);
            _rank.text = Card.RankSymbol(card.Rank);
            _rank.color = c;
            Sprite icon = CardArt.Instance != null ? CardArt.Instance.SuitSprite(card.Suit) : null;
            _suitSmall.sprite = icon; _suitSmall.color = c;
            _suitBig.sprite = icon; _suitBig.color = c;
            name = "Card_" + card;
            SetFaceUp(faceUp);
        }

        public void SetFaceUp(bool faceUp)
        {
            Bind();
            FaceUp = faceUp;
            _face.SetActive(faceUp);
            _back.SetActive(!faceUp);
        }

        public void SetDimmed(bool dimmed)
        {
            Bind();
            _dim.gameObject.SetActive(dimmed);
        }

        /// <summary>Whether the card blocks/receives pointer input.</summary>
        public void SetInteractable(bool value)
        {
            Bind();
            _group.blocksRaycasts = value;
        }

        public void OnPointerClick(PointerEventData e) => Clicked?.Invoke(this);

        public void OnBeginDrag(PointerEventData e) => _dragStartY = e.position.y;
        public void OnDrag(PointerEventData e) { }

        public void OnEndDrag(PointerEventData e)
        {
            if (e.position.y - _dragStartY > Screen.height * 0.06f) Flicked?.Invoke(this);
        }
    }
}
