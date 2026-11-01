using System;
using System.Collections.Generic;
using UnityEngine;

namespace BarDelPiu.Audio
{
    [Serializable]
    public class MusicEntry
    {
        public MusicTrack track;
        public AudioClip main;
        [Tooltip("Capa opcional sincronizada que entra con SetMusicIntensity (misma duración que main).")]
        public AudioClip rush;
        public bool loop = true;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [CreateAssetMenu(menuName = "Bar del Piu/Audio/Music Library", fileName = "MusicLibrary")]
    public class MusicLibrary : ScriptableObject
    {
        public List<MusicEntry> tracks = new List<MusicEntry>();
        public AudioClip ambience;
        [Range(0f, 1f)] public float ambienceVolume = 1f;

        public MusicEntry Get(MusicTrack track) => tracks.Find(t => t.track == track);
    }
}
