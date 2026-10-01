using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>
    /// Lightweight UI effects on a pooled set of Images (uGUI overlay canvases cannot draw ParticleSystems): bursts, twinkling
    /// sparkles, confetti, expanding rings, glow flashes, floating text and a screen shake. World positions in, design-space out.
    /// </summary>
    public sealed class UiFx : MonoBehaviour
    {
        public static UiFx Instance { get; private set; }

        private const int PoolSize = 220;

        private struct P
        {
            public bool alive;
            public Vector2 pos, vel;
            public float age, life, size0, size1, rot, rotVel, gravity, drag;
            public Color c0, c1;
            public bool twinkle;
        }

        private RectTransform _layer, _shakeTarget;
        private readonly Image[] _img = new Image[PoolSize];
        private readonly P[] _p = new P[PoolSize];
        private int _cursor;
        private float _shakeAmp, _shakeTime, _shakeLeft;
        private Vector2 _shakeHome;
        private bool _homeSet;

        private void Awake()
        {
            Instance = this;
            _layer = (RectTransform)transform;
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("fx", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_layer, false);
                var im = go.GetComponent<Image>();
                im.raycastTarget = false;
                go.SetActive(false);
                _img[i] = im;
            }
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>The rect that Shake() moves (the whole design frame).</summary>
        public void SetShakeTarget(RectTransform t) { _shakeTarget = t; _homeSet = false; }

        // ------------------------------------------------------------ emitters

        private int Take()
        {
            for (int k = 0; k < PoolSize; k++)
            {
                int i = (_cursor + k) % PoolSize;
                if (!_p[i].alive) { _cursor = (i + 1) % PoolSize; return i; }
            }
            int j = _cursor; _cursor = (_cursor + 1) % PoolSize; return j;         // pool full: recycle the oldest slot
        }

        private Vector2 Local(Vector3 world) => (Vector2)_layer.InverseTransformPoint(world);

        private void Spawn(Vector2 pos, Vector2 vel, float life, float size0, float size1, Color c0, Color c1, Sprite sprite,
            float gravity = 0f, float drag = 0f, float rotVel = 0f, bool twinkle = false, float rot = 0f)
        {
            int i = Take();
            _p[i] = new P { alive = true, pos = pos, vel = vel, life = Mathf.Max(0.05f, life), size0 = size0, size1 = size1, c0 = c0, c1 = c1,
                gravity = gravity, drag = drag, rotVel = rotVel, twinkle = twinkle, rot = rot };
            Image im = _img[i];
            im.sprite = sprite;
            im.gameObject.SetActive(true);
            Apply(i);
        }

        /// <summary>Radial burst of soft dots.</summary>
        public void Burst(Vector3 world, int count, Color color, float speed = 260f, float life = 0.6f, float size = 22f, float gravity = -240f)
        {
            Vector2 o = Local(world);
            Sprite dot = UiArt.Get("particle_dot");
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f, v = speed * Random.Range(0.35f, 1f);
                Color c1 = color; c1.a = 0f;
                Spawn(o, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * v, life * Random.Range(0.7f, 1.2f), size * Random.Range(0.6f, 1.2f), size * 0.2f, color, c1, dot, gravity, 2.2f);
            }
        }

        /// <summary>Twinkling four-point stars drifting upwards.</summary>
        public void Sparkles(Vector3 world, int count, float radius, Color tint, float life = 0.9f, float size = 34f)
        {
            Vector2 o = Local(world);
            Sprite star = UiArt.Get("particle_star");
            for (int i = 0; i < count; i++)
            {
                Vector2 off = Random.insideUnitCircle * radius;
                Color c1 = tint; c1.a = 0f;
                Spawn(o + off, new Vector2(Random.Range(-14f, 14f), Random.Range(10f, 60f)), life * Random.Range(0.7f, 1.3f),
                    size * Random.Range(0.5f, 1.2f), size * 0.1f, tint, c1, star, 0f, 0.6f, Random.Range(-90f, 90f), true, Random.Range(0f, 90f));
            }
        }

        /// <summary>Colourful confetti falling from the top of the frame.</summary>
        public void Confetti(int count, float width = 850f, float top = 925f)
        {
            Sprite conf = UiArt.Get("particle_confetti");
            Color[] pal = { UiArt.Gold, UiArt.Teal, UiArt.Orange, new Color32(255, 110, 130, 255), new Color32(190, 150, 255, 255), Color.white };
            for (int i = 0; i < count; i++)
            {
                Color c = pal[Random.Range(0, pal.Length)]; Color c1 = c; c1.a = 0f;
                Spawn(new Vector2(Random.Range(-width * 0.5f, width * 0.5f), top + Random.Range(0f, 120f)),
                    new Vector2(Random.Range(-90f, 90f), Random.Range(-160f, 40f)), Random.Range(2.0f, 3.4f), Random.Range(16f, 30f), Random.Range(12f, 24f),
                    c, c1, conf, -330f, 0.7f, Random.Range(-360f, 360f), false, Random.Range(0f, 360f));
            }
        }

        /// <summary>Expanding ring (landing ripple, turn cue).</summary>
        public void Ring(Vector3 world, Color color, float from = 40f, float to = 220f, float life = 0.55f)
        {
            Color c1 = color; c1.a = 0f;
            Spawn(Local(world), Vector2.zero, life, from, to, color, c1, UiArt.Get("ring_pulse"));
        }

        /// <summary>Soft glow flash.</summary>
        public void Glow(Vector3 world, Color color, float size = 260f, float life = 0.5f)
        {
            Color c1 = color; c1.a = 0f;
            Spawn(Local(world), Vector2.zero, life, size * 0.5f, size, color, c1, UiArt.Get("glow_gold"));
        }

        /// <summary>Text that rises and fades ("+1", "Nice!").</summary>
        public void FloatText(Vector3 world, string text, Color color, float fontSize = 54f, float rise = 90f, float life = 1.0f)
        {
            StartCoroutine(FloatRoutine(Local(world), text, color, fontSize, rise, life));
        }

        private System.Collections.IEnumerator FloatRoutine(Vector2 start, string text, Color color, float fontSize, float rise, float life)
        {
            var go = new GameObject("floatText", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(_layer, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(420f, 90f);
            var tm = go.GetComponent<TextMeshProUGUI>();
            tm.text = text; tm.fontSize = fontSize; tm.fontStyle = FontStyles.Bold; tm.alignment = TextAlignmentOptions.Center;
            tm.color = color; tm.raycastTarget = false; tm.textWrappingMode = TextWrappingModes.NoWrap;
            if (UiFont.Number != null) tm.font = UiFont.Number;
            for (float t = 0f; t < life; t += Time.deltaTime)
            {
                float k = t / life;
                rt.anchoredPosition = start + Vector2.up * (rise * Tween.Evaluate(Ease.OutCubic, k));
                rt.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.5f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, (k - 0.15f) / 0.85f));
                Color c = color; c.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f; tm.color = c;
                yield return null;
            }
            Destroy(go);
        }

        public void Shake(float amplitude = 10f, float duration = 0.25f)
        {
            if (_shakeTarget == null) return;
            if (!_homeSet) { _shakeHome = _shakeTarget.anchoredPosition; _homeSet = true; }
            _shakeAmp = amplitude; _shakeTime = duration; _shakeLeft = duration;
        }

        // ------------------------------------------------------------ simulation

        private void Apply(int i)
        {
            P p = _p[i];
            float k = Mathf.Clamp01(p.age / p.life);
            Image im = _img[i];
            var rt = (RectTransform)im.transform;
            rt.anchoredPosition = p.pos;
            float s = Mathf.Lerp(p.size0, p.size1, k);
            if (p.twinkle) s *= 0.65f + 0.35f * Mathf.Sin(p.age * 18f + i);
            rt.sizeDelta = new Vector2(s, s);
            rt.localRotation = Quaternion.Euler(0f, 0f, p.rot);
            im.color = Color.Lerp(p.c0, p.c1, k * k);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < PoolSize; i++)
            {
                if (!_p[i].alive) continue;
                ref P p = ref _p[i];
                p.age += dt;
                if (p.age >= p.life) { p.alive = false; _img[i].gameObject.SetActive(false); continue; }
                p.vel.y += p.gravity * dt;
                p.vel *= Mathf.Max(0f, 1f - p.drag * dt);
                p.pos += p.vel * dt;
                p.rot += p.rotVel * dt;
                Apply(i);
            }
            if (_shakeLeft > 0f && _shakeTarget != null)
            {
                _shakeLeft -= dt;
                float k = Mathf.Clamp01(_shakeLeft / _shakeTime);
                _shakeTarget.anchoredPosition = _shakeHome + Random.insideUnitCircle * (_shakeAmp * k);
                if (_shakeLeft <= 0f) _shakeTarget.anchoredPosition = _shakeHome;
            }
        }
    }
}
