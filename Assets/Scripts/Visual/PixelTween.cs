using System;
using System.Collections;
using UnityEngine;

namespace BarDelPiu.Visual
{
    /// <summary>
    /// Tweens mínimos con corrutinas (sin dependencias externas). Se ejecutan sobre el
    /// MonoBehaviour que se pasa como «owner», así se detienen solos si el objeto se destruye.
    /// </summary>
    public static class PixelTween
    {
        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float EaseInCubic(float t) => t * t * t;

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static Coroutine Value(MonoBehaviour owner, float duration, Action<float> step, Action done = null, bool unscaled = false)
        {
            if (owner == null || !owner.isActiveAndEnabled) return null;
            return owner.StartCoroutine(Run(duration, step, done, unscaled));
        }

        static IEnumerator Run(float duration, Action<float> step, Action done, bool unscaled)
        {
            float t = 0f;
            while (t < duration)
            {
                step(t / duration);
                yield return null;
                t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            }
            step(1f);
            done?.Invoke();
        }

        /// <summary>Golpe de escala elástico (squash & stretch simple).</summary>
        public static Coroutine Punch(MonoBehaviour owner, Transform target, float amount = 0.2f, float duration = 0.18f)
        {
            Vector3 baseScale = target.localScale;
            return Value(owner, duration, t =>
            {
                float s = Mathf.Sin(t * Mathf.PI) * amount * (1f - t);
                target.localScale = new Vector3(baseScale.x * (1f + s), baseScale.y * (1f - s * 0.6f), baseScale.z);
            }, () => target.localScale = baseScale);
        }

        public static Coroutine MoveLocal(MonoBehaviour owner, Transform target, Vector3 to, float duration,
            Func<float, float> ease = null, Action done = null)
        {
            Vector3 from = target.localPosition;
            ease ??= EaseOutCubic;
            return Value(owner, duration, t => target.localPosition = Vector3.LerpUnclamped(from, to, ease(t)), done);
        }
    }
}
