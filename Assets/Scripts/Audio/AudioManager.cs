using System.Collections.Generic;
using UnityEngine;

namespace BarDelPiu.Audio
{
    /// <summary>
    /// Punto único de audio del juego. Se crea solo desde Resources/BarDelPiu/AudioManager
    /// la primera vez que se usa, y persiste entre escenas.
    ///
    /// Uso: AudioManager.Play(Sfx.TapOpen); AudioManager.PlayMusic(MusicTrack.Game);
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AudioManager : MonoBehaviour
    {
        const string ResourcePath = "BarDelPiu/AudioManager";
        const string PrefsPrefix = "bdp.vol.";
        const int OneShotVoices = 16;
        const int LoopVoices = 8;

        public SfxLibrary sfx;
        public MusicLibrary music;

        [Header("Volúmenes iniciales")]
        [Range(0f, 1f)] public float defaultMaster = 1f;
        [Range(0f, 1f)] public float defaultMusic = 0.7f;
        [Range(0f, 1f)] public float defaultAmbience = 0.5f;
        [Range(0f, 1f)] public float defaultSfx = 1f;
        [Range(0f, 1f)] public float defaultUi = 0.8f;

        [Header("Mezcla")]
        [Tooltip("Cuántos semitonos sube el sonido de combo como máximo.")]
        public int comboMaxSemitones = 12;
        public float musicFade = 1.2f;

        static AudioManager _instance;
        static bool _quitting;

        readonly float[] _bus = new float[5];
        readonly List<AudioSource> _oneShots = new List<AudioSource>();
        readonly List<AudioSource> _loops = new List<AudioSource>();
        readonly Dictionary<Sfx, float> _lastPlayed = new Dictionary<Sfx, float>();
        readonly Dictionary<AudioSource, Sfx> _voiceOwner = new Dictionary<AudioSource, Sfx>();
        readonly Dictionary<AudioSource, LoopState> _loopState = new Dictionary<AudioSource, LoopState>();

        AudioSource _musicA, _musicB, _rushA, _rushB, _ambience;
        bool _aIsCurrent = true;
        MusicTrack _currentTrack = MusicTrack.None;
        float _musicTrackVolume = 1f;
        float _musicFadeT = 1f;
        float _intensity, _intensityTarget;
        float _duck = 1f, _duckTarget = 1f, _duckUntil;
        bool _ambienceOn;

        public static AudioManager Instance
        {
            get
            {
                if (_instance is null && !_quitting)
                {
                    var prefab = Resources.Load<AudioManager>(ResourcePath);
                    _instance = prefab != null
                        ? Instantiate(prefab)
                        : new GameObject("AudioManager").AddComponent<AudioManager>();
                    _instance.name = "AudioManager";
                }
                return _instance;
            }
        }

        /// <summary>Instancia viva o null real (evita usar un objeto ya destruido al cerrar).</summary>
        static AudioManager Get()
        {
            var i = Instance;
            return i != null ? i : null;
        }

        // ------------------------------------------------------------------ API estática

        public static void Play(Sfx id, float volume = 1f, float pitch = 0f) => Get()?.PlayInternal(id, volume, pitch);

        /// <summary>Sonido de combo: sube un semitono por nivel (1 = tono base).</summary>
        public static void PlayCombo(int level)
        {
            var am = Get();
            if (am == null) return;
            int semis = Mathf.Clamp(level - 1, 0, am.comboMaxSemitones);
            am.PlayInternal(Sfx.ComboUp, 1f, Mathf.Pow(2f, semis / 12f));
        }

        /// <summary>Blip de diálogo. Pipe usa un tono más grave que Nata.</summary>
        public static void PlayDialogBlip(float basePitch) =>
            Get()?.PlayInternal(Sfx.DialogBlip, 1f, basePitch * Random.Range(0.94f, 1.06f));

        public static AudioLoopHandle StartLoop(Sfx id, float volume = 1f, float pitch = 1f) =>
            Get()?.StartLoopInternal(id, volume, pitch);

        public static void PlayMusic(MusicTrack track) => Get()?.PlayMusicInternal(track);

        /// <summary>0 = solo música base, 1 = capa «rush» completa (últimos segundos, barra llena).</summary>
        public static void SetMusicIntensity(float value)
        {
            var am = Get();
            if (am != null) am._intensityTarget = Mathf.Clamp01(value);
        }

        public static void SetAmbience(bool on)
        {
            Get()?.SetAmbienceInternal(on);
        }

        /// <summary>Baja la música momentáneamente para que se escuche un resultado importante.</summary>
        public static void Duck(float amount = 0.5f, float duration = 0.6f)
        {
            var am = Get();
            if (am == null) return;
            am._duckTarget = Mathf.Min(am._duckTarget, 1f - Mathf.Clamp01(amount));
            am._duckUntil = Time.unscaledTime + duration;
        }

        public static float GetVolume(AudioBus bus)
        {
            var am = Get();
            return am != null ? am._bus[(int)bus] : 0f;
        }

        public static void SetVolume(AudioBus bus, float value)
        {
            var am = Get();
            if (am == null) return;
            am._bus[(int)bus] = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefsPrefix + bus, am._bus[(int)bus]);
            am.ApplyVolumes();
        }

        // ------------------------------------------------------------------ ciclo de vida

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            float[] defaults = { defaultMaster, defaultMusic, defaultAmbience, defaultSfx, defaultUi };
            for (int i = 0; i < _bus.Length; i++)
                _bus[i] = PlayerPrefs.GetFloat(PrefsPrefix + (AudioBus)i, defaults[i]);

            for (int i = 0; i < OneShotVoices; i++) _oneShots.Add(NewSource("OneShot"));
            for (int i = 0; i < LoopVoices; i++) _loops.Add(NewSource("Loop", loop: true));
            _musicA = NewSource("MusicA", true);
            _musicB = NewSource("MusicB", true);
            _rushA = NewSource("RushA", true);
            _rushB = NewSource("RushB", true);
            _ambience = NewSource("Ambience", true);
            ApplyVolumes();
        }

        void OnApplicationQuit() => _quitting = true;

        AudioSource NewSource(string label, bool loop = false)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;
            return src;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (Time.unscaledTime > _duckUntil) _duckTarget = 1f;
            _duck = Mathf.MoveTowards(_duck, _duckTarget, dt * (_duckTarget < _duck ? 6f : 1.5f));
            _intensity = Mathf.MoveTowards(_intensity, _intensityTarget, dt * 0.5f);
            _musicFadeT = Mathf.MoveTowards(_musicFadeT, 1f, dt / Mathf.Max(0.01f, musicFade));

            UpdateMusicVolumes();
            _ambience.volume = Mathf.MoveTowards(_ambience.volume,
                _ambienceOn ? BusGain(AudioBus.Ambience) * (music ? music.ambienceVolume : 1f) : 0f, dt * 0.5f);
            if (!_ambienceOn && _ambience.isPlaying && _ambience.volume <= 0f) _ambience.Stop();
        }

        float BusGain(AudioBus bus) => bus == AudioBus.Master ? _bus[0] : _bus[0] * _bus[(int)bus];

        void ApplyVolumes()
        {
            foreach (var kv in _loopState)
                if (kv.Key.isPlaying) ApplyLoop(kv.Key, kv.Value);
        }

        // ------------------------------------------------------------------ efectos

        void PlayInternal(Sfx id, float volume, float pitch)
        {
            var e = sfx != null ? sfx.Get(id) : null;
            if (e == null || e.clips == null || e.clips.Length == 0) return;

            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(id, out var last) && now - last < e.cooldown) return;

            int voices = 0;
            AudioSource free = null, oldest = null;
            foreach (var s in _oneShots)
            {
                if (s.isPlaying)
                {
                    if (_voiceOwner.TryGetValue(s, out var owner) && owner == id) { voices++; oldest ??= s; }
                }
                else free ??= s;
            }
            if (voices >= e.maxVoices) free = oldest;
            if (free == null) return;

            _lastPlayed[id] = now;
            _voiceOwner[free] = id;
            free.Stop();
            free.clip = e.clips[Random.Range(0, e.clips.Length)];
            free.pitch = pitch > 0f ? pitch : Random.Range(e.pitch.x, e.pitch.y);
            free.volume = e.volume * volume * BusGain(e.bus);
            free.Play();
        }

        AudioLoopHandle StartLoopInternal(Sfx id, float volume, float pitch)
        {
            var e = sfx != null ? sfx.Get(id) : null;
            if (e == null || e.clips == null || e.clips.Length == 0) return null;
            var src = _loops.Find(s => !s.isPlaying);
            if (src == null) return null;

            if (!_loopState.TryGetValue(src, out var state)) _loopState[src] = state = new LoopState();
            state.entry = e;
            state.volume = volume;
            state.fade = 1f;
            state.generation++;
            src.clip = e.clips[0];
            src.pitch = pitch;
            // Empieza en un punto al azar para que dos loops iguales no suenen en fase
            // (en WebGL el clip puede no estar cargado todavía: entonces desde el inicio)
            if (src.clip.loadState == AudioDataLoadState.Loaded) src.time = Random.Range(0f, src.clip.length * 0.9f);
            ApplyLoop(src, state);
            src.Play();
            return new AudioLoopHandle(src, state, state.generation, this);
        }

        internal void ApplyLoop(AudioSource src, LoopState s)
        {
            if (s.entry != null) src.volume = s.entry.volume * BusGain(s.entry.bus) * s.volume * s.fade;
        }

        // ------------------------------------------------------------------ música

        void PlayMusicInternal(MusicTrack track)
        {
            if (track == _currentTrack) return;
            _currentTrack = track;
            _aIsCurrent = !_aIsCurrent;
            var main = _aIsCurrent ? _musicA : _musicB;
            var rush = _aIsCurrent ? _rushA : _rushB;
            main.Stop();
            rush.Stop();
            _musicFadeT = 0f;

            var entry = music != null ? music.Get(track) : null;
            if (entry == null || entry.main == null)
            {
                _musicTrackVolume = 0f;
                return;
            }
            _musicTrackVolume = entry.volume;
            main.clip = entry.main;
            main.loop = entry.loop;
            main.volume = 0f;
            double start = AudioSettings.dspTime + 0.05;
            main.PlayScheduled(start);
            if (entry.rush != null)
            {
                rush.clip = entry.rush;
                rush.loop = entry.loop;
                rush.volume = 0f;
                rush.PlayScheduled(start);
            }
        }

        void UpdateMusicVolumes()
        {
            float bus = BusGain(AudioBus.Music) * _duck;
            float fadeIn = _musicFadeT;
            float fadeOut = 1f - _musicFadeT;
            var curMain = _aIsCurrent ? _musicA : _musicB;
            var curRush = _aIsCurrent ? _rushA : _rushB;
            var oldMain = _aIsCurrent ? _musicB : _musicA;
            var oldRush = _aIsCurrent ? _rushB : _rushA;

            curMain.volume = bus * _musicTrackVolume * fadeIn;
            curRush.volume = bus * _musicTrackVolume * fadeIn * _intensity;
            oldMain.volume = Mathf.Min(oldMain.volume, bus * fadeOut);
            oldRush.volume = Mathf.Min(oldRush.volume, bus * fadeOut * _intensity);
            if (_musicFadeT >= 1f)
            {
                if (oldMain.isPlaying) oldMain.Stop();
                if (oldRush.isPlaying) oldRush.Stop();
            }
        }

        void SetAmbienceInternal(bool on)
        {
            _ambienceOn = on;
            if (on && !_ambience.isPlaying && music != null && music.ambience != null)
            {
                _ambience.clip = music.ambience;
                _ambience.volume = 0f;
                _ambience.Play();
            }
        }

        /// <summary>Estado de un AudioSource de loop. generation invalida handles viejos al reutilizarlo.</summary>
        internal class LoopState
        {
            public SfxEntry entry;
            public float volume = 1f;
            public float fade = 1f;
            public int generation;
        }
    }

    /// <summary>Control de un sonido en loop (chorro, espuma). Llamar Stop() al terminar.</summary>
    public class AudioLoopHandle
    {
        readonly AudioSource _src;
        readonly AudioManager.LoopState _state;
        readonly int _generation;
        readonly AudioManager _am;
        Coroutine _fadeRoutine;

        internal AudioLoopHandle(AudioSource src, AudioManager.LoopState state, int generation, AudioManager am)
        {
            _src = src;
            _state = state;
            _generation = generation;
            _am = am;
        }

        bool Valid => _src != null && _am != null && _state.generation == _generation;

        public bool IsPlaying => Valid && _src.isPlaying;

        public void SetPitch(float pitch)
        {
            if (Valid) _src.pitch = pitch;
        }

        public void SetVolume(float volume)
        {
            if (!Valid) return;
            _state.volume = volume;
            _am.ApplyLoop(_src, _state);
        }

        public void Stop(float fade = 0.08f)
        {
            if (!IsPlaying) return;
            if (_fadeRoutine != null) _am.StopCoroutine(_fadeRoutine);
            _fadeRoutine = _am.StartCoroutine(FadeOut(fade));
        }

        System.Collections.IEnumerator FadeOut(float duration)
        {
            float t = 0f;
            while (t < duration && Valid)
            {
                t += Time.unscaledDeltaTime;
                _state.fade = 1f - t / duration;
                _am.ApplyLoop(_src, _state);
                yield return null;
            }
            if (Valid)
            {
                _src.Stop();
                _state.fade = 1f;
            }
        }
    }
}
