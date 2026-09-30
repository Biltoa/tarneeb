using System;
using System.Collections;
using System.Collections.Generic;
using Tarneeb.Core.Cards;
using UnityEngine;

namespace Tarneeb.Presentation
{
    /// <summary>The local player's fanned hand (Figma "Local player hand"). Handles layout, hover lift and tap/flick-to-play input.</summary>
    public sealed class HandView : MonoBehaviour
    {
        public float maxSpacing = 74f;
        public float width = 836f;
        public float liftSelected = 46f;
        public float liftHover = 20f;

        private readonly List<CardView> _cards = new List<CardView>();
        private CardView _selected, _hover;
        private HashSet<Card> _legal;
        private Action<Card> _onPlay;

        public IReadOnlyList<CardView> Cards => _cards;
        public int Count => _cards.Count;
        /// <summary>True while the hand is waiting for the local player to pick a card.</summary>
        public bool AwaitingInput => _onPlay != null;

        public void Add(CardView cv)
        {
            cv.transform.SetParent(transform, true);
            cv.Clicked += OnClicked;
            cv.Flicked += OnFlicked;
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
                cv.Flicked -= OnFlicked;
                cv.Hovered -= OnHovered;
                cv.SetHighlight(false);
                if (_selected == cv) _selected = null;
                if (_hover == cv) _hover = null;
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
                routines.Add(Tween.Local(_cards[i].Rect, pos, rot, lift > 0f ? 1.04f : 1f, duration));
            }
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
            _selected = _hover = null;
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

        private void OnFlicked(CardView cv)
        {
            if (_onPlay == null || _legal == null || !_legal.Contains(cv.Card)) return;
            Play(cv);
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
