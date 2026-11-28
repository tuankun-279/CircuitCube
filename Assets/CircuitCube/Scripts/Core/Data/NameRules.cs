using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CircuitCube.Core
{
    /// <summary>Why a name was refused (or <see cref="Ok"/>).</summary>
    public enum NameVerdict { Ok, Empty, Blocked, Reserved }

    /// <summary>
    /// Player names for the leaderboards: letters, digits and _ only, at most <see cref="MaxLength"/>, checked
    /// against word lists kept as data (Resources/NameRules.txt: sections [parts], [words], [reserved]). The game
    /// and the Cloud Code module read the same file, so both refuse the same names.
    /// </summary>
    public sealed class NameRules
    {
        public const int MaxLength = 20;
        public const string Fallback = "Player";

        readonly List<string> _parts = new List<string>();
        readonly List<string> _words = new List<string>();
        readonly List<string> _reserved = new List<string>();

        public int Count => _parts.Count + _words.Count + _reserved.Count;

        /// <summary>Reads the rules file: "[section]" lines, one word per line, "#" comments.</summary>
        public static NameRules Parse(string text)
        {
            var r = new NameRules();
            List<string> into = null;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                string line = raw.Trim();
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash).Trim();
                if (line.Length == 0) continue;
                if (line == "[parts]") into = r._parts;
                else if (line == "[words]") into = r._words;
                else if (line == "[reserved]") into = r._reserved;
                else if (into != null) into.Add(Fold(line));
            }
            return r;
        }

        /// <summary>The name as the boards show it: allowed characters only, spaces as _, cut to the length.</summary>
        public static string Tidy(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in (name ?? "").Trim())
            {
                if (sb.Length >= MaxLength) break;
                if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
                else if (c == ' ' && sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
            }
            return sb.ToString().Trim('_');
        }

        /// <summary>Checks a name; <paramref name="clean"/> is the tidied name to show and save.</summary>
        public NameVerdict Check(string name, out string clean)
        {
            clean = Tidy(name);
            if (clean.Length == 0) return NameVerdict.Empty;
            string folded = Fold(clean);
            foreach (var w in _parts) if (folded.Contains(w)) return NameVerdict.Blocked;
            foreach (var part in folded.Split('_'))
                foreach (var w in _words) if (part == w) return NameVerdict.Blocked;
            string joined = folded.Replace("_", "");
            foreach (var w in _reserved) if (joined.Contains(w.Replace("_", ""))) return NameVerdict.Reserved;
            return NameVerdict.Ok;
        }

        /// <summary>The tidied name, or <see cref="Fallback"/> when it is empty or refused.</summary>
        public string Clean(string name) => Check(name, out var clean) == NameVerdict.Ok ? clean : Fallback;

        /// <summary>Lower case without Vietnamese marks, so "Đ" and "đ" read as "d".</summary>
        public static string Fold(string s)
        {
            var d = (s ?? "").ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in d)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString();
        }
    }
}
