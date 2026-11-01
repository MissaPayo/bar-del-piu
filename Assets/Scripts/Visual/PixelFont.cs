using System;
using System.Collections.Generic;
using UnityEngine;

namespace BarDelPiu.Visual
{
    /// <summary>Fuente bitmap 5x7 (solo mayúsculas, con tildes, Ñ, ¡ y ¿). Se arma desde font.json.</summary>
    [CreateAssetMenu(menuName = "Bar del Piu/Visual/Pixel Font", fileName = "PixelFont")]
    public class PixelFont : ScriptableObject
    {
        [Serializable]
        public struct Glyph
        {
            public string character;
            public Sprite sprite;
            public int width;
        }

        public int height = 10;
        public int spacing = 1;
        public int spaceWidth = 3;
        public int lineSpacing = 2;
        public List<Glyph> glyphs = new List<Glyph>();

        Dictionary<char, Glyph> _map;

        public bool TryGet(char c, out Glyph g)
        {
            if (_map == null)
            {
                _map = new Dictionary<char, Glyph>();
                foreach (var gl in glyphs)
                    if (!string.IsNullOrEmpty(gl.character)) _map[gl.character[0]] = gl;
            }
            return _map.TryGetValue(c, out g);
        }

        /// <summary>Ancho en píxeles de una línea (sin escala).</summary>
        public int Measure(string line)
        {
            int w = 0;
            foreach (char raw in line)
            {
                char c = char.ToUpperInvariant(raw);
                if (c == ' ') w += spaceWidth + spacing;
                else if (TryGet(c, out var g)) w += g.width + spacing;
            }
            return Mathf.Max(0, w - spacing);
        }

        void OnValidate() => _map = null;
    }
}
