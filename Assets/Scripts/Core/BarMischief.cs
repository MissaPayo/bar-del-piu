using BarDelPiu.Visual;
using UnityEngine;

namespace BarDelPiu.Core
{
    /// <summary>
    /// Travesuras del menú que duran hasta la próxima partida (se guardan en PlayerPrefs):
    /// cervezas regaladas a Pipe y Nata, quién quedó ebrio y qué ampolletas se rompieron.
    /// Si hay una ampolleta rota al empezar la partida, se juega en modo Hardcore.
    /// </summary>
    public static class BarMischief
    {
        /// <summary>Con la cerveza número 11 el personaje queda ebrio.</summary>
        public const int DrunkAfter = 10;
        /// <summary>Si el compañero está ebrio, el otro acepta solo 9 (tiene que trabajar).</summary>
        public const int SoberLimitWhenPartnerDrunk = 9;
        public const int LampCount = 2;

        const string Prefix = "bdp.mischief.";

        public static int Beers(Speaker who) => PlayerPrefs.GetInt(Prefix + "beers." + who, 0);

        public static bool IsDrunk(Speaker who) => PlayerPrefs.GetInt(Prefix + "drunk." + who, 0) == 1;

        public static Speaker Other(Speaker who) => who == Speaker.Pipe ? Speaker.Nata : Speaker.Pipe;

        /// <summary>Quién atiende: el pedido, salvo que esté ebrio (entonces el otro).</summary>
        public static Speaker Available(Speaker preferred) => IsDrunk(preferred) ? Other(preferred) : preferred;

        public enum GiveResult { Accepted, BecameDrunk, Refused, Absent }

        /// <summary>Registra una cerveza regalada y devuelve qué pasó.</summary>
        public static GiveResult Give(Speaker who)
        {
            if (IsDrunk(who)) return GiveResult.Absent;
            int n = Beers(who);
            if (IsDrunk(Other(who)) && n >= SoberLimitWhenPartnerDrunk) return GiveResult.Refused;
            n++;
            PlayerPrefs.SetInt(Prefix + "beers." + who, n);
            bool drunk = n > DrunkAfter;
            if (drunk) PlayerPrefs.SetInt(Prefix + "drunk." + who, 1);
            PlayerPrefs.Save();
            return drunk ? GiveResult.BecameDrunk : GiveResult.Accepted;
        }

        public static bool IsLampBroken(int i) => PlayerPrefs.GetInt(Prefix + "lamp." + i, 0) == 1;

        public static void BreakLamp(int i)
        {
            PlayerPrefs.SetInt(Prefix + "lamp." + i, 1);
            PlayerPrefs.Save();
        }

        public static bool AnyLampBroken
        {
            get
            {
                for (int i = 0; i < LampCount; i++)
                    if (IsLampBroken(i)) return true;
                return false;
            }
        }

        /// <summary>Al terminar una partida: ampolletas nuevas y Pipe y Nata sobrios otra vez.</summary>
        public static void ResetAfterMatch()
        {
            foreach (var s in new[] { Speaker.Pipe, Speaker.Nata })
            {
                PlayerPrefs.DeleteKey(Prefix + "beers." + s);
                PlayerPrefs.DeleteKey(Prefix + "drunk." + s);
            }
            for (int i = 0; i < LampCount; i++) PlayerPrefs.DeleteKey(Prefix + "lamp." + i);
            PlayerPrefs.Save();
        }
    }
}
