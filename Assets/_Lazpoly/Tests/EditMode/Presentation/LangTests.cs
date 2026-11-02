using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Texts;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class LangTests
    {
        [TearDown]
        public void BackToTurkish() => Lang.Set(Language.Turkish);

        [Test]
        public void TurkishIsTheDefaultAndTextsPassThrough()
        {
            Assert.AreEqual(Language.Turkish, Lang.Current);
            Assert.AreEqual("Zar At", Lang.L("Zar At"));
            Assert.AreEqual("Tur 3 / 25", Lang.F($"Tur {3} / {25}"));
        }

        [Test]
        public void EnglishTranslatesAndKeepsTheValues()
        {
            Lang.Set(Language.English);
            Assert.AreEqual("Roll Dice", Lang.L("Zar At"));
            Assert.AreEqual("Round 3 / 25", Lang.F($"Tur {3} / {25}"));
            Assert.AreEqual("Bilinmeyen metin", Lang.L("Bilinmeyen metin"), "A missing entry falls back to the Turkish.");
        }

        [Test]
        public void ChangingLanguageBumpsTheVersionSoViewsRedraw()
        {
            int before = Lang.Version;
            Lang.Set(Language.English);
            Assert.AreEqual(before + 1, Lang.Version);
            Lang.Set(Language.English);
            Assert.AreEqual(before + 1, Lang.Version, "Setting the same language again changes nothing.");
            Lang.Set(Language.Turkish);
            Assert.AreEqual(before + 2, Lang.Version);
        }

        [Test]
        public void AutoFollowsTheSystemLanguage()
        {
            Assert.AreEqual(Language.Turkish, Lang.Resolve(Language.Auto, SystemLanguage.Turkish));
            Assert.AreEqual(Language.English, Lang.Resolve(Language.Auto, SystemLanguage.German));
            Assert.AreEqual(Language.Turkish, Lang.Resolve(Language.Turkish, SystemLanguage.English));
        }

        [Test]
        public void TheParserReadsEscapesAndRejectsBrokenFiles()
        {
            Dictionary<string, string> table = Lang.Parse("{ \"a\\nb\": \"x \\\"y\\\" \\u00e7\", \"k\": \"v\" }");
            Assert.AreEqual("x \"y\" ç", table["a\nb"]);
            Assert.AreEqual("v", table["k"]);
            Assert.Throws<FormatException>(() => Lang.Parse("[1]"));
            Assert.Throws<FormatException>(() => Lang.Parse("{ \"a\": \"b\" "));
        }

        [Test]
        public void ThousandsAreGroupedTheWayTheLanguageWritesThem()
        {
            Assert.AreEqual("1.500", Lang.N(1500));
            Assert.AreEqual("-2.000.000", Lang.N(-2000000));
            Assert.AreEqual("999", Lang.N(999));
            Assert.AreEqual("Kira 1.500 ödendi, tur 12", Lang.F($"Kira {1500} ödendi, tur {12}"), "whole numbers in texts are grouped too");
            Assert.AreEqual("0.5", Lang.F($"{0.5f}"), "only whole numbers; the rest keeps the invariant form");

            Lang.Set(Language.English);
            Assert.AreEqual("1,500", Lang.N(1500));
            Assert.AreEqual("1,500 (standard)", Lang.F($"{1500} (normal)"), "the text is translated and the number grouped");
        }

        [Test]
        public void EnglishButtonTextsStayShortEnoughForTheirButtons()
        {
            // A short Turkish text is almost always a button or a heading; its English must fit the same button.
            var tooLong = new List<string>();
            foreach (KeyValuePair<string, string> pair in Lang.Load(Language.English))
            {
                if (pair.Key.Length <= 24 && pair.Value.Length > pair.Key.Length * 1.5f + 6) tooLong.Add($"\"{pair.Key}\" -> \"{pair.Value}\"");
            }

            Assert.IsEmpty(tooLong, "Shorten these English texts:\n" + string.Join("\n", tooLong));
        }

        [Test]
        public void EveryTextInTheCodeHasAnEnglishEntry()
        {
            Dictionary<string, string> english = Lang.Load(Language.English);
            var missing = new List<string>();
            string root = Path.Combine(Application.dataPath, "_Lazpoly", "Code", "Presentation");
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                foreach (string key in SourceTexts.Keys(File.ReadAllText(file)))
                {
                    if (!english.ContainsKey(key)) missing.Add($"{Path.GetFileName(file)}: {key}");
                }
            }

            Assert.IsEmpty(missing, "Add these to UI/Resources/Lazpoly/Lang/en.json:\n" + string.Join("\n", missing));
        }

        [Test]
        public void EnglishEntriesKeepThePlaceholders()
        {
            foreach (KeyValuePair<string, string> pair in Lang.Load(Language.English))
            {
                for (int i = 0; i < 6; i++)
                {
                    Assert.AreEqual(pair.Key.Contains("{" + i), pair.Value.Contains("{" + i), $"placeholder {{{i}}} differs in \"{pair.Key}\"");
                }
            }
        }

        [Test]
        public void CardsAndSpecialSquaresOfTheBoardAreTranslated()
        {
            Dictionary<string, string> english = Lang.Load(Language.English);
            BoardData data = BoardDataLoader.LoadFromResources();
            foreach (Core.Board.CardDefinition card in data.Board.Cards) Assert.IsTrue(english.ContainsKey(card.Text), card.Text);
            for (int i = 0; i < data.Board.Count; i++)
            {
                if (data.Board[i].Type != Core.Board.SquareType.Property) Assert.IsTrue(english.ContainsKey(data.Board[i].Name), data.Board[i].Name);
            }
        }

        [Test]
        public void TheSourceScannerFindsPlainAndInterpolatedTexts()
        {
            const string code = "ui.Label(L(\"Merhaba \\\"sen\\\"\")); x = F($\"Tur {state.Round} / {limit:00}{(a ? \"}\" : \"\")}\"); y = \"not wrapped\";";
            List<string> keys = SourceTexts.Keys(code);
            CollectionAssert.AreEqual(new[] { "Merhaba \"sen\"", "Tur {0} / {1:00}{2}" }, keys);
        }
    }

    /// <summary>Finds the texts wrapped in L("...") and F($"...") in C# source, as the keys the language file needs.</summary>
    public static class SourceTexts
    {
        public static List<string> Keys(string source)
        {
            var keys = new List<string>();
            int i = 0;
            while (i < source.Length)
            {
                int plain = source.IndexOf("L(\"", i, StringComparison.Ordinal);
                int interpolated = source.IndexOf("F($\"", i, StringComparison.Ordinal);
                if (plain < 0 && interpolated < 0) break;
                bool isInterpolated = plain < 0 || (interpolated >= 0 && interpolated < plain);
                int start = isInterpolated ? interpolated + 4 : plain + 3;
                // Only our helpers: the character before must not continue an identifier (e.g. "URL(").
                int nameStart = isInterpolated ? interpolated : plain;
                if (nameStart > 0 && (char.IsLetterOrDigit(source[nameStart - 1]) || source[nameStart - 1] == '_' || source[nameStart - 1] == '.'))
                {
                    i = start;
                    continue;
                }

                string key = isInterpolated ? ReadInterpolated(source, ref start) : ReadPlain(source, ref start);
                if (HasLetters(key)) keys.Add(key);
                i = start;
            }

            return keys;
        }

        private static bool HasLetters(string s)
        {
            foreach (char c in s) if (char.IsLetter(c)) return true;
            return false;
        }

        private static string ReadPlain(string s, ref int i)
        {
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    sb.Append(s[i] == 'n' ? '\n' : s[i]);
                }
                else sb.Append(s[i]);
                i++;
            }

            i++;
            return sb.ToString();
        }

        /// <summary>Replaces each {expression[:format]} with {n[:format]}, the way FormattableString.Format does.</summary>
        private static string ReadInterpolated(string s, ref int i)
        {
            var sb = new StringBuilder();
            int n = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    sb.Append(s[i + 1] == 'n' ? '\n' : s[i + 1]);
                    i += 2;
                    continue;
                }

                if (c == '"') { i++; return sb.ToString(); }
                if (c == '{' && i + 1 < s.Length && s[i + 1] == '{') { sb.Append("{{"); i += 2; continue; }
                if (c == '}' && i + 1 < s.Length && s[i + 1] == '}') { sb.Append("}}"); i += 2; continue; }
                if (c == '{')
                {
                    i++;
                    string format = SkipExpression(s, ref i);
                    sb.Append('{').Append(n++).Append(format).Append('}');
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        /// <summary>Skips to the matching '}' through nested braces, parentheses and strings; returns ":format" if any.</summary>
        private static string SkipExpression(string s, ref int i)
        {
            int depth = 1, parens = 0;
            string format = string.Empty;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '"')
                {
                    i++;
                    ReadPlain(s, ref i);
                    continue;
                }

                if (c == '\'' ) { i += s[i + 1] == '\\' ? 4 : 3; continue; }
                if (c == '(' || c == '[') parens++;
                else if (c == ')' || c == ']') parens--;
                else if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) { i++; return format; }
                }
                else if (c == ':' && depth == 1 && parens == 0 && format.Length == 0)
                {
                    int close = s.IndexOf('}', i);
                    format = s.Substring(i, close - i);
                    i = close + 1;
                    return format;
                }

                i++;
            }

            return format;
        }
    }
}
