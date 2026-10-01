using System;
using TMPro;
using UnityEngine;

namespace Tarneeb.Localization
{
    public enum Language { English = 0, Arabic = 1 }

    /// <summary>
    /// Language state and lookups. English is the default; the player chooses once on first launch (no flags) and can change it in Settings.
    /// Strings are looked up by their English text. Rule for views: callers pass LOGICAL strings (from T/Fmt), and whoever assigns a string to a
    /// TMP text runs it through <see cref="Display"/> (shaping + right-to-left order) exactly once; LocText does that for you.
    /// </summary>
    public static class Loc
    {
        private const string Key = "tarneeb.lang";
        private static Language? _current;

        public static event Action Changed;

        public static bool HasChosen => PlayerPrefs.HasKey(Key);

        public static Language Current
        {
            get
            {
                if (_current == null) _current = (Language)Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, 1);
                return _current.Value;
            }
        }

        public static bool IsRtl => Current == Language.Arabic;

        public static void SetLanguage(Language l, bool persist = true)
        {
            bool changed = Current != l;
            _current = l;
            if (persist) { PlayerPrefs.SetInt(Key, (int)l); PlayerPrefs.Save(); }
            if (changed) Changed?.Invoke();
        }

        /// <summary>Test hook: forget the stored choice.</summary>
        public static void ResetChoice() { PlayerPrefs.DeleteKey(Key); _current = null; }

        public static bool Has(string english) => !string.IsNullOrEmpty(english) && LocTable.Ar.ContainsKey(english);

        /// <summary>The string for the current language (logical order). Unknown strings come back unchanged.</summary>
        public static string T(string english)
        {
            if (Current == Language.Arabic && english != null && LocTable.Ar.TryGetValue(english, out string ar)) return ar;
            return english;
        }

        public static string Fmt(string english, params object[] args) => string.Format(T(english), args);

        /// <summary>The Arabic text for a known English string whatever the current language is (null if there is none).</summary>
        public static string Arabic(string english) => LocTable.Ar.TryGetValue(english, out string ar) ? ar : null;

        /// <summary>Logical text to what TMP must show: Arabic is shaped and reordered, anything else is unchanged. Do not apply it twice.</summary>
        public static string Display(string logical) => ArabicShaper.ToVisual(logical);

        /// <summary>Shows a (logical) string on a TMP text and keeps it translated when the language changes.</summary>
        public static void Set(TMP_Text text, string english, params object[] args)
        {
            if (text == null) return;
            LocText lt = text.GetComponent<LocText>();
            if (lt == null) lt = text.gameObject.AddComponent<LocText>();
            lt.SetKey(english, args);
        }
    }
}
