using System;
using System.Collections;
using System.Collections.Generic;
using Tarneeb.Core.Cards;
using UnityEngine;

namespace Tarneeb.Presentation
{
    /// <summary>The local player's fanned hand. Handles layout and tap/flick-to-play input.</summary>
    public sealed class HandView : MonoBehaviour
    {
        public float maxSpacing = 74f;
        public float width = 980f;
        public float angleStep = 2.0f;
        public float archK = 1.6f;
        public float liftY = 52f;

        private readonly List<CardView> _cards = new List<CardView>();
        private CardView _selected;
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
                if (_selected == cv) _selected = null;
                return cv;
            }
            return null;
        }

        public void Clear()
        {
            EndInput();
            for (int i = 0; i < _cards.Count; i++) if (_cards[i] != null) Destroy(_cards[i].gameObject);
            _cards.Clear();
            _selected = null;
        }

        public void Slot(int index, int total, out Vector2 pos, out float rot)
        {
            float d = index - (total - 1) * 0.5f;
            float spacing = total > 1 ? Mathf.Min(maxSpacing, (width - CardView.Width) / (total - 1)) : 0f;
            pos = new Vector2(d * spacing, -d * d * archK);
            rot = -d * angleStep;
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

        public IEnumerator Arrange(float duration = 0.22f)
        {
            var routines = new List<IEnumerator>(_cards.Count);
            for (int i = 0; i < _cards.Count; i++)
            {
                Slot(i, _cards.Count, out Vector2 pos, out float rot);
                bool sel = _cards[i] == _selected;
                if (sel) { pos.y += liftY; rot = 0f; }
                _cards[i].transform.SetSiblingIndex(i);
                routines.Add(Tween.Local(_cards[i].Rect, pos, rot, 1f, duration));
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
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] == null) continue;
                _cards[i].SetDimmed(false);
                _cards[i].SetInteractable(false);
            }
            if (_selected != null)
            {
                _selected = null;
                StartCoroutine(Arrange(0.12f));
            }
        }

        private void OnClicked(CardView cv)
        {
            if (_onPlay == null || _legal == null || !_legal.Contains(cv.Card)) return;
            if (_selected == cv) { Play(cv); return; }
            _selected = cv;
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
            cb?.Invoke(cv.Card);
        }
    }
}
