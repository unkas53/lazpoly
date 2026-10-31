using System;
using System.Collections.Generic;
using System.Globalization;
using Lazpoly.Infra.Settings;
using UnityEngine;

namespace Lazpoly.Presentation.Texts
{
    /// <summary>
    /// Translation by lookup: the Turkish text in the code is the key, and one file per other
    /// language maps it to that language (<c>Resources/Lazpoly/Lang/en.json</c>). Wrap plain
    /// strings in <see cref="L"/> and interpolated ones in <see cref="F"/>; a missing entry falls
    /// back to the Turkish. Until the settings are applied the game speaks Turkish, so tests do too.
    /// </summary>
    public static class Lang
    {
        public const string ResourceFolder = "Lazpoly/Lang/";

        private static Dictionary<string, string> _table;
        private static Language _current = Language.Turkish;

        /// <summary>The language in use after <see cref="Apply"/> resolved Auto.</summary>
        public static Language Current => _current;

        /// <summary>Grows on every language change; views mix it into their keys so they redraw.</summary>
        public static int Version { get; private set; }

        /// <summary>Turns a setting into a language: Auto follows the system, falling back to English for anything but Turkish.</summary>
        public static Language Resolve(Language setting, SystemLanguage system)
        {
            if (setting != Language.Auto) return setting;
            return system == SystemLanguage.Turkish ? Language.Turkish : Language.English;
        }

        /// <summary>Switches language; loads the table on the first use of a non-Turkish one.</summary>
        public static void Apply(Language setting) => Set(Resolve(setting, Application.systemLanguage));

        public static void Set(Language language)
        {
            if (language == Language.Auto) throw new ArgumentException("Resolve Auto before setting the language.", nameof(language));
            if (language == _current && (language == Language.Turkish || _table != null)) return;
            _current = language;
            _table = language == Language.Turkish ? null : Load(language);
            Version++;
        }

        /// <summary>Looks a Turkish text up in the current language; the text itself when there is no entry.</summary>
        public static string L(string turkish)
        {
            if (turkish == null || _table == null) return turkish;
            return _table.TryGetValue(turkish, out string translated) ? translated : turkish;
        }

        /// <summary>Translates the template of an interpolated string, then fills in its values.</summary>
        public static string F(FormattableString text)
        {
            if (text == null) return null;
            return string.Format(CultureInfo.InvariantCulture, L(text.Format), text.GetArguments());
        }

        /// <summary>Entries loaded for the current language, for tests; null while Turkish.</summary>
        public static IReadOnlyDictionary<string, string> Table => _table;

        public static string FileNameOf(Language language) => language == Language.English ? "en" : language.ToString().ToLowerInvariant();

        /// <summary>Reads the language file: a flat JSON object of Turkish → translated text.</summary>
        public static Dictionary<string, string> Load(Language language)
        {
            var asset = Resources.Load<TextAsset>(ResourceFolder + FileNameOf(language));
            if (asset == null)
            {
                Debug.LogWarning($"[Lazpoly] Language file '{ResourceFolder}{FileNameOf(language)}' is missing.");
                return new Dictionary<string, string>();
            }

            return Parse(asset.text);
        }

        /// <summary>Parses a flat JSON object with string values; Unity's JsonUtility cannot read dictionaries.</summary>
        public static Dictionary<string, string> Parse(string json)
        {
            var table = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(json)) return table;
            int i = 0;
            SkipSpace(json, ref i);
            if (i >= json.Length || json[i] != '{') throw new FormatException("A language file is one JSON object.");
            i++;
            while (true)
            {
                SkipSpace(json, ref i);
                if (i < json.Length && json[i] == '}') return table;
                string key = ReadString(json, ref i);
                SkipSpace(json, ref i);
                if (i >= json.Length || json[i] != ':') throw new FormatException($"Expected ':' after \"{key}\".");
                i++;
                SkipSpace(json, ref i);
                table[key] = ReadString(json, ref i);
                SkipSpace(json, ref i);
                if (i < json.Length && json[i] == ',') i++;
                else if (i >= json.Length || json[i] != '}') throw new FormatException($"Expected ',' or '}}' after \"{key}\".");
            }
        }

        private static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static string ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new FormatException($"Expected a string at {i}.");
            i++;
            var sb = new System.Text.StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }

            throw new FormatException("Unterminated string in language file.");
        }

        // Clears static state when Domain Reload is disabled in Enter Play Mode settings.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _table = null;
            _current = Language.Turkish;
            Version = 0;
        }
    }
}
