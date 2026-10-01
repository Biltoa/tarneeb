using System;
using System.Collections;
using Tarneeb.Core.Cards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>
    /// One card on screen. Prefab layout: Shadow, Face, Back, Dim, Glow (all Images) under a root with a CanvasGroup.
    /// The face is a sprite from the card atlas (Tools/gen_cards.py); Dim and Shadow reuse its rounded silhouette.
    /// </summary>
    public sealed class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const float Width = 166f;
        public const float Height = 232f;

        private Image _face, _back, _dim, _shadow, _glow;
        private CanvasGroup _group;
        private bool _bound;

        public Card Card { get; private set; }
        public bool FaceUp { get; private set; }
        public bool IsDimmed => _dim != null && _dim.gameObject.activeSelf;
        public RectTransform Rect => (RectTransform)transform;

        /// <summary>Card was tapped.</summary>
        public event Action<CardView> Clicked;
        /// <summary>Press-and-drag gestures on the card (the hand decides what they mean).</summary>
        public event Action<CardView, PointerEventData> DragBegan, Dragging, DragEnded;
        /// <summary>Pointer entered (true) or left (false); mouse only.</summary>
        public event Action<CardView, bool> Hovered;

        private void Awake() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _face = UiBind.Get<Image>(transform, "Face");
            _back = UiBind.Get<Image>(transform, "Back");
            _dim = UiBind.Get<Image>(transform, "Dim");
            _shadow = transform.Find("Shadow") != null ? transform.Find("Shadow").GetComponent<Image>() : null;
            _glow = transform.Find("Glow") != null ? transform.Find("Glow").GetComponent<Image>() : null;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _back.sprite = CardArt.Back;
            _dim.gameObject.SetActive(false);
            if (_glow != null) _glow.gameObject.SetActive(false);
        }

        public void Show(Card card, bool faceUp)
        {
            Bind();
            Card = card;
            Sprite face = CardArt.Face(card);
            _face.sprite = face;
            _dim.sprite = face;
            name = "Card_" + card;
            SetFaceUp(faceUp);
        }

        public void SetFaceUp(bool faceUp)
        {
            Bind();
            FaceUp = faceUp;
            _face.gameObject.SetActive(faceUp);
            _back.gameObject.SetActive(!faceUp);
        }

        /// <summary>Turns the card over with a scale-X flip (face changes at the halfway point).</summary>
        public IEnumerator Flip(bool faceUp, float duration = 0.22f)
        {
            Bind();
            if (FaceUp == faceUp) yield break;
            Vector3 s = transform.localScale;
            duration = duration / Mathf.Max(0.1f, Tween.SpeedMultiplier);
            for (float t = 0f; t < duration * 0.5f; t += Time.deltaTime)
            {
                transform.localScale = new Vector3(Mathf.Lerp(s.x, 0f, t / (duration * 0.5f)), s.y, s.z);
                yield return null;
            }
            SetFaceUp(faceUp);
            for (float t = 0f; t < duration * 0.5f; t += Time.deltaTime)
            {
                transform.localScale = new Vector3(Mathf.Lerp(0f, s.x, t / (duration * 0.5f)), s.y, s.z);
                yield return null;
            }
            transform.localScale = s;
        }

        public void SetDimmed(bool dimmed)
        {
            Bind();
            _dim.gameObject.SetActive(dimmed);
        }

        /// <summary>Gold border on the card (selected or being dragged); drawn inside the card's own rectangle.</summary>
        public void SetHighlight(bool on)
        {
            Bind();
            if (_glow != null) _glow.gameObject.SetActive(on);
        }

        public void SetShadow(bool on)
        {
            Bind();
            if (_shadow != null) _shadow.gameObject.SetActive(on);
        }

        /// <summary>Whether the card blocks/receives pointer input.</summary>
        public void SetInteractable(bool value)
        {
            Bind();
            _group.blocksRaycasts = value;
        }

        public void OnPointerClick(PointerEventData e) => Clicked?.Invoke(this);
        public void OnPointerEnter(PointerEventData e) => Hovered?.Invoke(this, true);
        public void OnPointerExit(PointerEventData e) => Hovered?.Invoke(this, false);
        public void OnBeginDrag(PointerEventData e) => DragBegan?.Invoke(this, e);
        public void OnDrag(PointerEventData e) => Dragging?.Invoke(this, e);
        public void OnEndDrag(PointerEventData e) => DragEnded?.Invoke(this, e);
    }
}
