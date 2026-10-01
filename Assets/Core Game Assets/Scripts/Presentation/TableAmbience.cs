using UnityEngine;

namespace Tarneeb.Presentation
{
    /// <summary>Quiet life on the table: the central medallion breathes, the jewels catch the light now and then, gold dust drifts over the felt.</summary>
    public sealed class TableAmbience : MonoBehaviour
    {
        private CanvasGroup _medallion;
        private Transform _jewels;
        private float _clock, _nextGlint = 2.5f, _nextDust = 1f;

        private void Awake()
        {
            Transform med = null;
            foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t.name == "Central medallion / centered") { med = t; break; }
            if (med != null) _medallion = med.GetComponent<CanvasGroup>();
            _jewels = transform.Find("Jewels");
        }

        private void Update()
        {
            _clock += Time.deltaTime;
            if (_medallion != null) _medallion.alpha = 0.72f + 0.28f * (0.5f + 0.5f * Mathf.Sin(_clock * 0.9f));
            UiFx fx = UiFx.Instance;
            if (fx == null) return;
            if (_jewels != null && _clock > _nextGlint && _jewels.childCount > 0)
            {
                _nextGlint = _clock + Random.Range(2.5f, 5.5f);
                Transform j = _jewels.GetChild(Random.Range(0, _jewels.childCount));
                fx.Sparkles(j.position, 2, 12f, Color.white, 0.8f, 30f);
                StartCoroutine(Tween.Punch(j, 0.25f, 0.4f));
            }
            if (_clock > _nextDust)
            {
                _nextDust = _clock + Random.Range(0.7f, 1.6f);
                Transform table = transform.Find("Table");
                if (table != null)
                {
                    Vector3 p = table.TransformPoint(new Vector3(Random.Range(-230f, 230f), Random.Range(-320f, 300f), 0f));
                    fx.Sparkles(p, 1, 3f, new Color(1f, 0.93f, 0.7f, 0.55f), 2.4f, 20f);
                }
            }
        }
    }
}
