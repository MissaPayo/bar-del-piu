using BarDelPiu.Audio;
using BarDelPiu.Core;
using BarDelPiu.UI;
using BarDelPiu.Visual;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace BarDelPiu.Screens
{
    /// <summary>
    /// Pantalla de espera: el Bar del Piu de noche visto desde la calle, con su logo
    /// y autos que pasan en un ciclo de 20 s. Cualquier toque o tecla vuelve al menú.
    /// Con el modo Hardcore activo, el bar se ve punk: grafitis, toldos rojo y negro,
    /// fiesta en las vitrinas, neón HARDCORE, luz roja, motos y música pesada.
    /// </summary>
    public class IdleScreenController : MonoBehaviour
    {
        public PixelText prompt;
        public SpriteRenderer logo;
        [Tooltip("Ignora toques durante el primer instante para que el último toque no la cierre.")]
        public float inputDelay = 0.6f;

        [Header("Versión Hardcore")]
        public SpriteRenderer hardcoreDecor;
        public NeonFlicker neon;
        public Sprite neonHardcoreOn;
        public Sprite neonHardcoreOff;
        public Light2D nightLight;
        public Light2D[] warmLights;
        public CarLoop cars;
        public Sprite[] motoFrames;

        float _t;
        bool _leaving;

        void Start()
        {
            bool hardcore = BarMischief.AnyLampBroken;
            AudioManager.PlayMusic(hardcore ? MusicTrack.Hardcore : MusicTrack.Menu);
            AudioManager.SetMusicIntensity(0f);
            AudioManager.SetAmbience(!hardcore); // en Hardcore manda la música pesada, sin murmullo encima
            if (hardcoreDecor) hardcoreDecor.enabled = hardcore;
            if (hardcore) ApplyHardcore();
        }

        void ApplyHardcore()
        {
            if (neon && neonHardcoreOn)
            {
                neon.on = neonHardcoreOn;
                neon.off = neonHardcoreOff;
                neon.sign.sprite = neonHardcoreOn;
                if (neon.glow) neon.glow.color = new Color(1f, 0.18f, 0.24f);
            }
            if (nightLight) nightLight.color = new Color(0.85f, 0.45f, 0.75f);
            if (warmLights != null)
                foreach (var l in warmLights)
                    if (l) l.color = new Color(1f, 0.3f, 0.45f);
            // la mitad del tráfico pasa a ser motos con pilotos punk
            if (cars != null && motoFrames != null && motoFrames.Length > 0)
                for (int i = 0; i < cars.cars.Length; i += 2)
                {
                    cars.cars[i].frames = motoFrames;
                    cars.cars[i].renderer.sprite = motoFrames[0];
                }
            if (prompt) prompt.Set("TOCA LA PANTALLA... SI TE ATREVES");
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (prompt) prompt.SetAlpha(Mathf.Repeat(_t, 1.4f) < 0.9f ? 1f : 0f);

            // El logo «respira» cada pocos segundos para llamar la atención
            if (logo && Mathf.Repeat(_t, 4f) < Time.deltaTime) PixelTween.Punch(this, logo.transform, 0.12f, 0.3f);

            if (_leaving || _t < inputDelay) return;
            var kb = Keyboard.current;
            bool pressed = PixelPointer.Pressed || (kb != null && kb.anyKey.wasPressedThisFrame);
            if (pressed)
            {
                _leaving = true;
                AudioManager.Play(Sfx.UiClick);
                SceneFlow.Load(GameSession.MenuScene);
            }
        }
    }
}
