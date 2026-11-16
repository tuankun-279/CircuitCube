using System;
using System.Collections.Generic;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// The game's languages: English (the source text) and Vietnamese. Text is looked up by its English wording, so
    /// code writes <c>Loc.T("Level complete")</c> and a missing translation simply shows the English. Sentences with
    /// numbers or names use a format: <c>Loc.F("Level {0} / {1}", n, total)</c>. Texts placed in the UI prefab carry a
    /// <see cref="CircuitCube.UI.LocText"/> and follow <see cref="Changed"/>. The first start picks the phone's
    /// language; Settings can change it. Translations live in <see cref="LocVi"/>.
    /// </summary>
    public static class Loc
    {
        /// <summary>Language codes in the order of the Settings buttons.</summary>
        public static readonly string[] Languages = { "en", "vi" };

        /// <summary>Raised after the language changes.</summary>
        public static event Action Changed;

        static string _current;
        static Dictionary<string, string> _table;

        public static string Current
        {
            get
            {
                if (_current == null) Apply(Pick(CubeSave.Settings.language));
                return _current;
            }
        }

        public static bool Vietnamese => Current == "vi";

        static string Pick(string saved)
        {
            if (Array.IndexOf(Languages, saved) >= 0) return saved;
            return Application.systemLanguage == SystemLanguage.Vietnamese ? "vi" : "en";
        }

        static void Apply(string lang)
        {
            _current = lang;
            _table = lang == "vi" ? LocVi.Table : null;
        }

        /// <summary>Switch language, save the choice and refresh every text on screen.</summary>
        public static void Set(string lang)
        {
            if (Array.IndexOf(Languages, lang) < 0 || lang == Current) return;
            Apply(lang);
            CubeSave.Settings.language = lang;
            CubeSave.SaveSettings();
            GameAnalytics.Log("language", ("lang", lang));
            Changed?.Invoke();
        }

        /// <summary>The text in the current language (the English itself when there is no translation).</summary>
        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english)) return english;
            if (_current == null) _ = Current;
            return _table != null && _table.TryGetValue(english, out var t) ? t : english;
        }

        public static string F(string englishFormat, object a) => string.Format(T(englishFormat), a);
        public static string F(string englishFormat, object a, object b) => string.Format(T(englishFormat), a, b);
        public static string F(string englishFormat, object a, object b, object c) => string.Format(T(englishFormat), a, b, c);

        /// <summary>Upper case that keeps Vietnamese letters right (the invariant culture does).</summary>
        public static string Upper(string s) => s == null ? null : s.ToUpperInvariant();
    }
}
