using UnityEngine;

namespace BarDelPiu.Core
{
    /// <summary>
    /// Datos que viajan entre escenas: puntaje de la última partida, récord local,
    /// si el tutorial ya se vio y el último apodo usado. Persiste en PlayerPrefs.
    /// </summary>
    public static class GameSession
    {
        public const string MenuScene = "Menu";
        public const string IntroScene = "Intro";
        public const string GameScene = "ArtAudioTest"; // reemplazar por la escena de juego real
        public const string ResultsScene = "Results";
        public const string IdleScene = "Espera";

        const string RecordKey = "bdp.record";
        const string TutorialKey = "bdp.tutorial";
        const string NicknameKey = "bdp.nickname";

        public static int LastScore { get; private set; }
        public static int LastPerfects { get; private set; }
        public static bool LastWasRecord { get; private set; }

        /// <summary>Si el tutorial se abrió desde «Instrucciones» vuelve al menú en vez de ir a jugar.</summary>
        public static bool TutorialFromMenu { get; set; }

        public static int Record => PlayerPrefs.GetInt(RecordKey, 0);

        /// <summary>Partida en modo Hardcore: alguien rompió una ampolleta en el menú.</summary>
        public static bool Hardcore { get; private set; }

        /// <summary>La escena de juego llama esto al empezar: decide si la partida es Hardcore.</summary>
        public static bool BeginMatch()
        {
            Hardcore = BarMischief.AnyLampBroken;
            return Hardcore;
        }

        /// <summary>Multiplicador Hardcore: 3 elevado a los aciertos seguidos (x3, x9, x27...), con tope.</summary>
        public static int HardcoreMultiplier(int streak)
        {
            int m = 1;
            for (int i = 0; i < Mathf.Clamp(streak, 0, 10); i++) m *= 3;
            return m;
        }

        public static bool TutorialDone
        {
            get => PlayerPrefs.GetInt(TutorialKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(TutorialKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static string Nickname
        {
            get => PlayerPrefs.GetString(NicknameKey, "");
            set
            {
                PlayerPrefs.SetString(NicknameKey, value ?? "");
                PlayerPrefs.Save();
            }
        }

        /// <summary>La lógica de juego llama esto al terminar la partida y luego carga Results.</summary>
        public static void FinishGame(int score, int perfects = 0)
        {
            LastScore = Mathf.Max(0, score);
            // Se jugó una partida: ampolletas nuevas y Pipe y Nata sobrios otra vez
            BarMischief.ResetAfterMatch();
            Hardcore = false;
            LastPerfects = perfects;
            LastWasRecord = LastScore > Record;
            if (LastWasRecord)
            {
                PlayerPrefs.SetInt(RecordKey, LastScore);
                PlayerPrefs.Save();
            }
            SceneFlow.Load(ResultsScene);
        }

        /// <summary>«Jugar»: la primera vez pasa por la intro y el tutorial.</summary>
        public static void Play()
        {
            TutorialFromMenu = false;
            SceneFlow.Load(TutorialDone ? GameScene : IntroScene);
        }
    }
}
