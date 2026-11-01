using System;
using System.Collections.Generic;
using UnityEngine;

namespace BarDelPiu.Leaderboard
{
    /// <summary>Ranking local de respaldo (top 10 en PlayerPrefs) para jugar sin conexión.</summary>
    public static class LocalScores
    {
        const string Key = "bdp.localScores";
        const int Max = 10;

        [Serializable]
        class Entry
        {
            public string name;
            public int score;
        }

        [Serializable]
        class Table
        {
            public List<Entry> entries = new List<Entry>();
        }

        static Table Load()
        {
            var json = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return new Table();
            try { return JsonUtility.FromJson<Table>(json) ?? new Table(); }
            catch (Exception) { return new Table(); }
        }

        public static void Add(string name, int score)
        {
            var t = Load();
            t.entries.Add(new Entry { name = name, score = score });
            t.entries.Sort((a, b) => b.score.CompareTo(a.score));
            if (t.entries.Count > Max) t.entries.RemoveRange(Max, t.entries.Count - Max);
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(t));
            PlayerPrefs.Save();
        }

        public static int RankOf(int score)
        {
            var t = Load();
            int rank = 1;
            foreach (var e in t.entries)
                if (e.score > score) rank++;
            return rank;
        }

        public static List<ScoreRow> Top(int count)
        {
            var rows = new List<ScoreRow>();
            var t = Load();
            for (int i = 0; i < t.entries.Count && i < count; i++)
                rows.Add(new ScoreRow { rank = i + 1, name = t.entries[i].name, score = t.entries[i].score });
            return rows;
        }
    }
}
