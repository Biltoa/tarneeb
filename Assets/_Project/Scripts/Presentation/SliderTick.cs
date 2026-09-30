using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>Plays a soft tick while a slider is dragged (throttled).</summary>
    public sealed class SliderTick : MonoBehaviour
    {
        private System.Action _tick;
        private float _last;

        public void Init(System.Action tick)
        {
            _tick = tick;
            GetComponent<Slider>().onValueChanged.AddListener(_ =>
            {
                if (Time.unscaledTime - _last < 0.12f) return;
                _last = Time.unscaledTime;
                _tick?.Invoke();
            });
        }
    }
}
