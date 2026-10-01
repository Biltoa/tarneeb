using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>
    /// Main-menu atmosphere: a fan of soft rays turning very slowly, a breathing glow behind the cards, and small glowing gold dots that drift
    /// upwards, sway and twinkle. Pure uGUI (overlay canvases cannot draw ParticleSystems); children "Rays" and "Glow" are optional.
    /// </summary>
    public sealed class MenuAmbience : MonoBehaviour
    {
        private const int EmberCount = 48;
        private const float W = 850f, H = 1850f;

        private struct Ember { public RectTransform rt; public Image img; public float x, y, speed, size, phase, sway, freq, life, age, alpha; }

        private RectTransform _rays, _glow;
        private Image _glowImage;
        private readonly Ember[] _embers = new Ember[EmberCount];
        private float _clock;

        private void Awake()
        {
            _rays = transform.Find("Rays") as RectTransform;
            _glow = transform.Find("Glow") as RectTransform;
            if (_glow != null) _glowImage = _glow.GetComponent<Image>();
            Sprite dot = UiArt.Get("particle_glow");
            for (int i = 0; i < EmberCount; i++)
            {
                var go = new GameObject("Ember", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                var im = go.GetComponent<Image>();
                im.sprite = dot; im.raycastTarget = false;
                _embers[i] = new Ember { rt = rt, img = im };
                Respawn(ref _embers[i], true);
            }
        }

        private static void Respawn(ref Ember e, bool scatter)
        {
            e.x = Random.Range(20f, W - 20f);
            e.size = Random.Range(9f, 24f);
            e.speed = Random.Range(14f, 44f) * (0.6f + e.size / 48f);          // bigger (closer) embers drift a little faster
            e.phase = Random.value * 6.28f;
            e.sway = Random.Range(10f, 34f);
            e.freq = Random.Range(0.25f, 0.6f);
            e.life = Random.Range(9f, 16f);
            e.age = scatter ? Random.value * e.life : 0f;
            e.alpha = Random.Range(0.45f, 0.95f);
            e.y = scatter ? Random.Range(0f, H) : Random.Range(H * 0.75f, H + 60f);
            e.img.color = new Color(1f, Random.Range(0.8f, 0.92f), Random.Range(0.45f, 0.68f), 0f);
            e.rt.sizeDelta = new Vector2(e.size, e.size);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _clock += dt;
            if (_rays != null) _rays.localRotation = Quaternion.Euler(0f, 0f, _clock * 1.6f);
            if (_glow != null)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(_clock * 0.9f);
                _glow.localScale = Vector3.one * (1f + 0.05f * k);
                if (_glowImage != null) { Color c = _glowImage.color; c.a = 0.11f + 0.05f * k; _glowImage.color = c; }
            }
            for (int i = 0; i < EmberCount; i++)
            {
                ref Ember e = ref _embers[i];
                e.age += dt;
                e.y -= e.speed * dt;                                            // y counts downwards from the top edge: embers rise
                if (e.age >= e.life || e.y < -50f) { Respawn(ref e, false); continue; }
                float fade = Mathf.Clamp01(Mathf.Min(e.age, e.life - e.age) / 2.2f);
                float twinkle = 0.8f + 0.2f * Mathf.Sin(_clock * 2.4f + e.phase);
                Color c = e.img.color; c.a = e.alpha * fade * twinkle; e.img.color = c;
                e.rt.anchoredPosition = new Vector2(e.x + Mathf.Sin(_clock * e.freq + e.phase) * e.sway, -e.y);
            }
        }
    }
}
