using UnityEngine;

namespace BarDelPiu
{
    /// <summary>Variedades de cerveza del MVP. La lógica de juego puede usar este mismo enum en BeerData.</summary>
    public enum BeerKind
    {
        Rubia,
        Ambar,
        Negra,
    }

    /// <summary>Resultado del servido (sección 4.3 del MVP).</summary>
    public enum ServeGrade
    {
        Perfecto,
        Bueno,
        Regular,
        Malo,
    }
}

namespace BarDelPiu.Visual
{
    /// <summary>Paleta oficial (ver Assets/Art/GUIA_ARTE_Y_AUDIO.md).</summary>
    public static class BarPalette
    {
        public static readonly Color Ink = Hex(0x1A1410);
        public static readonly Color GreenDark = Hex(0x1B2B24);
        public static readonly Color Green = Hex(0x263B32);
        public static readonly Color GreenLight = Hex(0x3A5A49);
        public static readonly Color GreenHighlight = Hex(0x5C8068);
        public static readonly Color Cream = Hex(0xF4EBD8);
        public static readonly Color CreamDark = Hex(0xD9C9A8);
        public static readonly Color Amber = Hex(0xD99B32);
        public static readonly Color AmberLight = Hex(0xF2C25C);
        public static readonly Color AmberDark = Hex(0xA8701F);
        public static readonly Color Cafe = Hex(0x795638);
        public static readonly Color CafeDark = Hex(0x553B26);
        public static readonly Color Red = Hex(0xC65C4B);
        public static readonly Color RedDark = Hex(0x8E3A30);
        public static readonly Color Neon = Hex(0xFF7A6B);
        public static readonly Color Mint = Hex(0x7FE0C8);

        public static Color LiquidLight(BeerKind k) => k switch
        {
            BeerKind.Rubia => Hex(0xF5C84C),
            BeerKind.Ambar => Hex(0xD2802E),
            _ => Hex(0x4A2C1C),
        };

        public static Color LiquidDark(BeerKind k) => k switch
        {
            BeerKind.Rubia => Hex(0xD9A030),
            BeerKind.Ambar => Hex(0x9A5420),
            _ => Hex(0x24150E),
        };

        public static Color Foam(BeerKind k) => k switch
        {
            BeerKind.Rubia => Hex(0xFFF8EA),
            BeerKind.Ambar => Hex(0xFFF1DA),
            _ => Hex(0xE9D3B0),
        };

        public static Color FoamShade(BeerKind k) => Color.Lerp(Foam(k), Hex(0xB3A27F), 0.35f);

        public static Color Grade(ServeGrade g) => g switch
        {
            ServeGrade.Perfecto => AmberLight,
            ServeGrade.Bueno => GreenHighlight,
            ServeGrade.Regular => CreamDark,
            _ => Red,
        };

        public static string GradeLabel(ServeGrade g) => g switch
        {
            ServeGrade.Perfecto => "¡PERFECTO!",
            ServeGrade.Bueno => "¡BUENO!",
            ServeGrade.Regular => "REGULAR",
            _ => "MALO...",
        };

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }

    /// <summary>Órdenes de dibujo (todo en la capa Default para que las luces 2D afecten a todo).</summary>
    public static class SortOrder
    {
        public const int Background = 0;
        public const int WallDecor = 10;
        public const int Customers = 100;
        public const int CustomerUi = 150;
        public const int Underbar = 190;
        public const int Counter = 200;
        public const int Props = 300;
        public const int Fx = 400;
        public const int Ui = 500;
        public const int UiText = 520;
        public const int Overlay = 600;
        /// <summary>Diálogos de Pipe y Nata: por encima de botones, teclado y ranking para que nunca se corten.</summary>
        public const int Dialogue = 660;
    }

    /// <summary>Utilidades de píxel: 16 PPU.</summary>
    public static class Px
    {
        public const float PPU = 16f;
        public const float Unit = 1f / PPU;

        public static float Snap(float v) => Mathf.Round(v * PPU) / PPU;

        public static Vector3 Snap(Vector3 v) => new Vector3(Snap(v.x), Snap(v.y), v.z);
    }
}
