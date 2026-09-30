using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tarneeb.Presentation
{
    public enum Ease { Linear, OutCubic, InOutCubic, OutBack, OutQuad, InQuad, OutElastic, InBack }

    /// <summary>Tiny coroutine tween helper. Replaces DOTween for this prototype (see DEVLOG).</summary>
    public static class Tween
    {
        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.OutCubic: { float u = 1f - t; return 1f - u * u * u; }
                case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                case Ease.OutBack: { const float c1 = 1.70158f, c3 = c1 + 1f; float u = t - 1f; return 1f + c3 * u * u * u + c1 * u * u; }
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.InQuad: return t * t;
                case Ease.InBack: { const float c1 = 1.70158f, c3 = c1 + 1f; return c3 * t * t * t - c1 * t * t; }
                case Ease.OutElastic:
                {
                    if (t <= 0f || t >= 1f) return t;
                    const float c4 = 2f * Mathf.PI / 3f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                }
                default: return t;
            }
        }

        /// <summary>World-space move/rotate/scale in one tween. Stops quietly if the object is destroyed.</summary>
        public static IEnumerator Fly(Transform tr, Vector3 worldPos, float rotZ, float scale, float duration, Ease ease = Ease.OutCubic)
        {
            if (tr == null) yield break;
            Vector3 p0 = tr.position;
            float r0 = NormalizeAngle(tr.eulerAngles.z);
            float s0 = tr.localScale.x;
            float r1 = r0 + Mathf.DeltaAngle(r0, rotZ);
            duration = Scaled(duration);
            if (duration <= 0f) { tr.position = worldPos; tr.rotation = Quaternion.Euler(0, 0, rotZ); tr.localScale = Vector3.one * scale; yield break; }
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (tr == null) yield break;
                float k = Evaluate(ease, t / duration);
                tr.position = Vector3.LerpUnclamped(p0, worldPos, k);
                tr.rotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(r0, r1, k));
                tr.localScale = Vector3.one * Mathf.LerpUnclamped(s0, scale, k);
                yield return null;
            }
            if (tr == null) yield break;
            tr.position = worldPos;
            tr.rotation = Quaternion.Euler(0, 0, rotZ);
            tr.localScale = Vector3.one * scale;
        }

        /// <summary>Local-space (anchoredPosition) move/rotate/scale for cards inside a container.</summary>
        public static IEnumerator Local(RectTransform rt, Vector2 anchoredPos, float rotZ, float scale, float duration, Ease ease = Ease.OutCubic)
        {
            if (rt == null) yield break;
            Vector2 p0 = rt.anchoredPosition;
            float r0 = NormalizeAngle(rt.localEulerAngles.z);
            float r1 = r0 + Mathf.DeltaAngle(r0, rotZ);
            float s0 = rt.localScale.x;
            duration = Scaled(duration);
            if (duration <= 0f) { rt.anchoredPosition = anchoredPos; rt.localRotation = Quaternion.Euler(0, 0, rotZ); rt.localScale = Vector3.one * scale; yield break; }
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                float k = Evaluate(ease, t / duration);
                rt.anchoredPosition = Vector2.LerpUnclamped(p0, anchoredPos, k);
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(r0, r1, k));
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(s0, scale, k);
                yield return null;
            }
            if (rt == null) yield break;
            rt.anchoredPosition = anchoredPos;
            rt.localRotation = Quaternion.Euler(0, 0, rotZ);
            rt.localScale = Vector3.one * scale;
        }

        public static IEnumerator Fade(CanvasGroup group, float to, float duration)
        {
            if (group == null) yield break;
            float a0 = group.alpha;
            duration = Scaled(duration);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (group == null) yield break;
                group.alpha = Mathf.Lerp(a0, to, t / duration);
                yield return null;
            }
            if (group != null) group.alpha = to;
        }

        /// <summary>Uniform scale tween on any transform.</summary>
        public static IEnumerator ScaleTo(Transform tr, float from, float to, float duration, Ease ease = Ease.OutCubic)
        {
            if (tr == null) yield break;
            duration = Scaled(duration);
            tr.localScale = Vector3.one * from;
            if (duration <= 0f) { tr.localScale = Vector3.one * to; yield break; }
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (tr == null) yield break;
                tr.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, Evaluate(ease, t / duration));
                yield return null;
            }
            if (tr != null) tr.localScale = Vector3.one * to;
        }

        /// <summary>Scale in from zero with a small overshoot (popups, badges).</summary>
        public static IEnumerator Pop(Transform tr, float duration = 0.32f, float from = 0f) => ScaleTo(tr, from, 1f, duration, Ease.OutBack);

        /// <summary>Quick squash-and-recover (score changes, button presses).</summary>
        public static IEnumerator Punch(Transform tr, float amount = 0.18f, float duration = 0.28f)
        {
            if (tr == null) yield break;
            duration = Scaled(duration);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (tr == null) yield break;
                float k = t / duration;
                tr.localScale = Vector3.one * (1f + amount * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.4f));
                yield return null;
            }
            if (tr != null) tr.localScale = Vector3.one;
        }

        public static IEnumerator FadeGraphic(UnityEngine.UI.Graphic g, float to, float duration)
        {
            if (g == null) yield break;
            Color c = g.color; float a0 = c.a;
            duration = Scaled(duration);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (g == null) yield break;
                c.a = Mathf.Lerp(a0, to, t / duration); g.color = c;
                yield return null;
            }
            if (g != null) { c.a = to; g.color = c; }
        }

        public static IEnumerator Wait(float seconds)
        {
            seconds = Scaled(seconds);
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }

        /// <summary>Runs routines concurrently and completes when all have finished.</summary>
        public static IEnumerator All(MonoBehaviour host, List<IEnumerator> routines)
        {
            int pending = routines.Count;
            for (int i = 0; i < routines.Count; i++) host.StartCoroutine(Track(routines[i], () => pending--));
            while (pending > 0) yield return null;
        }

        private static IEnumerator Track(IEnumerator routine, Action done)
        {
            yield return routine;
            done();
        }

        public static float SpeedMultiplier = 1f;
        private static float Scaled(float seconds) => seconds / Mathf.Max(0.1f, SpeedMultiplier);

        private static float NormalizeAngle(float a) => a > 180f ? a - 360f : a;
    }
}
