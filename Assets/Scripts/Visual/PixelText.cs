using System.Collections.Generic;
using UnityEngine;

namespace BarDelPiu.Visual
{
    /// <summary>
    /// Texto pixel perfect hecho con un SpriteRenderer por letra. Sirve para HUD, textos flotantes
    /// y diálogos. Escala entera (1, 2, 3...) para no deformar los píxeles.
    /// </summary>
    [ExecuteAlways]
    public class PixelText : MonoBehaviour
    {
        public enum Align { Left, Center, Right }

        public PixelFont font;
        [TextArea] public string text = "BAR DEL PIU";
        public Color color = Color.white;
        [Min(1)] public int scale = 1;
        public Align alignment = Align.Center;
        [Tooltip("Ancho máximo en píxeles antes de cortar línea (0 = sin límite).")]
        public int maxWidth;
        public bool shadow = true;
        public Color shadowColor = new Color(0.1f, 0.08f, 0.06f, 1f);
        public int sortingOrder = SortOrder.UiText;
        public Material material;
        [Tooltip("Cantidad de caracteres visibles (efecto máquina de escribir). -1 = todos.")]
        public int visibleCharacters = -1;

        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        bool _dirty = true;
        string _builtText;

        public int VisibleLength { get; private set; }

        public void Set(string value)
        {
            if (text == value) return;
            text = value;
            _dirty = true;
        }

        public void SetColor(Color c)
        {
            color = c;
            ApplyColors();
        }

        public void SetAlpha(float a)
        {
            color.a = a;
            shadowColor.a = a;
            ApplyColors();
        }

        public void MarkDirty() => _dirty = true;

        void OnValidate() => _dirty = true;

        void OnEnable()
        {
            // Tras una recarga de scripts la lista se vacía pero las letras ocultas siguen ahí
            if (_pool.Count == 0)
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    var child = transform.GetChild(i).gameObject;
                    if (child.hideFlags != HideFlags.HideAndDontSave) continue;
                    if (Application.isPlaying) Destroy(child);
                    else DestroyImmediate(child);
                }
            }
            _dirty = true;
        }

        void LateUpdate()
        {
            if (_dirty || _builtText != text) Rebuild();
        }

        /// <summary>Corta en líneas respetando maxWidth.</summary>
        public List<string> Wrap(string value)
        {
            var lines = new List<string>();
            foreach (var paragraph in (value ?? "").Split('\n'))
            {
                if (maxWidth <= 0 || font == null)
                {
                    lines.Add(paragraph);
                    continue;
                }
                string line = "";
                foreach (var word in paragraph.Split(' '))
                {
                    string candidate = line.Length == 0 ? word : line + " " + word;
                    if (line.Length > 0 && font.Measure(candidate) > maxWidth)
                    {
                        lines.Add(line);
                        line = word;
                    }
                    else line = candidate;
                }
                lines.Add(line);
            }
            return lines;
        }

        public void Rebuild()
        {
            _dirty = false;
            _builtText = text;
            if (font == null) return;

            var lines = Wrap(text);
            int lineH = font.height + font.lineSpacing;
            int used = 0;
            int charIndex = 0;
            float u = Px.Unit * scale;

            for (int li = 0; li < lines.Count; li++)
            {
                string line = lines[li].ToUpperInvariant();
                int w = font.Measure(line);
                int x = alignment switch
                {
                    Align.Center => -Mathf.FloorToInt(w / 2f),
                    Align.Right => -w,
                    _ => 0,
                };
                int y = -li * lineH;
                foreach (char c in line)
                {
                    if (c == ' ')
                    {
                        x += font.spaceWidth + font.spacing;
                        charIndex++;
                        continue;
                    }
                    if (!font.TryGet(c, out var g)) continue;
                    bool visible = visibleCharacters < 0 || charIndex < visibleCharacters;
                    if (shadow) Place(used++, g.sprite, x, y - 1, u, sortingOrder, shadowColor, visible);
                    Place(used++, g.sprite, x, y, u, sortingOrder + 1, color, visible);
                    x += g.width + font.spacing;
                    charIndex++;
                }
            }
            VisibleLength = charIndex;
            for (int i = used; i < _pool.Count; i++)
                if (_pool[i]) _pool[i].enabled = false;
        }

        void Place(int index, Sprite sprite, int x, int y, float u, int order, Color c, bool visible)
        {
            while (_pool.Count <= index)
            {
                var go = new GameObject("g");
                go.hideFlags = HideFlags.HideAndDontSave;
                go.transform.SetParent(transform, false);
                _pool.Add(go.AddComponent<SpriteRenderer>());
            }
            var sr = _pool[index];
            if (sr == null)
            {
                _pool.Clear();
                Rebuild();
                return;
            }
            sr.enabled = visible;
            sr.sprite = sprite;
            sr.color = c;
            sr.sortingOrder = order;
            if (material) sr.sharedMaterial = material;
            sr.transform.localPosition = new Vector3(x * u, y * u, 0f);
            sr.transform.localScale = new Vector3(scale, scale, 1f);
            sr.gameObject.layer = gameObject.layer;
        }

        void ApplyColors()
        {
            foreach (var sr in _pool)
            {
                if (!sr) continue;
                sr.color = sr.sortingOrder == sortingOrder ? shadowColor : color;
            }
        }

        void OnDestroy()
        {
            foreach (var sr in _pool)
            {
                if (!sr) continue;
                if (Application.isPlaying) Destroy(sr.gameObject);
                else DestroyImmediate(sr.gameObject);
            }
            _pool.Clear();
        }
    }
}
