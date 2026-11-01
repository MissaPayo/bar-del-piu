using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BarDelPiu.Visual
{
    /// <summary>Letrero de neón: parpadeos breves y aleatorios, con su luz 2D sincronizada.</summary>
    public class NeonFlicker : MonoBehaviour
    {
        public SpriteRenderer sign;
        public Sprite on;
        public Sprite off;
        public Light2D glow;
        public Vector2 secondsBetweenFlickers = new Vector2(4f, 11f);
        [Tooltip("Desactivar para jugadores sensibles a destellos.")]
        public bool flickerEnabled = true;

        float _next, _baseIntensity;
        int _blinksLeft;
        float _blinkUntil;
        bool _isOn = true;

        void Start()
        {
            if (glow) _baseIntensity = glow.intensity;
            Schedule();
        }

        void Schedule() => _next = Time.time + Random.Range(secondsBetweenFlickers.x, secondsBetweenFlickers.y);

        void Update()
        {
            if (!flickerEnabled || !Core.GameSettings.NeonFlicker)
            {
                Set(true);
                return;
            }
            if (_blinksLeft == 0 && Time.time >= _next)
                _blinksLeft = Random.Range(2, 6);
            if (_blinksLeft > 0 && Time.time >= _blinkUntil)
            {
                Set(!_isOn);
                _blinkUntil = Time.time + Random.Range(0.04f, 0.12f);
                if (_isOn && --_blinksLeft == 0) Schedule();
            }
        }

        void Set(bool value)
        {
            _isOn = value;
            if (sign) sign.sprite = value ? on : off;
            if (glow) glow.intensity = value ? _baseIntensity : _baseIntensity * 0.15f;
        }
    }
}
