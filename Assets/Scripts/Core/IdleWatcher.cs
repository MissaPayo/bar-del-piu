using UnityEngine;
using UnityEngine.InputSystem;

namespace BarDelPiu.Core
{
    /// <summary>
    /// Si nadie toca nada durante «timeout» segundos, abre la pantalla de espera.
    /// Cualquier toque, clic, tecla o movimiento del mouse reinicia la cuenta.
    /// </summary>
    public class IdleWatcher : MonoBehaviour
    {
        public float timeout = 30f;
        [Tooltip("Pausa la cuenta (por ejemplo durante créditos o la sorpresa de cumpleaños).")]
        public bool suspended;

        float _idle;

        void Update()
        {
            if (suspended || SceneFlow.IsBusy || AnyInput())
            {
                _idle = 0f;
                return;
            }
            _idle += Time.unscaledDeltaTime;
            if (_idle >= timeout)
            {
                _idle = 0f;
                SceneFlow.Load(GameSession.IdleScene);
            }
        }

        public static bool AnyInput()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.isPressed) return true;
            var m = Mouse.current;
            if (m != null && (m.leftButton.isPressed || m.rightButton.isPressed || m.delta.ReadValue().sqrMagnitude > 0.5f)) return true;
            var t = Touchscreen.current;
            return t != null && t.primaryTouch.press.isPressed;
        }
    }
}
