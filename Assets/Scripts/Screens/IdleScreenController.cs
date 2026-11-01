using BarDelPiu.Audio;
using BarDelPiu.Core;
using BarDelPiu.UI;
using BarDelPiu.Visual;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BarDelPiu.Screens
{
    /// <summary>
    /// Pantalla de espera: el Bar del Piu de noche visto desde la calle, con su logo
    /// y autos que pasan en un ciclo de 20 s. Cualquier toque o tecla vuelve al menú.
    /// </summary>
    public class IdleScreenController : MonoBehaviour
    {
        public PixelText prompt;
        public SpriteRenderer logo;
        [Tooltip("Ignora toques durante el primer instante para que el último toque no la cierre.")]
        public float inputDelay = 0.6f;

        float _t;
        bool _leaving;

        void Start()
        {
            AudioManager.PlayMusic(MusicTrack.Menu);
            AudioManager.SetMusicIntensity(0f);
            AudioManager.SetAmbience(true);
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
