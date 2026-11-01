using System.Collections;
using BarDelPiu.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BarDelPiu.Core
{
    /// <summary>
    /// Cambio de escena con fundido a negro en pasos (estilo retro, sin degradé suave).
    /// Se crea solo y persiste entre escenas.
    /// </summary>
    public class SceneFlow : MonoBehaviour
    {
        const int Steps = 5;
        const float StepTime = 0.05f;

        static SceneFlow _instance;
        Texture2D _black;
        float _alpha;
        bool _busy;

        public static bool IsBusy => _instance != null && _instance._busy;

        static SceneFlow Get()
        {
            if (_instance == null)
            {
                _instance = new GameObject("SceneFlow").AddComponent<SceneFlow>();
                DontDestroyOnLoad(_instance.gameObject);
            }
            return _instance;
        }

        public static void Load(string scene)
        {
            var f = Get();
            if (f._busy) return;
            f.StartCoroutine(f.Run(scene));
        }

        /// <summary>Fundido de entrada al iniciar una escena directamente (sin pasar por Load).</summary>
        public static void FadeIn() => Get().StartCoroutine(Get().Fade(1f, 0f));

        void Awake()
        {
            _black = new Texture2D(1, 1);
            _black.SetPixel(0, 0, new Color(0.1f, 0.08f, 0.06f, 1f));
            _black.Apply();
        }

        IEnumerator Run(string scene)
        {
            _busy = true;
            AudioManager.Play(Sfx.Whoosh);
            yield return Fade(0f, 1f);
            var op = SceneManager.LoadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
            yield return Fade(1f, 0f);
            _busy = false;
        }

        IEnumerator Fade(float from, float to)
        {
            for (int i = 0; i <= Steps; i++)
            {
                _alpha = Mathf.Lerp(from, to, i / (float)Steps);
                yield return new WaitForSecondsRealtime(StepTime);
            }
            _alpha = to;
        }

        void OnGUI()
        {
            if (_alpha <= 0f) return;
            GUI.depth = -1000;
            var c = GUI.color;
            GUI.color = new Color(1, 1, 1, _alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _black);
            GUI.color = c;
        }
    }
}
