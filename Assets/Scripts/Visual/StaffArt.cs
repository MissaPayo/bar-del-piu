using UnityEngine;

namespace BarDelPiu.Visual
{
    public enum Speaker
    {
        Pipe,
        Nata,
    }

    public enum StaffExpression
    {
        Neutral,
        Feliz,
        Sorpresa,
        Ebrio,
    }

    /// <summary>Retratos de Pipe y Nata (provisorios hasta tener referencias reales).</summary>
    [CreateAssetMenu(menuName = "Bar del Piu/Visual/Staff Art", fileName = "StaffArt")]
    public class StaffArt : ScriptableObject
    {
        public Sprite[] pipe = new Sprite[4];
        public Sprite[] nata = new Sprite[4];
        [Header("Modo Hardcore (look punk/metalero)")]
        public Sprite[] pipeHardcore = new Sprite[4];
        public Sprite[] nataHardcore = new Sprite[4];

        /// <summary>Si está activo, Get devuelve los retratos Hardcore (lo maneja BarScene).</summary>
        public static bool Hardcore;
        [Tooltip("Tono de la «voz» (blip) de cada uno.")]
        public float pipeVoicePitch = 0.85f;
        public float nataVoicePitch = 1.2f;

        public Sprite Get(Speaker s, StaffExpression e)
        {
            var hc = s == Speaker.Pipe ? pipeHardcore : nataHardcore;
            bool useHc = Hardcore && hc != null && hc.Length > 0 && hc[0];
            var set = useHc ? hc : s == Speaker.Pipe ? pipe : nata;
            int i = Mathf.Clamp((int)e, 0, set.Length - 1);
            return set[i];
        }

        public float Pitch(Speaker s) => s == Speaker.Pipe ? pipeVoicePitch : nataVoicePitch;
    }
}
