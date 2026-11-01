using UnityEngine;

namespace BarDelPiu.Core
{
    /// <summary>Opciones de accesibilidad guardadas en PlayerPrefs (los volúmenes los guarda AudioManager).</summary>
    public static class GameSettings
    {
        const string ShakeKey = "bdp.shake";
        const string FlickerKey = "bdp.flicker";

        public const string Version = "1.0";

        public static bool ScreenShake
        {
            get => PlayerPrefs.GetInt(ShakeKey, 1) == 1;
            set { PlayerPrefs.SetInt(ShakeKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Parpadeo de los neones (desactivable para personas sensibles a destellos).</summary>
        public static bool NeonFlicker
        {
            get => PlayerPrefs.GetInt(FlickerKey, 1) == 1;
            set { PlayerPrefs.SetInt(FlickerKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
