using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using UnityEngine;

namespace BarDelPiu.Leaderboard
{
    public struct ScoreRow
    {
        public int rank; // 1 = primero
        public string name;
        public int score;
        public bool isPlayer;
    }

    /// <summary>
    /// Ranking online con Unity Leaderboards (login anónimo automático por navegador/dispositivo).
    /// Si no hay conexión o el servicio no está configurado, usa un ranking local de respaldo.
    /// </summary>
    public static class LeaderboardClient
    {
        /// <summary>Id de la tabla en Unity Cloud (ver Assets/Services/bar_del_piu_puntaje.lb).</summary>
        public const string LeaderboardId = "bar_del_piu_puntaje";

        static Task<bool> _init;

        public static bool IsOnline { get; private set; }
        public static string LastError { get; private set; }

        [Serializable]
        class Meta
        {
            public string name;
        }

        public static Task<bool> EnsureReadyAsync() => _init ??= InitAsync();

        static async Task<bool> InitAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                IsOnline = true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogWarning("[Ranking] Sin servicio online, se usa el ranking local: " + e.Message);
                IsOnline = false;
                _init = null; // reintentar la próxima vez
            }
            return IsOnline;
        }

        /// <summary>Envía el puntaje (la tabla guarda el mejor de cada jugador). Devuelve el puesto (1..n) o 0.</summary>
        public static async Task<int> SubmitAsync(int score, string nickname)
        {
            LocalScores.Add(nickname, score);
            if (!await EnsureReadyAsync()) return LocalScores.RankOf(score);
            try
            {
                // La API exige un objeto JSON en metadata (un texto con JSON da error 400)
                var meta = new Dictionary<string, string> { { "name", nickname } };
                var entry = await LeaderboardsService.Instance.AddPlayerScoreAsync(LeaderboardId, score,
                    new AddPlayerScoreOptions { Metadata = meta });
                return entry.Rank + 1;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogWarning("[Ranking] No se pudo enviar el puntaje: " + e.Message);
                IsOnline = false;
                return LocalScores.RankOf(score);
            }
        }

        public static async Task<List<ScoreRow>> GetTopAsync(int count = 10)
        {
            if (!await EnsureReadyAsync()) return LocalScores.Top(count);
            try
            {
                var page = await LeaderboardsService.Instance.GetScoresAsync(LeaderboardId,
                    new GetScoresOptions { Limit = count, IncludeMetadata = true });
                string me = AuthenticationService.Instance.PlayerId;
                var rows = new List<ScoreRow>();
                foreach (var e in page.Results)
                {
                    rows.Add(new ScoreRow
                    {
                        rank = e.Rank + 1,
                        name = NameFrom(e.Metadata, e.PlayerName),
                        score = (int)e.Score,
                        isPlayer = e.PlayerId == me,
                    });
                }
                IsOnline = true;
                return rows;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogWarning("[Ranking] No se pudo leer el ranking: " + e.Message);
                IsOnline = false;
                return LocalScores.Top(count);
            }
        }

        /// <summary>Puesto y mejor puntaje del jugador en la tabla online (null si no tiene).</summary>
        public static async Task<ScoreRow?> GetPlayerAsync()
        {
            if (!await EnsureReadyAsync()) return null;
            try
            {
                var e = await LeaderboardsService.Instance.GetPlayerScoreAsync(LeaderboardId,
                    new GetPlayerScoreOptions { IncludeMetadata = true });
                return new ScoreRow { rank = e.Rank + 1, name = NameFrom(e.Metadata, e.PlayerName), score = (int)e.Score, isPlayer = true };
            }
            catch (Exception)
            {
                return null; // todavía no envió puntaje
            }
        }

        static string NameFrom(string metadata, string fallback)
        {
            if (!string.IsNullOrEmpty(metadata))
            {
                try
                {
                    var m = JsonUtility.FromJson<Meta>(metadata);
                    if (!string.IsNullOrEmpty(m.name)) return NameFilter.Clean(m.name);
                }
                catch (Exception) { }
            }
            // PlayerName de Unity trae sufijo #1234
            var n = fallback ?? "???";
            int hash = n.IndexOf('#');
            return NameFilter.Clean(hash > 0 ? n.Substring(0, hash) : n);
        }
    }
}
