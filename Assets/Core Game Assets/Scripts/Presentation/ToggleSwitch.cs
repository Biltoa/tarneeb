using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>A rounded-rectangle switch (children: Track, Knob). Animates the knob and swaps the track sprite.</summary>
    public sealed class ToggleSwitch : MonoBehaviour, IPointerClickHandler
    {
        private Image _track;
        private RectTransform _knob;
        private bool _on, _bound;
        private float _pos;

        public event Action<bool> Changed;
        public bool IsOn => _on;

        private void Awake() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _track = UiBind.Get<Image>(transform, "Track");
            _knob = UiBind.Get<RectTransform>(transform, "Knob");
            _knob.anchorMin = _knob.anchorMax = new Vector2(0.5f, 0.5f);     // centre-anchored so x is measured from the middle of the track
        }

        public void Set(bool on, bool notify = false)
        {
            Bind();
            _on = on;
            _pos = on ? 1f : 0f;
            Refresh(true);
            if (notify) Changed?.Invoke(on);
        }

        public void OnPointerClick(PointerEventData e)
        {
            Bind();
            _on = !_on;
            AudioManager.Play(_on ? Sfx.ToggleOn : Sfx.ToggleOff, 0.8f);
            Changed?.Invoke(_on);
        }

        private void Update()
        {
            if (!_bound) return;
            float target = _on ? 1f : 0f;
            if (Mathf.Approximately(_pos, target)) return;
            _pos = Mathf.MoveTowards(_pos, target, Time.unscaledDeltaTime * 9f);
            Refresh(false);
        }

        private void Refresh(bool swap)
        {
            float w = ((RectTransform)transform).rect.width;
            float travel = Mathf.Max(0f, w - _knob.rect.width - 12f);
            _knob.anchoredPosition = new Vector2(-travel * 0.5f + travel * Tween.Evaluate(Ease.OutCubic, _pos), 0f);
            _track.sprite = UiArt.Get(_on ? "toggle_track_on_9s24" : "toggle_track_9s24");
        }
    }
}
