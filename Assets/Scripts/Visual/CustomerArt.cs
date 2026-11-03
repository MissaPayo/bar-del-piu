using System;
using System.Collections.Generic;
using UnityEngine;

namespace BarDelPiu.Visual
{
    public enum Mood
    {
        Neutral,
        Feliz,
        Impaciente,
        Enojado,
    }

    /// <summary>Sprites de clientes (variantes x expresiones) y globos de pedido.</summary>
    [CreateAssetMenu(menuName = "Bar del Piu/Visual/Customer Art", fileName = "CustomerArt")]
    public class CustomerArt : ScriptableObject
    {
        [Serializable]
        public class Variant
        {
            public Sprite neutral;
            public Sprite feliz;
            public Sprite impaciente;
            public Sprite enojado;

            public Sprite Get(Mood m) => m switch
            {
                Mood.Feliz => feliz,
                Mood.Impaciente => impaciente,
                Mood.Enojado => enojado,
                _ => neutral,
            };
        }

        public List<Variant> variants = new List<Variant>();
        [Tooltip("Clientes del modo Hardcore: punks y metaleros.")]
        public List<Variant> hardcoreVariants = new List<Variant>();
        public Sprite orderRubia;
        public Sprite orderAmbar;
        public Sprite orderNegra;
        [Header("Pedido resaltado (cliente que espera)")]
        public Sprite orderRubiaHint;
        public Sprite orderAmbarHint;
        public Sprite orderNegraHint;

        [Header("Paciencia")]
        public Gradient patienceColor;
        [Range(0f, 1f)] public float impatientBelow = 0.6f;
        [Range(0f, 1f)] public float angryBelow = 0.3f;

        public Sprite OrderHint(BeerKind k) => k switch
        {
            BeerKind.Rubia => orderRubiaHint,
            BeerKind.Ambar => orderAmbarHint,
            _ => orderNegraHint,
        };

        public Sprite Order(BeerKind k) => k switch
        {
            BeerKind.Rubia => orderRubia,
            BeerKind.Ambar => orderAmbar,
            _ => orderNegra,
        };
    }
}
