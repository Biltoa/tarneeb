using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>Press squash, hover lift and click sound for any Button. Added to every button by the scene builders.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class ButtonFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public bool silent;
        public float pressScale = 0.93f, hoverScale = 1.04f;

        private Button _button;
        private float _target = 1f;
        private bool _down, _over;

        private void Awake() => _button = GetComponent<Button>();
        private void OnDisable() { transform.localScale = Vector3.one; _down = _over = false; _target = 1f; }

        private void Update()
        {
            float s = transform.localScale.x;
            if (Mathf.Abs(s - _target) < 0.001f) return;
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 22f);
            transform.localScale = Vector3.one * Mathf.Lerp(s, _target, k);
        }

        private void Retarget() => _target = !_button.IsInteractable() ? 1f : _down ? pressScale : _over ? hoverScale : 1f;
        public void OnPointerDown(PointerEventData e) { _down = true; Retarget(); }
        public void OnPointerUp(PointerEventData e) { _down = false; Retarget(); }
        public void OnPointerEnter(PointerEventData e) { _over = true; Retarget(); }
        public void OnPointerExit(PointerEventData e) { _over = false; _down = false; Retarget(); }

        public void OnPointerClick(PointerEventData e)
        {
            if (silent) return;
            if (_button.IsInteractable()) AudioManager.Play(Sfx.Click);
            else AudioManager.Play(Sfx.Deny, 0.6f);
        }
    }
}
