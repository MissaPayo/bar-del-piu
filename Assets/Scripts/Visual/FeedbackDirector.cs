using System.Collections.Generic;
using BarDelPiu.Audio;
using UnityEngine;

namespace BarDelPiu.Visual
{
    /// <summary>
    /// Centraliza el «game feel» de los eventos de juego: texto flotante, partículas,
    /// sonido, sacudida de cámara y ducking. La lógica llama un método por evento.
    /// </summary>
    public class FeedbackDirector : MonoBehaviour
    {
        public static FeedbackDirector Instance { get; private set; }

        [Header("Referencias")]
        public PixelFont font;
        public Material textMaterial;
        public CameraShake cameraShake;

        [Header("Partículas (prefabs)")]
        public ParticleSystem sparkles;
        public ParticleSystem foamBurst;
        public ParticleSystem coins;
        public ParticleSystem puff;
        public ParticleSystem hearts;

        readonly Dictionary<ParticleSystem, ParticleSystem> _instances = new Dictionary<ParticleSystem, ParticleSystem>();

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ eventos

        public void ServeResult(ServeGrade grade, Vector3 pos, int points, int combo)
        {
            var color = BarPalette.Grade(grade);
            FloatingText.Spawn(this, font, textMaterial, BarPalette.GradeLabel(grade), pos + Vector3.up * 0.6f, color,
                scale: 1, rise: 14, duration: 0.9f, pop: grade == ServeGrade.Perfecto);
            if (points > 0)
                FloatingText.Spawn(this, font, textMaterial, "+" + points, pos + Vector3.up * 0.1f, BarPalette.Cream,
                    scale: 1, rise: 10, duration: 0.8f, delay: 0.08f);
            if (combo >= 2)
                FloatingText.Spawn(this, font, textMaterial, "COMBO X" + combo, pos + Vector3.up * 1.4f, BarPalette.Neon,
                    scale: 1, rise: 8, duration: 0.9f, delay: 0.15f, pop: true);

            switch (grade)
            {
                case ServeGrade.Perfecto:
                    Burst(sparkles, pos, 14);
                    Burst(foamBurst, pos, 8);
                    AudioManager.Play(Sfx.ServePerfect);
                    AudioManager.Duck(0.45f, 0.6f);
                    if (combo >= 5 && cameraShake) cameraShake.Shake(1, 0.12f);
                    break;
                case ServeGrade.Bueno:
                    Burst(sparkles, pos, 6);
                    AudioManager.Play(Sfx.ServeGood);
                    break;
                case ServeGrade.Regular:
                    Burst(foamBurst, pos, 4);
                    AudioManager.Play(Sfx.ServeRegular);
                    break;
                default:
                    Burst(puff, pos, 6);
                    AudioManager.Play(Sfx.ServeBad);
                    if (cameraShake) cameraShake.Shake(2, 0.18f);
                    break;
            }
            if (combo >= 2) AudioManager.PlayCombo(combo);
        }

        public void Tip(Vector3 pos, int amount)
        {
            Burst(coins, pos, Mathf.Clamp(amount / 10, 3, 12));
            AudioManager.Play(Sfx.TipCoin);
            FloatingText.Spawn(this, font, textMaterial, "+$" + amount, pos + Vector3.up * 0.4f, BarPalette.AmberLight,
                scale: 1, rise: 12, duration: 0.9f, delay: 0.1f);
        }

        public void CustomerHappy(Vector3 pos) => Burst(hearts, pos, 4);

        public void CustomerAngry(Vector3 pos) => Burst(puff, pos, 8);

        public void Puff(Vector3 pos, int count = 6) => Burst(puff, pos, count);

        public void Sparks(Vector3 pos, int count = 12) => Burst(sparkles, pos, count);

        /// <summary>Error (pedido incorrecto, cliente perdido...).</summary>
        public void Error(Vector3 pos, string label = "¡ERROR!")
        {
            AudioManager.Play(Sfx.Error);
            AudioManager.Duck(0.3f, 0.4f);
            if (cameraShake) cameraShake.Shake(2, 0.2f);
            FloatingText.Spawn(this, font, textMaterial, label, pos, BarPalette.Red, scale: 1, rise: 10, duration: 0.8f, pop: true);
        }

        public void Message(Vector3 pos, string text, Color color, int scale = 1) =>
            FloatingText.Spawn(this, font, textMaterial, text, pos, color, scale, rise: 8, duration: 1.1f, pop: true);

        // ------------------------------------------------------------------ partículas

        void Burst(ParticleSystem prefab, Vector3 pos, int count)
        {
            if (prefab == null) return;
            if (!_instances.TryGetValue(prefab, out var ps) || ps == null)
            {
                ps = Instantiate(prefab, transform);
                _instances[prefab] = ps;
            }
            ps.transform.position = new Vector3(pos.x, pos.y, 0f);
            ps.Emit(count);
        }
    }
}
