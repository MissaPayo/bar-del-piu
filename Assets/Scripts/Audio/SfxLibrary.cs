using System;
using System.Collections.Generic;
using UnityEngine;

namespace BarDelPiu.Audio
{
    [Serializable]
    public class SfxEntry
    {
        public Sfx id;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Rango de pitch aleatorio (min, max). Da variedad a sonidos repetidos.")]
        public Vector2 pitch = new Vector2(0.97f, 1.03f);
        public AudioBus bus = AudioBus.Sfx;
        [Tooltip("Tiempo mínimo entre dos reproducciones del mismo sonido.")]
        public float cooldown = 0.03f;
        [Tooltip("Máximo de voces simultáneas de este sonido.")]
        public int maxVoices = 4;
    }

    [CreateAssetMenu(menuName = "Bar del Piu/Audio/Sfx Library", fileName = "SfxLibrary")]
    public class SfxLibrary : ScriptableObject
    {
        public List<SfxEntry> entries = new List<SfxEntry>();

        Dictionary<Sfx, SfxEntry> _lookup;

        public SfxEntry Get(Sfx id)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<Sfx, SfxEntry>();
                foreach (var e in entries)
                    _lookup[e.id] = e;
            }
            _lookup.TryGetValue(id, out var entry);
            return entry;
        }

        void OnValidate() => _lookup = null;
    }
}
