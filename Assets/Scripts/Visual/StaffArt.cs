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
        [Tooltip("Tono de la «voz» (blip) de cada uno.")]
        public float pipeVoicePitch = 0.85f;
        public float nataVoicePitch = 1.2f;

        public Sprite Get(Speaker s, StaffExpression e)
        {
            var set = s == Speaker.Pipe ? pipe : nata;
            int i = Mathf.Clamp((int)e, 0, set.Length - 1);
            return set[i];
        }

        public float Pitch(Speaker s) => s == Speaker.Pipe ? pipeVoicePitch : nataVoicePitch;
    }
}
