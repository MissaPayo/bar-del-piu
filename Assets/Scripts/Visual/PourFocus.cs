using System.Collections;
using UnityEngine;

namespace BarDelPiu.Visual
{
    /// <summary>
    /// Pantalla de concentración: mientras se llena la jarra, una capa oscura tapa a los clientes
    /// y aparece una copia grande de la jarra (x3) lejos del dedo, para ver bien el llenado en celular.
    /// BeerGlassVisual la muestra al servir y la oculta al soltar.
    /// </summary>
    public class PourFocus : MonoBehaviour
    {
        public SpriteRenderer overlay;
        [Tooltip("Partes de la jarra grande (fondo, líquido, frente).")]
        public SpriteRenderer bigBack;
        public SpriteRenderer bigLiquid;
        public SpriteRenderer bigFront;
        [Tooltip("Tarjeta oscura detrás de la jarra grande: el vidrio se lee bien aunque esté vacío.")]
        public SpriteRenderer bigCard;
        public PixelText hint;
        [Range(0f, 1f)] public float overlayAlpha = 0.94f;
        public float fadeTime = 0.12f;
        [Tooltip("Tiempo que la jarra grande queda visible después de soltar, para ver dónde quedó el nivel.")]
        public float holdAfterRelease = 0.45f;

        float _alpha, _target;
        float _hideAt = -1f;
        Coroutine _punch;

        public bool IsShowing => _target > 0f;

        void Awake() => Apply(0f);

        public void Show()
        {
            _hideAt = -1f;
            if (_target > 0f) return;
            _target = 1f;
            if (bigLiquid) _punch = PixelTween.Punch(this, bigLiquid.transform.parent, 0.06f, 0.15f);
        }

        public void HideSoon() => _hideAt = Time.time + holdAfterRelease;

        public void HideNow()
        {
            _hideAt = -1f;
            _target = 0f;
        }

        void Update()
        {
            if (_hideAt > 0f && Time.time >= _hideAt)
            {
                _hideAt = -1f;
                _target = 0f;
            }
            if (Mathf.Approximately(_alpha, _target)) return;
            _alpha = Mathf.MoveTowards(_alpha, _target, Time.deltaTime / Mathf.Max(0.01f, fadeTime));
            Apply(_alpha);
        }

        void Apply(float a)
        {
            // Pasos de 25 % para un fundido «pixel», sin degradé suave
            float stepped = Mathf.Ceil(a * 4f) / 4f;
            bool visible = stepped > 0f;
            if (overlay)
            {
                overlay.enabled = visible;
                var c = overlay.color;
                c.a = overlayAlpha * stepped;
                overlay.color = c;
            }
            foreach (var sr in new[] { bigCard, bigBack, bigLiquid, bigFront })
                if (sr) sr.enabled = stepped >= 0.5f;
            if (hint)
            {
                hint.enabled = stepped >= 0.5f;
                if (hint.enabled) hint.Rebuild();
                else foreach (var r in hint.GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = false;
            }
        }
    }
}
