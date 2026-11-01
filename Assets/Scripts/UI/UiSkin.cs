using BarDelPiu.Visual;
using UnityEngine;

namespace BarDelPiu.UI
{
    /// <summary>Recursos compartidos de interfaz para crear botones, paneles y teclado por código.</summary>
    [CreateAssetMenu(menuName = "Bar del Piu/UI/Ui Skin", fileName = "UiSkin")]
    public class UiSkin : ScriptableObject
    {
        public PixelFont font;
        public Material unlit;
        public Sprite buttonNormal;
        public Sprite buttonPressed;
        public Sprite panelCream;
        public Sprite panelDark;
        public Sprite arrowDown;
        public Sprite soundOn;
        public Sprite soundOff;
        public Sprite pixel;
        public Sprite gear;
        public Sprite heart;

        [Header("Cumpleaños")]
        public Sprite cake;
        public Sprite[] candleFlame;
        public Sprite hatRed;
        public Sprite hatMint;
        public Sprite confetti;
    }
}
