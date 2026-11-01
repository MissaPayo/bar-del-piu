using System;
using System.Collections.Generic;
using BarDelPiu.Audio;
using BarDelPiu.Leaderboard;
using BarDelPiu.Visual;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BarDelPiu.UI
{
    /// <summary>
    /// Ingreso de apodo libre (hasta 10 caracteres) con teclado en pantalla pixel art,
    /// que funciona igual en celular y en PC; en PC también se puede escribir con el teclado físico.
    /// </summary>
    public class NicknameEntry : MonoBehaviour
    {
        public UiSkin skin;

        static readonly string[] Rows =
        {
            "1234567890",
            "QWERTYUIOP",
            "ASDFGHJKLÑ",
            "ZXCVBNM-",
        };

        const int KeyW = 15, KeyH = 14, Gap = 2;

        PixelText _field, _hint;
        SpriteRenderer _caret;
        string _value = "";
        Action<string> _onDone;
        readonly List<PixelButton> _keys = new List<PixelButton>();
        bool _built, _open;
        float _blink;

        public bool IsOpen => _open;

        public void Open(string initial, Action<string> onDone)
        {
            Build();
            _value = NameFilter.Clean(initial);
            _onDone = onDone;
            _open = true;
            gameObject.SetActive(true);
            if (Keyboard.current != null)
            {
                Keyboard.current.onTextInput -= OnText;
                Keyboard.current.onTextInput += OnText;
            }
            _hint.Set("ESCRIBE TU APODO");
            _hint.SetColor(BarPalette.CreamDark);
            Refresh();
        }

        public void Close()
        {
            _open = false;
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnText;
            gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnText;
        }

        void Build()
        {
            if (_built) return;
            _built = true;
            float u = Px.Unit;
            int width = 10 * (KeyW + Gap) + 10;
            int height = 132;

            var panel = new GameObject("Panel").AddComponent<SpriteRenderer>();
            panel.transform.SetParent(transform, false);
            panel.sprite = skin.panelDark;
            panel.drawMode = SpriteDrawMode.Sliced;
            panel.size = new Vector2(width, height) * u;
            panel.transform.localPosition = new Vector3(0, -height / 2f * u, 0);
            panel.sortingOrder = SortOrder.Overlay + 10;
            panel.sharedMaterial = skin.unlit;

            _hint = Text("Hint", new Vector3(0, -14 * u, 0), "", BarPalette.CreamDark);

            var field = new GameObject("Field").AddComponent<SpriteRenderer>();
            field.transform.SetParent(transform, false);
            field.sprite = skin.panelCream;
            field.drawMode = SpriteDrawMode.Sliced;
            field.size = new Vector2(100, 16) * u;
            field.transform.localPosition = new Vector3(0, -28 * u, 0);
            field.sortingOrder = SortOrder.Overlay + 11;
            field.sharedMaterial = skin.unlit;
            _field = Text("Value", new Vector3(0, -32 * u, 0), "", BarPalette.Ink);
            _field.shadow = false;

            _caret = new GameObject("Caret").AddComponent<SpriteRenderer>();
            _caret.transform.SetParent(transform, false);
            _caret.sprite = skin.pixel;
            _caret.color = BarPalette.Ink;
            _caret.transform.localScale = new Vector3(4, 1, 1);
            _caret.sortingOrder = SortOrder.Overlay + 14;
            _caret.sharedMaterial = skin.unlit;

            for (int r = 0; r < Rows.Length; r++)
            {
                string row = Rows[r];
                int rowW = row.Length * (KeyW + Gap) - Gap;
                for (int i = 0; i < row.Length; i++)
                {
                    char c = row[i];
                    float x = -rowW / 2f + KeyW / 2f + i * (KeyW + Gap);
                    float y = -(48 + r * (KeyH + Gap));
                    var k = PixelButton.Create(transform, skin, c.ToString(), new Vector2Int(KeyW, KeyH),
                        new Vector3(Mathf.Round(x) * u, y * u, 0), () => Type(c), SortOrder.Overlay + 12);
                    k.clickSound = Sfx.UiClick;
                    _keys.Add(k);
                }
            }
            float yb = -(48 + Rows.Length * (KeyH + Gap));
            var del = PixelButton.Create(transform, skin, "BORRAR", new Vector2Int(48, KeyH), new Vector3(-58 * u, yb * u, 0),
                Backspace, SortOrder.Overlay + 12);
            del.clickSound = Sfx.UiBack;
            PixelButton.Create(transform, skin, "ESPACIO", new Vector2Int(48, KeyH), new Vector3(-6 * u, yb * u, 0),
                () => Type(' '), SortOrder.Overlay + 12);
            var ok = PixelButton.Create(transform, skin, "LISTO", new Vector2Int(44, KeyH), new Vector3(52 * u, yb * u, 0),
                Submit, SortOrder.Overlay + 12);
            ok.labelColor = BarPalette.GreenDark;
        }

        PixelText Text(string n, Vector3 pos, string text, Color c)
        {
            var t = new GameObject(n).AddComponent<PixelText>();
            t.transform.SetParent(transform, false);
            t.transform.localPosition = pos;
            t.font = skin.font;
            t.material = skin.unlit;
            t.text = text;
            t.color = c;
            t.sortingOrder = SortOrder.Overlay + 13;
            return t;
        }

        void OnText(char c)
        {
            if (!_open) return;
            // Borrar y Enter se leen en Update (no todos los navegadores los envían como texto)
            if (!char.IsControl(c)) Type(c);
        }

        void Type(char c)
        {
            string next = NameFilter.Clean(_value + c);
            if (next.Length == _value.Length && c != ' ') return;
            if (c == ' ' && (_value.Length == 0 || _value.EndsWith(" ") || _value.Length >= NameFilter.MaxLength)) return;
            _value = c == ' ' ? _value + " " : next;
            Refresh();
            PixelTween.Punch(this, _field.transform, 0.1f, 0.1f);
        }

        void Backspace()
        {
            if (_value.Length == 0) return;
            _value = _value.Substring(0, _value.Length - 1);
            Refresh();
        }

        void Submit()
        {
            string clean = NameFilter.Clean(_value);
            if (!NameFilter.IsAcceptable(clean))
            {
                _hint.Set(clean.Length < 2 ? "MÍNIMO 2 LETRAS" : "ESE APODO NO SE PUEDE");
                _hint.SetColor(BarPalette.Red);
                AudioManager.Play(Sfx.Error);
                PixelTween.Punch(this, _field.transform, 0.3f, 0.2f);
                return;
            }
            AudioManager.Play(Sfx.CountdownGo);
            var cb = _onDone;
            Close();
            cb?.Invoke(clean);
        }

        void Refresh()
        {
            _field.Set(_value);
            _field.Rebuild();
        }

        void Update()
        {
            if (!_open) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.backspaceKey.wasPressedThisFrame) Backspace();
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Submit();
            }

            // Cursor parpadeante después del texto
            _blink += Time.deltaTime;
            int w = skin.font.Measure(_value);
            _caret.transform.localPosition = new Vector3(Mathf.Floor(w / 2f + 2) * Px.Unit, -33 * Px.Unit, 0);
            _caret.enabled = _value.Length < NameFilter.MaxLength && Mathf.Repeat(_blink, 1f) < 0.55f;
        }
    }
}
