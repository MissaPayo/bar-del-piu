using System.Text;

namespace BarDelPiu.Leaderboard
{
    /// <summary>
    /// Limpia el apodo: solo letras de la fuente pixel, números, espacio y - ; máximo 10 caracteres;
    /// y bloquea insultos comunes (lista básica, ampliable).
    /// </summary>
    public static class NameFilter
    {
        public const int MaxLength = 10;
        const string Allowed = "ABCDEFGHIJKLMNÑOPQRSTUVWXYZÁÉÍÓÚÜ0123456789 -";

        static readonly string[] Blocked =
        {
            "PUTA", "PUTO", "MIERDA", "VERGA", "CONCHA", "CULIAO", "CULIA", "WEON", "HUEVON", "MARICON", "MARICA",
            "PENDEJO", "CABRON", "CULO", "COÑO", "ZORRA", "PERRA", "NAZI", "HITLER",
            "FUCK", "SHIT", "BITCH", "NIGG", "CUNT", "DICK", "XXX",
        };

        public static string Clean(string raw)
        {
            var sb = new StringBuilder();
            foreach (char c in (raw ?? "").ToUpperInvariant())
            {
                if (Allowed.IndexOf(c) >= 0) sb.Append(c);
                if (sb.Length >= MaxLength) break;
            }
            return sb.ToString().Trim();
        }

        public static bool IsAcceptable(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length < 2) return false;
            string flat = Flatten(name);
            foreach (var bad in Blocked)
                if (flat.Contains(bad)) return false;
            return true;
        }

        /// <summary>Quita tildes, espacios y cambia números «leet» por letras para detectar trucos.</summary>
        static string Flatten(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                switch (c)
                {
                    case 'Á': sb.Append('A'); break;
                    case 'É': sb.Append('E'); break;
                    case 'Í': sb.Append('I'); break;
                    case 'Ó': sb.Append('O'); break;
                    case 'Ú': case 'Ü': sb.Append('U'); break;
                    case '0': sb.Append('O'); break;
                    case '1': sb.Append('I'); break;
                    case '3': sb.Append('E'); break;
                    case '4': sb.Append('A'); break;
                    case '5': sb.Append('S'); break;
                    case '7': sb.Append('T'); break;
                    case ' ': case '-': case '_': break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
