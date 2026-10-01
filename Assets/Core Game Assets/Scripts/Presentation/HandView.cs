using System;
using System.Collections;
using System.Collections.Generic;
using Tarneeb.Core.Cards;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Tarneeb.Presentation
{
    /// <summary>The local player's fanned hand (Figma "Local player hand"). Handles layout, hover lift and tap/flick-to-play input.</summary>
    public sealed class HandView : MonoBehaviour
    {
        public float maxSpacing = 78f;
        public float width = 800f;
        public float liftSelected = 30f;
        public float liftHover = 14f;

        private readonly List<CardView> _cards = new List<CardView>();
        private CardView _selected, _hover, _dragging;
        private Vector3 _grabOffset;
        private float _zoneGlow;
        private bool _overZone, _passedThreshold;
        /// <summary>Share of the deck-to-table-centre distance a card must be dragged for a release (or return) to count as playing it.</summary>
        public float playThreshold = 0.25f;
        private HashSet<Card> _legal;
        private Action<Card> _onPlay;

        /// <summary>Releasing a dragged card over this rect plays it; anywhere else it returns to the hand. Set by TableView (Table/DropZone).</summary>
        public RectTransform DropZone;
        private UnityEngine.UI.Image _zoneImage;

        public IReadOnlyList<CardView> Cards => _cards;
        public int Count => _cards.Count;
        /// <summary>True while the hand is waiting for the local player to pick a card.</summary>
        public bool AwaitingInput => _onPlay != null;

        public void Add(CardView cv)
        {
            cv.transform.SetParent(transform, true);
            cv.Clicked += OnClicked;
            cv.DragBegan += OnDragBegan; cv.Dragging += OnDragging; cv.DragEnded += OnDragEnded;
            cv.Hovered += OnHovered;
            _cards.Add(cv);
        }

        public CardView Remove(Card card)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].Card != card) continue;
                CardView cv = _cards[i];
                _cards.RemoveAt(i);
                cv.Clicked -= OnClicked;
                cv.DragBegan -= OnDragBegan; cv.Dragging -= OnDragging; cv.DragEnded -= OnDragEnded;
                cv.Hovered -= OnHovered;
                cv.SetHighlight(false);
                if (_selected == cv) _selected = null;
                if (_hover == cv) _hover = null;
                if (_dragging == cv) _dragging = null;
                return cv;
            }
            return null;
        }

        public void Clear()
        {
            EndInput();
            for (int i = 0; i < _cards.Count; i++) if (_cards[i] != null) Destroy(_cards[i].gameObject);
            _cards.Clear();
            _selected = _hover = null;
        }

        /// <summary>Fan geometry: even spacing, a shallow arch and a slight outward tilt, like the Figma hand.</summary>
        public void Slot(int index, int total, out Vector2 pos, out float rot)
        {
            float half = (total - 1) * 0.5f;
            float d = index - half;
            float t = half > 0f ? d / half : 0f;                                  // -1 .. 1 across the fan
            float fill = Mathf.Clamp01((total - 1) / 9f);                         // fewer cards: flatter fan
            float spacing = total > 1 ? Mathf.Min(maxSpacing, (width - CardView.Width) / (total - 1)) : 0f;
            pos = new Vector2(d * spacing, -Mathf.Lerp(6f, 30f, fill) * t * t);
            rot = -t * Mathf.Lerp(1.5f, 6.5f, fill);
        }

        public Vector3 SlotWorld(int index, int total)
        {
            Slot(index, total, out Vector2 p, out _);
            return transform.TransformPoint(new Vector3(p.x, p.y, 0f));
        }

        public float SlotRotation(int index, int total)
        {
            Slot(index, total, out _, out float r);
            return r;
        }

        private float Lift(CardView cv) => cv == _selected ? liftSelected : (cv == _hover && AwaitingInput && _legal != null && _legal.Contains(cv.Card) ? liftHover : 0f);

        public IEnumerator Arrange(float duration = 0.22f)
        {
            var routines = new List<IEnumerator>(_cards.Count);
            for (int i = 0; i < _cards.Count; i++)
            {
                Slot(i, _cards.Count, out Vector2 pos, out float rot);
                float lift = Lift(_cards[i]);
                if (_cards[i] == _selected) rot = 0f;
                pos.y += lift;
                _cards[i].transform.SetSiblingIndex(i);
                _cards[i].SetHighlight(_cards[i] == _selected);
                if (_cards[i] == _dragging) continue;                              // the pointer owns it
                routines.Add(Tween.Local(_cards[i].Rect, pos, rot, lift > 0f ? 1.04f : 1f, duration));
            }
            if (_selected != null) _selected.transform.SetAsLastSibling();          // the picked card is shown whole, in front of its neighbours
            yield return Tween.All(this, routines);
        }

        public void ArrangeImmediate()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                Slot(i, _cards.Count, out Vector2 pos, out float rot);
                _cards[i].transform.SetSiblingIndex(i);
                _cards[i].Rect.anchoredPosition = pos;
                _cards[i].Rect.localRotation = Quaternion.Euler(0, 0, rot);
                _cards[i].Rect.localScale = Vector3.one;
            }
        }

        // ------------------------------------------------------------ input

        public void BeginInput(ICollection<Card> legal, Action<Card> onPlay)
        {
            _legal = new HashSet<Card>(legal);
            _onPlay = onPlay;
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].SetDimmed(!_legal.Contains(_cards[i].Card));
                _cards[i].SetInteractable(true);
            }
        }

        public void EndInput()
        {
            _onPlay = null;
            _legal = null;
            bool moved = _selected != null || _hover != null;
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] == null) continue;
                _cards[i].SetDimmed(false);
                _cards[i].SetHighlight(false);
                _cards[i].SetInteractable(false);
            }
            _selected = _hover = _dragging = null;
            if (moved && isActiveAndEnabled) StartCoroutine(Arrange(0.12f));
        }

        private void OnHovered(CardView cv, bool over)
        {
            if (!AwaitingInput) return;
            CardView next = over ? cv : (_hover == cv ? null : _hover);
            if (next == _hover) return;
            _hover = next;
            StartCoroutine(Arrange(0.09f));
        }

        private void OnClicked(CardView cv)
        {
            if (_onPlay == null || _legal == null) return;
            if (!_legal.Contains(cv.Card)) { AudioManager.Play(Sfx.Deny, 0.7f); StartCoroutine(Tween.Punch(cv.transform, -0.06f, 0.2f)); return; }
            if (_selected == cv) { Play(cv); return; }
            _selected = cv;
            AudioManager.PlayVaried(Sfx.CardFlip, 0.5f);
            StartCoroutine(Arrange(0.12f));
        }

        // ------------------------------------------------------------ press, drag, drop

        private bool CanDrag(CardView cv) => AwaitingInput && _legal != null && _legal.Contains(cv.Card);

        /// <summary>The UI camera (null for the normal overlay canvas).</summary>
        private Camera UiCamera
        {
            get
            {
                Canvas c = GetComponentInParent<Canvas>();
                c = c != null ? c.rootCanvas : null;
                return c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
            }
        }

        private bool Pointer(PointerEventData e, out Vector3 world) =>
            RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)transform, e.position, UiCamera, out world);

        private void OnDragBegan(CardView cv, PointerEventData e)
        {
            if (!CanDrag(cv) || !Pointer(e, out Vector3 w)) { _dragging = null; return; }
            _dragging = cv;
            _passedThreshold = false;
            _selected = cv;
            _hover = null;
            _grabOffset = cv.transform.position - w;
            cv.SetHighlight(true);
            cv.transform.SetAsLastSibling();
            cv.Rect.localRotation = Quaternion.identity;
            cv.Rect.localScale = Vector3.one * 1.06f;
            AudioManager.PlayVaried(Sfx.CardFlip, 0.5f);
            StartCoroutine(Arrange(0.12f));                                        // the rest of the hand settles
        }

        private void OnDragging(CardView cv, PointerEventData e)
        {
            if (_dragging != cv || !Pointer(e, out Vector3 w)) return;
            cv.transform.position = w + _grabOffset;
            _overZone = DropZone != null && RectTransformUtility.RectangleContainsScreenPoint(DropZone, e.position, UiCamera);
            if (DragProgress(cv) >= playThreshold) _passedThreshold = true;
            else if (_passedThreshold) { ReturnToHand(cv); }                         // dragged out, then back under the threshold while still holding: back in the hand, free to pick another card
        }

        /// <summary>How far the card has travelled from the hand towards the table centre (0 = at the hand, 1 = at the centre).</summary>
        private float DragProgress(CardView cv)
        {
            if (DropZone == null) return 0f;
            Vector3 from = transform.position;
            Vector3 to = DropZone.TransformPoint(DropZone.rect.center);
            Vector3 axis = to - from;
            float len2 = axis.sqrMagnitude;
            return len2 < 1e-6f ? 0f : Vector3.Dot(cv.transform.position - from, axis) / len2;
        }

        private void ReturnToHand(CardView cv)
        {
            _dragging = null;
            _overZone = false;
            _passedThreshold = false;
            _selected = null;
            cv.SetHighlight(false);
            AudioManager.PlayVaried(Sfx.CardFlip, 0.4f);
            StartCoroutine(Arrange(0.24f));
        }

        private void OnDragEnded(CardView cv, PointerEventData e)
        {
            if (_dragging != cv) return;
            bool play = DragProgress(cv) >= playThreshold;
            if (play) { _dragging = null; _overZone = false; _passedThreshold = false; Play(cv); return; }
            ReturnToHand(cv);                                                       // released short of the threshold: back into the hand
        }

        /// <summary>The table's drop area lights up while a card is being dragged, brighter when the pointer is over it.</summary>
        private void Update()
        {
            if (DropZone == null) return;
            if (_zoneImage == null) _zoneImage = DropZone.GetComponent<UnityEngine.UI.Image>();
            if (_zoneImage == null) return;
            float target = _dragging == null ? 0f : _overZone ? 0.42f : 0.16f;
            _zoneGlow = Mathf.MoveTowards(_zoneGlow, target, Time.unscaledDeltaTime * 1.2f);
            Color c = _zoneImage.color; c.a = _zoneGlow; _zoneImage.color = c;
        }

        private void Play(CardView cv)
        {
            Action<Card> cb = _onPlay;
            _onPlay = null;
            cv.SetHighlight(false);
            cb?.Invoke(cv.Card);
        }
    }
}
