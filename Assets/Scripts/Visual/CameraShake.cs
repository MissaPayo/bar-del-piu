using UnityEngine;

namespace BarDelPiu.Visual
{
    /// <summary>
    /// Sacudida de cámara en píxeles enteros (respeta el pixel perfect). El desplazamiento se suma
    /// y se quita cada frame, así no interfiere con paneos de cámara hechos por otros scripts.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        [Tooltip("Permite desactivar la sacudida (opción de accesibilidad).")]
        public bool enabledByPlayer = true;

        Vector3 _offset;
        float _until, _duration;
        int _pixels;

        public void Shake(int pixels, float duration)
        {
            if (!enabledByPlayer || !Core.GameSettings.ScreenShake) return;
            if (pixels < _pixels && Time.unscaledTime < _until) return;
            _pixels = pixels;
            _duration = duration;
            _until = Time.unscaledTime + duration;
        }

        void LateUpdate()
        {
            transform.localPosition -= _offset;
            _offset = Vector3.zero;
            if (Time.unscaledTime >= _until)
            {
                _pixels = 0;
                return;
            }
            float left = (_until - Time.unscaledTime) / _duration;
            int p = Mathf.Max(1, Mathf.RoundToInt(_pixels * left));
            _offset = new Vector3(Random.Range(-p, p + 1), Random.Range(-p, p + 1), 0f) * Px.Unit;
            transform.localPosition += _offset;
        }
    }
}
