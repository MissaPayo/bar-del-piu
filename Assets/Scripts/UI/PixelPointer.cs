using UnityEngine;
using UnityEngine.InputSystem;

namespace BarDelPiu.UI
{
    /// <summary>Mouse o primer toque, unificado y en coordenadas de mundo. Se lee una vez por frame.</summary>
    public static class PixelPointer
    {
        static int _frame = -1;
        static Vector3 _world;
        static bool _down, _pressed, _released, _hasPointer, _isTouch;

        public static Vector3 World { get { Refresh(); return _world; } }
        public static bool Down { get { Refresh(); return _down; } }
        public static bool Pressed { get { Refresh(); return _pressed; } }
        public static bool Released { get { Refresh(); return _released; } }
        public static bool HasPointer { get { Refresh(); return _hasPointer; } }
        public static bool IsTouch { get { Refresh(); return _isTouch; } }

        static void Refresh()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            _pressed = _released = _down = _isTouch = false;
            _hasPointer = false;
            Vector2 screen = default;

            var touch = Touchscreen.current;
            if (touch != null && (touch.primaryTouch.press.isPressed || touch.primaryTouch.press.wasReleasedThisFrame))
            {
                var p = touch.primaryTouch;
                screen = p.position.ReadValue();
                _down = p.press.isPressed;
                _pressed = p.press.wasPressedThisFrame;
                _released = p.press.wasReleasedThisFrame;
                _hasPointer = _isTouch = true;
            }
            else if (Mouse.current != null)
            {
                var m = Mouse.current;
                screen = m.position.ReadValue();
                _down = m.leftButton.isPressed;
                _pressed = m.leftButton.wasPressedThisFrame;
                _released = m.leftButton.wasReleasedThisFrame;
                _hasPointer = true;
            }

            var cam = Camera.main;
            if (cam != null && _hasPointer)
            {
                _world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
                _world.z = 0f;
            }
        }
    }
}
