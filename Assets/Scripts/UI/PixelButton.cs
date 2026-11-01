using System;
using System.Collections.Generic;
using BarDelPiu.Audio;
using BarDelPiu.Visual;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace BarDelPiu.UI
{
    /// <summary>
    /// Botón pixel art en el mundo (funciona con mouse, toque y teclado).
    /// Al presionarlo baja 2 px como una tecla física; suena al pasar y al hacer clic.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class PixelButton : MonoBehaviour
    {
        public SpriteRenderer background;
        public PixelText label;
        public Sprite normal;
        public Sprite pressed;
        public Color labelColor = BarPalette.Ink;
        public Color focusTint = new Color(1f, 1f, 1f);
        public Color idleTint = new Color(0.86f, 0.84f, 0.8f);
        public bool interactable = true;
        public Sfx clickSound = Sfx.UiClick;
        public UnityEvent onClick = new UnityEvent();

        public event Action Clicked;

        static readonly List<PixelButton> Active = new List<PixelButton>();
        static PixelButton _focused;

        BoxCollider2D _collider;
        Vector3 _labelHome;
        bool _pressedInside, _hover;

        public bool IsFocused => _focused == this;

        /// <summary>Crea un botón por código. size en píxeles.</summary>
        public static PixelButton Create(Transform parent, UiSkin skin, string text, Vector2Int size, Vector3 localPos,
            Action onClick, int sortingOrder = SortOrder.Ui + 10)
        {
            var go = new GameObject("Btn_" + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var b = go.AddComponent<PixelButton>();
            b.normal = skin.buttonNormal;
            b.pressed = skin.buttonPressed;

            var bg = new GameObject("Bg").AddComponent<SpriteRenderer>();
            bg.transform.SetParent(go.transform, false);
            bg.sprite = skin.buttonNormal;
            bg.drawMode = SpriteDrawMode.Sliced;
            bg.size = (Vector2)size * Px.Unit;
            bg.sortingOrder = sortingOrder;
            bg.sharedMaterial = skin.unlit;
            b.background = bg;

            var lt = new GameObject("Label").AddComponent<PixelText>();
            lt.transform.SetParent(go.transform, false);
            // El cuerpo de la letra ocupa las filas 1..7 de la celda: -3 px la centra en la cara del botón
            lt.transform.localPosition = new Vector3(0f, -3 * Px.Unit, 0f);
            lt.font = skin.font;
            lt.material = skin.unlit;
            lt.text = text;
            lt.shadow = false;
            lt.color = BarPalette.Ink;
            lt.sortingOrder = sortingOrder + 2;
            b.label = lt;
            b._labelHome = lt.transform.localPosition; // Awake corrió antes de crear la etiqueta

            var col = go.GetComponent<BoxCollider2D>();
            col.size = (Vector2)size * Px.Unit + new Vector2(0.1f, 0.1f);
            col.isTrigger = true;
            if (onClick != null) b.Clicked += onClick;
            return b;
        }

        void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
            if (label) _labelHome = label.transform.localPosition;
        }

        void OnEnable() => Active.Add(this);

        void OnDisable()
        {
            Active.Remove(this);
            if (_focused == this) _focused = null;
            SetPressedLook(false);
        }

        public void SetText(string text)
        {
            if (label) label.Set(text);
        }

        public void Focus()
        {
            if (_focused == this) return;
            _focused = this;
            AudioManager.Play(Sfx.UiHover);
        }

        public void Press()
        {
            if (!interactable) return;
            AudioManager.Play(clickSound);
            PixelTween.Punch(this, transform, 0.12f, 0.15f);
            onClick?.Invoke();
            Clicked?.Invoke();
        }

        void Update()
        {
            bool inside = interactable && PixelPointer.HasPointer && _collider.OverlapPoint(PixelPointer.World) &&
                          (background == null || background.sortingOrder >= SortOrder.Dialogue || !DialogueBox.IsBlocking(PixelPointer.World));
            if (inside && !_hover && !PixelPointer.IsTouch) Focus();
            _hover = inside;

            if (PixelPointer.Pressed && inside) _pressedInside = true;
            bool held = _pressedInside && PixelPointer.Down && inside;
            if (PixelPointer.Released)
            {
                if (_pressedInside && inside) Press();
                _pressedInside = false;
            }
            SetPressedLook(held);

            if (background)
                background.color = !interactable ? new Color(0.5f, 0.45f, 0.42f) : (IsFocused || _hover) ? focusTint : idleTint;
            if (label) label.SetColor(labelColor);
        }

        void SetPressedLook(bool down)
        {
            if (background) background.sprite = down && pressed ? pressed : normal;
            if (label) label.transform.localPosition = _labelHome + (down ? Vector3.down * 2 * Px.Unit : Vector3.zero);
        }

        // ------------------------------------------------------------------ teclado

        /// <summary>Navegación con flechas/WASD + Enter/Espacio entre los botones visibles.</summary>
        public static void UpdateKeyboardNavigation()
        {
            var kb = Keyboard.current;
            if (kb == null || Active.Count == 0) return;
            Vector2 dir = Vector2.zero;
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) dir = Vector2.up;
            else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) dir = Vector2.down;
            else if (kb.leftArrowKey.wasPressedThisFrame) dir = Vector2.left;
            else if (kb.rightArrowKey.wasPressedThisFrame) dir = Vector2.right;

            if (dir != Vector2.zero)
            {
                if (_focused == null || !_focused.isActiveAndEnabled)
                {
                    FirstInteractable()?.Focus();
                    return;
                }
                PixelButton best = null;
                float bestScore = float.MaxValue;
                Vector2 from = _focused.transform.position;
                foreach (var b in Active)
                {
                    if (b == _focused || !b.interactable) continue;
                    Vector2 d = (Vector2)b.transform.position - from;
                    float along = Vector2.Dot(d, dir);
                    if (along <= 0.01f) continue;
                    float score = along + Mathf.Abs(Vector2.Dot(d, new Vector2(-dir.y, dir.x))) * 2f;
                    if (score < bestScore) { bestScore = score; best = b; }
                }
                best?.Focus();
            }
            if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && _focused != null && _focused.isActiveAndEnabled)
                _focused.Press();
        }

        static PixelButton FirstInteractable()
        {
            PixelButton top = null;
            foreach (var b in Active)
                if (b.interactable && (top == null || b.transform.position.y > top.transform.position.y)) top = b;
            return top;
        }
    }
}
