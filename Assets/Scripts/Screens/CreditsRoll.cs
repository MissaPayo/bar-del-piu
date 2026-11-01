using System;
using System.Collections.Generic;
using BarDelPiu.Audio;
using BarDelPiu.UI;
using BarDelPiu.Visual;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BarDelPiu.Screens
{
    /// <summary>Créditos que suben por la pantalla sobre el bar oscurecido. Tocar o Esc para volver.</summary>
    public class CreditsRoll : MonoBehaviour
    {
        struct Line
        {
            public string text;
            public Color color;
            public int scale;
            public int gapAfter;
            public bool hearts;
        }

        const float Speed = 1.4f; // unidades por segundo (≈22 px/s)

        UiSkin _skin;
        Action _onClose;
        Transform _content;
        SpriteRenderer _dim;
        float _height, _scroll;
        PixelButton _back;
        readonly List<SpriteRenderer> _hearts = new List<SpriteRenderer>();

        public static CreditsRoll Create(Transform parent, UiSkin skin, Action onClose)
        {
            var go = new GameObject("Credits");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<CreditsRoll>();
            c._skin = skin;
            c._onClose = onClose;
            c.Build();
            go.SetActive(false);
            return c;
        }

        void Build()
        {
            float u = Px.Unit;
            _dim = new GameObject("Dim").AddComponent<SpriteRenderer>();
            _dim.transform.SetParent(transform, false);
            _dim.sprite = _skin.pixel;
            _dim.color = new Color(0.1f, 0.08f, 0.06f, 0.94f);
            _dim.transform.position = new Vector3(-14f, -8f, 0);
            _dim.transform.localScale = new Vector3(28 * 16, 16 * 16, 1);
            _dim.sortingOrder = SortOrder.Overlay + 30;
            _dim.sharedMaterial = _skin.unlit;

            _content = new GameObject("Content").transform;
            _content.SetParent(transform, false);

            var lines = new[]
            {
                new Line { text = "BAR DEL PIU", color = BarPalette.AmberLight, scale = 2, gapAfter = 4 },
                new Line { text = "UNA NOCHE EN LA BARRA", color = BarPalette.Cream, scale = 1, gapAfter = 30 },
                new Line { text = "DESARROLLADO POR", color = BarPalette.GreenHighlight, scale = 1, gapAfter = 6 },
                new Line { text = "MISSAEL FONTECILLA", color = BarPalette.Cream, scale = 2, gapAfter = 4 },
                new Line { text = "CEO DE UNRAVEL VISION", color = BarPalette.AmberLight, scale = 1, gapAfter = 12 },
                new Line { text = "PAULA CÁRCAMO", color = BarPalette.Cream, scale = 2, gapAfter = 40 },
                new Line { text = "PARA", color = BarPalette.GreenHighlight, scale = 1, gapAfter = 6 },
                new Line { text = "PIPE Y NATA", color = BarPalette.Neon, scale = 2, gapAfter = 8, hearts = true },
                new Line { text = "¡SALUD!", color = BarPalette.AmberLight, scale = 1, gapAfter = 0 },
            };
            float y = 0;
            foreach (var l in lines)
            {
                var t = new GameObject("Line").AddComponent<PixelText>();
                t.transform.SetParent(_content, false);
                // El origen del texto es la base de la letra: se baja una altura de línea para no pisar la anterior
                float baseY = -(y + _skin.font.height * l.scale);
                t.transform.localPosition = new Vector3(0, baseY * u, 0);
                t.font = _skin.font;
                t.material = _skin.unlit;
                t.text = l.text;
                t.color = l.color;
                t.scale = l.scale;
                t.alignment = PixelText.Align.Center;
                t.sortingOrder = SortOrder.Overlay + 32;
                if (l.hearts)
                {
                    int half = _skin.font.Measure(l.text) * l.scale / 2 + 10;
                    foreach (int side in new[] { -1, 1 })
                    {
                        var h = new GameObject("Heart").AddComponent<SpriteRenderer>();
                        h.transform.SetParent(_content, false);
                        h.transform.localPosition = new Vector3(side * half * u, (baseY + 9) * u, 0);
                        h.sprite = _skin.heart;
                        h.sortingOrder = SortOrder.Overlay + 33;
                        h.sharedMaterial = _skin.unlit;
                        _hearts.Add(h);
                    }
                }
                y += _skin.font.height * l.scale + l.gapAfter;
            }
            _height = y * u;

            _back = PixelButton.Create(transform, _skin, "VOLVER", new Vector2Int(64, 16), new Vector3(0, -76f / 16f, 0),
                () => _onClose?.Invoke(), SortOrder.Overlay + 40);
            _back.clickSound = Sfx.UiBack;
        }

        const float StartY = -6.5f; // primera línea justo bajo el borde inferior

        public void Open()
        {
            gameObject.SetActive(true);
            _scroll = StartY;
            _content.localPosition = new Vector3(0, StartY, 0);
            _back.Focus();
        }

        void Update()
        {
            // La posición real se acumula aparte: si se redondeara al píxel cada cuadro, nunca avanzaría
            _scroll += Speed * Time.deltaTime;
            if (_scroll > _height + 6.5f) _scroll = StartY; // salió por arriba: vuelve a empezar
            _content.localPosition = new Vector3(0, Px.Snap(_scroll), 0);

            float beat = Mathf.Repeat(Time.time * 1.6f, 1f);
            foreach (var h in _hearts)
                h.transform.localScale = Vector3.one * (beat < 0.15f ? 1.3f : 1f);

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) _onClose?.Invoke();
        }
    }
}
